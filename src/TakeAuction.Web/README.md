# TakeAuction.Web

Vite + React + TypeScript frontend for the TakeAuction live auction platform.

## Running

```bash
# 1. Infrastructure
docker compose up -d postgres redis rabbitmq   # from the repo root

# 2. API (http://localhost:5080)
dotnet run --project src/TakeAuction.Api

# 3. Frontend (http://localhost:5173)
cd src/TakeAuction.Web
npm install
npm run dev
```

`vite.config.ts` proxies `/api` and `/hubs` to `http://localhost:5080`, so the browser
sees a single origin in development and the session cookies are first-party. Set
`VITE_API_BASE` / `VITE_HUB_URL` only when serving the frontend from a different origin —
in that case the API's `Cors:AllowedOrigins` must list that origin, and `AuthCookies:SameSite`
must be `None` with `SecureAlways` enabled.

## Assets (not in git)

`public/models/*.glb` and `public/visuals/*.jpg` are gitignored. Without them the app still
runs — the API, listing, bidding and realtime all work — but the 3D stage and the editorial
imagery render empty.

To restore them:

- **Models** — copy the five source `.glb` files into `public/models/` as `bmw-m5.glb`,
  `canon-5d.glb`, `sofa.glb`, `satellite.glb`, `fridge.glb`, then Draco-compress each one:
  ```bash
  npx @gltf-transform/cli draco input.glb public/models/<name>.glb
  ```
  The Draco decoder itself is committed under `public/draco/`, so no CDN is involved.
- **Visuals** — five generated images: `hero-atrium.jpg`, `concurrency-lattice.jpg`,
  `realtime-signal.jpg`, `vault-archive.jpg`, `texture-plaster.jpg`.

## Structure

| Path | Purpose |
| --- | --- |
| `src/api/` | Axios client (CSRF header injection), typed endpoint wrappers |
| `src/realtime/` | SignalR hub singleton with ref-counted group subscriptions |
| `src/store/` | Zustand auth store |
| `src/three/` | R3F model loader, lighting rig, showcase canvas |
| `src/motion/` | Lenis smooth scroll, scroll-reveal primitives |
| `src/sections/` | Landing page sections |
| `src/pages/` | Routed pages |
| `src/content/catalog.ts` | Maps seeded auction titles to their GLB model |

`src/content/catalog.ts` matches auctions to models **by exact title**. If the seeded titles
in `ShowcaseCatalog.cs` change, update the titles here to match or the showcase falls back to
a deterministic per-id pick.

## Design tokens

Palette derived from the reference screenshot, defined in `tailwind.config.ts`:
`ink` (#1A1815), `paper` (#F4F1EB), `sand` (#C0A070), `slate` (#7E9BBD), `stone` (#8B837C).
