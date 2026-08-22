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
connects straight to Railway. Being a different site, that connection carries no cookie —
`AuctionHub` is `[AllowAnonymous]`, so it opens anyway and the group broadcasts arrive. It
does need Railway's CORS to name the Vercel origin, because the negotiate step is a
cross-origin POST.

### Why the hub needs a ticket of its own

Group broadcasts are enough for the price ticking on a lot everyone is watching. The outbid
notice is not a broadcast: `SignalRAuctionNotifier.OutbidAsync` sends to
`Clients.User(bidderId)`, which only resolves if the hub connection carries an identity. A
cookieless cross-site connection has none, so on this topology that notice would reach
nobody — and it would still work locally, where the SPA and the API share an origin, so no
test would catch it.

The client therefore fetches a ticket before it connects. `GET /api/v1/auth/hub-ticket`
goes through the Vercel rewrite, so it is same-origin and the session cookie rides along;
it hands back a JWT that lives for `Jwt__HubTicketLifetimeSeconds` (60 by default). The
SignalR client passes it through `accessTokenFactory`, which puts it in the `Authorization`
header on negotiate and in the `access_token` query string on the WebSocket itself.

The ticket carries a `token_use` claim of `hub`, and `OnTokenValidated` refuses a `hub`
token on anything outside `/hubs`. A leaked ticket is therefore worth one minute of
listening on a hub and nothing else — it cannot place a bid.

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

Naming it is not optional here. When neither `KnownProxies` nor `KnownNetworks` is set, the API
falls back to trusting the private ranges only (`127.0.0.0/8`, `10.0.0.0/8`, `172.16.0.0/12`,
`192.168.0.0/16`, `::1/128`, `fc00::/7`) and says so on the startup log. That default is right for
Docker Compose, where nginx reaches the API from the bridge network, and it deliberately refuses a
forwarding header from a public address. Railway's edge is not on a private address, so without the
two entries above every anonymous caller would land in one rate limiting partition.

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

## Session behaviour worth knowing

**Two refreshes at once do not end the session.** Rotation claims the presented token with a
single conditional `UPDATE` that sets `RevokedAtUtc` and `ReplacedByTokenId` together, so exactly
one caller can rotate it. The one that loses gets `409` with its cookies untouched, and the SPA
treats that as "somebody already refreshed me" and retries the original call.

Presenting a token that was rotated within `Jwt__RefreshRotationGraceSeconds` (30 by default) is
read the same way — a race, not a theft — and the family survives. Past that window it is reuse:
the whole family is revoked and every device on it is signed out. Setting the grace to `0` turns
the leniency off entirely and makes any second presentation fatal.

A token revoked by signing out is never forgiven, whatever the grace: only a token that carries a
`ReplacedByTokenId` counts as a rotation, and sign-out does not set one.

**Signing out does not kill the access token already in the browser.** It revokes the refresh
family, so no new access token can be minted, but the one in hand stays valid until it expires —
at most `Jwt__AccessTokenLifetimeMinutes` (15 by default). Shorten that value if the window
matters more than the round trips.

## Uploaded images have a lifetime

`POST /media/images` writes the file before any auction points at it, so an abandoned "create
auction" form leaves a file behind. A Hangfire job (`media:purge-orphan-images`,
`Jobs__PurgeOrphanImagesCron`, 03:30 UTC by default) deletes uploads that no `auctions.ImageUrl`
claims and that were last written more than `Media__OrphanRetentionHours` ago — 24 by default, so
a slow form is never caught mid-fill.

Uploading is also rated on its own: `RateLimiting__MediaUploadPermitLimit` per
`RateLimiting__MediaUploadWindowSeconds`, 20 an hour per signed-in user by default.

## What is not deployed

Swagger and the Hangfire dashboard are mapped only in Development, so neither is reachable in
production. The Hangfire *server* still runs — auction expiry and refresh-token purging happen
on schedule — only its UI is absent.
