# 🔨 TakeAuction

[🇹🇷 Türkçe](README.md) | [🇬🇧 English](README.en.md)

[![CI](https://github.com/EnsarAslannn/TakeAuction/actions/workflows/ci.yml/badge.svg)](https://github.com/EnsarAslannn/TakeAuction/actions/workflows/ci.yml)

A high-traffic, concurrent live auction system built with Vertical Slice Architecture on
.NET, where bids settle through an outbox-backed event pipeline and lots close themselves
on schedule.

**🔗 Live demo:** [take-auction.vercel.app](https://take-auction.vercel.app)

## 📌 About

TakeAuction is a real-time auction platform where sellers list lots, bidders compete with
sealed proxy bids, and every close is driven by the clock rather than a human watching a
dashboard.

A bid and the event announcing it are written in the same database transaction, so the
system is never in a state where a bid exists but nobody was told about it. A background
dispatcher moves those events to RabbitMQ — reacting instantly to a live bid, and sweeping
on a timer so nothing is lost to a broker outage or a dead instance.

The goal isn't just a CRUD app with a bid button; it's an end-to-end system that has to get
concurrency, delivery guarantees and closing semantics right under real contention, backed
by the metrics needed to prove it.

## ⚙️ Key Features

### 🔨 Proxy Bidding

- A bid is a sealed ceiling, not a price — the house bids on the bidder's behalf only as
  far as it takes to lead
- A winner pays one increment over the next-highest ceiling, never everything they were
  willing to spend
- A challenger who can't clear the leader's ceiling is answered automatically; ties go to
  the incumbent
- Ceilings are never exposed — not on the detail endpoint, the bid history, or the hub

### 📡 Transactional Outbox → RabbitMQ

- The bid row and its `outbox_messages` row commit in the same transaction, so the database
  and the event stream can never disagree
- A dispatcher wakes on the commit for millisecond delivery, and sweeps on a timer as a
  safety net
- Claims are leased with `FOR UPDATE SKIP LOCKED`, so multiple API instances never send the
  same message twice
- Delivery is at-least-once by design — consumers are expected to tolerate a repeat

### ⏱️ Self-Closing, Anti-Snipe Lots

- Each lot books its own close for the exact second it's due, instead of waiting on a
  sweep
- A recurring sweep remains as the safety net for any booking that gets lost
- Closing is idempotent, so whichever trigger arrives second finds the lot already closed
- A bid inside the closing window pushes the end out from the bid itself (not the old end),
  so every snipe buys the same reply window instead of stacking

### 🩺 Health & Operations

- `/health/live` — process liveness, checks nothing downstream
- `/health/ready` — PostgreSQL, Redis and RabbitMQ are all reachable
- `/metrics` — Prometheus scraping endpoint, internal-network only (the gateway 404s it)

### 📊 Observability

- Request counts and latencies come free from ASP.NET Core instrumentation
- Custom metrics track what the system is actually judged by: concurrency conflicts on
  bids (`takeauction.bids.concurrency_conflicts`), retry attempts per bid
  (`takeauction.bids.attempts`), end-to-end bid duration by outcome
  (`takeauction.bids.duration`), how often a proxy answered for a leader, how often a
  close was pushed out, and outbox batch fullness (`takeauction.outbox.batch_size`)
- Traces and metrics ship to an OTLP collector via `Telemetry__OtlpEndpoint`

### 🔐 Secrets & Configuration

- `Jwt:SigningKey` has no default outside Development — the API refuses to start without
  one, supplied via `Jwt__SigningKey`, user secrets, or the platform's secret store
- Connection strings follow the same rule: localhost values only live in
  `appsettings.Development.json`, so a missing one fails loudly instead of silently
  dialling localhost

## 🏗️ Architecture

The API is organized as Vertical Slices rather than horizontal layers:

```
src/TakeAuction.Api/Features → Auctions, Auth, Media — each slice owns its request,
                                handler and validation end to end
Outbox + dispatcher            → guarantees the database and RabbitMQ never disagree
Hangfire                       → per-lot close scheduling and the recurring sweep
```

An nginx gateway sits in front of everything: it routes `/api`, `/hubs` and `/uploads` to
the API and everything else to the SPA, so the browser only ever talks to one origin. The
gateway is also the first line of defence: 20 requests per second for `/api`, one per
second for sign-in and registration, and 20 concurrent hub connections per address. The
application's own limits run again behind it.

The frontend is a separate React + TypeScript project (`src/TakeAuction.Web`), talking to
the API over REST and SignalR.

## 🛠️ Tech Stack

**Backend**

- .NET 10, ASP.NET Core Web API
- PostgreSQL (Entity Framework Core / Npgsql)
- Redis
- RabbitMQ (MassTransit)
- Hangfire
- MediatR, FluentValidation, Serilog, OpenTelemetry
- JWT-based authentication

**Frontend**

- React 18 + TypeScript
- Vite
- Tailwind CSS
- Zustand, Axios, React Router
- React Three Fiber / drei, GSAP, Lenis

**Test**

- xUnit, NSubstitute
- Testcontainers (Postgres, Redis & RabbitMQ — real integration tests)
- Playwright (end-to-end)

**Deployment**

- API + PostgreSQL + Redis: Docker on Railway
- Frontend: Vercel
- nginx gateway: Docker Compose

## 🚀 Setup

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 22+
- Docker (Postgres, Redis and RabbitMQ)

### Running everything (containerised)

```bash
docker compose up --detach --wait # http://localhost:8080
```

The gateway routes `/api`, `/hubs` and `/uploads` to the API and everything else to the
SPA. The API migrates and seeds itself on start.

### Running for development

```bash
docker compose up --detach --wait postgres redis rabbitmq

dotnet run --project src/TakeAuction.Api # http://localhost:5080
npm --prefix src/TakeAuction.Web run dev # http://localhost:5173
```

### Tests

```bash
dotnet test # unit, integration and API contract suites
npm --prefix tests/TakeAuction.E2E test # Playwright, see tests/TakeAuction.E2E/README.md
```

> The integration and API suites start their own PostgreSQL, Redis and RabbitMQ through
> Testcontainers — Docker has to be running, but nothing needs to be up first.

### Deploying

The SPA goes to Vercel, the API and its Postgres and Redis go to Railway. Step by step in
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## 📸 Screenshots

**Home Page**

<p align="center">
<img src="docs/screenshots/homePage.png" width="800"/>
<img src="docs/screenshots/homePage2.png" width="800"/>
</p>

**Auctions**

<p align="center">
<img src="docs/screenshots/auctions.png" width="800"/>
</p>

**Auction Detail**

<p align="center">
<img src="docs/screenshots/auction.png" width="800"/>
</p>

## 📄 License

MIT — see [LICENSE](./LICENSE).
