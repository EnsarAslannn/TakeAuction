# Deploying to Vercel and Railway

The SPA is served by Vercel, the API and its backing services run on Railway.

```
browser ──► <app>.vercel.app ──┬── /api/*     ─┐
                               ├── /uploads/* ─┼─► <api>.up.railway.app ──┬─► Postgres
                               └── everything else: index.html            └─► Redis
        └──────────── /hubs/auctions (WebSocket, direct) ────────────────►
```

## Why the API is proxied instead of called directly

Authentication is a first-party cookie: the API sets `takeauction_access_token` (HttpOnly) and
`takeauction_csrf` alongside it. `*.vercel.app` and `*.up.railway.app` are separate sites, so a
browser talking to Railway from a Vercel page treats those cookies as third-party — Safari and
Firefox drop them outright and login silently fails.

Routing `/api` and `/uploads` through Vercel's rewrites puts everything on one origin. The
cookies stay first-party with `SameSite=Lax`, CORS never enters the picture, and the relative
`imageUrl` values the media slice returns resolve without change.

The SignalR hub is the exception: Vercel does not proxy WebSocket upgrades, so the client
connects straight to Railway. That works because `AuctionHub` is `[AllowAnonymous]` — the
connection carries no cookie and needs none. It does need Railway's CORS to name the Vercel
origin, because the negotiate step is a cross-origin POST.

## 1. Railway

Create a project with three services: **Postgres**, **Redis**, and the API.

### API service

Railway reads `railway.json` at the repository root: it builds `src/TakeAuction.Api/Dockerfile`
with the repository root as the build context and probes `/health/live`.

Point the service at the GitHub repository and leave the root directory as `/`.

### Variables

Set these on the API service. The `${{Postgres.*}}` and `${{Redis.*}}` references resolve at
deploy time — confirm the exact variable names in each service's *Variables* tab, Railway has
renamed them before.

| Variable | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_HTTP_PORTS` | `8080` |
| `ConnectionStrings__Postgres` | `Host=${{Postgres.RAILWAY_PRIVATE_DOMAIN}};Port=5432;Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}}` |
| `ConnectionStrings__Redis` | `${{Redis.RAILWAY_PRIVATE_DOMAIN}}:6379,password=${{Redis.REDISPASSWORD}},abortConnect=false` |
| `Jwt__SigningKey` | 32+ bytes of randomness, see below |
| `Cors__AllowedOrigins__0` | `https://<app>.vercel.app` |
| `Database__MigrateOnStartup` | `true` |
| `Seed__Enabled` | `true` |
| `Seed__DefaultPassword` | a real password — this one signs into the demo accounts |
| `ReverseProxy__KnownNetworks__0` | `0.0.0.0/0` |
| `ReverseProxy__KnownNetworks__1` | `::/0` |
| `RateLimiting__AuthPermitLimit` | `30`, see below |

Generate the signing key with:

```bash
openssl rand -base64 48
```

No `ConnectionStrings__RabbitMq` is set, so MassTransit falls back to its in-memory transport.
Integration events are still published and consumed in-process; they just do not leave it.
Adding a RabbitMQ service later is one variable away.

#### The client IP behind the proxy

Railway's edge *overwrites* `X-Forwarded-For` with the address that connected to it rather than
appending to it — measured, not assumed: a forged `X-Forwarded-For` sent straight at the Railway
domain is discarded and the real client address still comes through.

Two things follow. Forwarding headers cannot be spoofed here, so `ForwardLimit` stays at its
default of 1; raising it buys nothing, because the chain is never longer than one entry. And on
requests that arrive through the Vercel rewrite, that one entry is Vercel's egress address — the
original client IP is already gone by the time the API sees the request, and no `ForwardLimit`
value brings it back.

`KnownNetworks` still has to trust everything, because Railway's internal addresses are not fixed
and its private network is IPv6, hence both the v4 and v6 entries.

#### What that costs the rate limiter

Signed-in callers partition on `user:{id}` and are unaffected. Anonymous ones partition on the
client IP, which behind the rewrite is one of a handful of Vercel egress addresses — so every
anonymous visitor shares a bucket.

The login policy is the sharp edge, since sign-in requests are anonymous by definition. At its
default of 5 per minute, one shared bucket means the sixth sign-in attempt anywhere locks
everyone out, so this deployment raises `RateLimiting__AuthPermitLimit` to 30. That trades some
brute-force headroom for a usable login; the slow password hash remains the primary defence.

The real fix is a custom domain — `app.example.com` on Vercel, `api.example.com` on Railway.
Same site, so the cookies stay first-party without a rewrite, the browser talks to the API
directly, and real client addresses come back.

### Volume

Uploaded auction images are written to disk, and a container filesystem does not survive a
redeploy. Attach a volume to the API service mounted at:

```
/app/App_Data/uploads
```

The image runs as a non-root user. If uploads fail with a permission error after the volume is
attached, set `RAILWAY_RUN_UID=0` on the service — Railway mounts volumes as root, and this is
its documented escape hatch. It gives up the container's non-root hardening, so only reach for
it if the error actually appears.

### Generate a domain

*Settings → Networking → Generate Domain*. Note the `https://<api>.up.railway.app` hostname; the
next two steps both need it.

## 2. Vercel

Import the same repository and set **Root Directory** to `src/TakeAuction.Web`. The framework
preset, build command and output directory come from `vercel.json`.

Before the first deploy, replace both `REPLACE-ME.up.railway.app` placeholders in
[`src/TakeAuction.Web/vercel.json`](../src/TakeAuction.Web/vercel.json) with the Railway
hostname. Rewrite destinations cannot read environment variables, so the value is committed.

Add one environment variable to the Vercel project:

| Variable | Value |
| --- | --- |
| `VITE_HUB_URL` | `https://<api>.up.railway.app/hubs/auctions` |

`VITE_API_BASE` stays unset — the default `/api/v1` is what keeps the API on the SPA's origin.

Preview deployments get their own `*.vercel.app` hostnames, which the API's CORS list does not
name, so real-time updates are dead on previews until the origin is added. Everything else works,
because the rest goes through the rewrite.

## 3. Verify

```bash
curl https://<api>.up.railway.app/health/ready
```

`postgres` and `redis` should both report healthy.

Then, from the deployed SPA:

- `https://<api>.up.railway.app/api/v1/diagnostics/info` — signed in and called directly,
  `clientIp` should be your own address. Through the Vercel rewrite the same endpoint reports a
  Vercel egress address instead, which is expected and explained above. Anonymous callers get a
  401: the endpoint reports the environment, so it is not left open.
- Sign in as the seeded bidder. A `takeauction_access_token` cookie scoped to the Vercel host
  means the proxy is doing its job.
- Open the same lot as two different bidders and bid from one. Use two browser profiles or a
  private window — plain tabs share one cookie jar, so the second sign-in replaces the first and
  both tabs end up as the same person. The other screen moving without a refresh means the hub
  connected; if it did not, the browser console will name the CORS origin to add. Bid above the
  standing bidder's ceiling, or the proxy answers and the price moves by one increment only.

## What is not deployed

Swagger and the Hangfire dashboard are mapped only in Development, so neither is reachable in
production. The Hangfire *server* still runs — auction expiry and refresh-token purging happen
on schedule — only its UI is absent.
