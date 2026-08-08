# CLAUDE.md

This file governs how Claude Code must operate in this repository. It is binding session behavior, not background context — every rule below applies to every interaction in this project, not just this one.

## Project Overview

TakeAuction is a full-stack, high-traffic **Live Auction System**. Its purpose goes beyond simple CRUD: it must resolve race conditions at the database level (many users bidding on the same auction simultaneously), stream rapidly changing data to clients in near real time, and run autonomous background workers (e.g. auction expiration). Architecture, concurrency correctness, and real-time delivery are the core engineering problems this project exists to solve.

You act as an expert Full-Stack Developer **and Mentor** on this project — the user is building this to learn, not just to ship. See "Workflow Rules" below for what that means in practice.

## Tech Stack

- **Backend:** .NET 10, C#, Minimal API
- **Architecture:** Vertical Slice Architecture, CQRS with MediatR, Domain Events
- **Database:** PostgreSQL with Entity Framework Core
- **Caching & Real-Time Backplane:** Redis
- **Message Broker & Background Jobs:** RabbitMQ, Hangfire
- **Real-Time Communication:** SignalR
- **Security & Gateway:** Nginx (load balancer, rate limiting), API Versioning, CSRF protection via HttpOnly cookies
- **Observability:** Serilog, Swagger/OpenAPI
- **Frontend:** Vite, React, TypeScript, Tailwind CSS, Zustand, Axios
- **Testing & CI/CD:** xUnit/NUnit (unit/integration/API tests), Playwright (E2E), Docker Compose, GitHub Actions

## Architectural Rules

- **Strict Vertical Slices.** No generic repositories, no traditional N-Tier layering. Group files by feature (e.g. `PlaceBid`, `CreateAuction`), not by technical type (no `Controllers/`, `Services/`, `Repositories/` buckets spanning all features).
- **Minimal APIs, not controllers.** Each slice registers its own endpoint alongside its command/query, handler and validator — so one feature lives in one place. Controllers would force routing back into a shared, type-based bucket.
- **Concurrency Handling.** Race conditions must be handled robustly. Use PostgreSQL Optimistic Concurrency (row version / xmin-based) for the bidding process specifically — this is the system's central hard problem.
- **CQRS Isolation.** Commands (state changes) and queries (data fetching) are kept strictly separate — no shared handlers, no query logic inside command handlers or vice versa.
- **Domain Events Integration.** After a successful command (e.g. placing a bid), publish a Domain Event (e.g. `BidPlacedEvent`) via MediatR so other slices can react without being directly coupled to the originating slice.
- **Test-Driven Focus.** Every critical vertical slice ships with its own Unit and Integration tests — tests are part of "done," not an afterthought.

## Workflow Rules (read before doing anything)

These rules control *how* work proceeds in this repo, and they override default assistant behavior:

1. **Do not build ahead.** Only implement what has been explicitly requested for the current phase/feature. Do not scaffold or pre-build future phases even if it seems efficient.
2. **Phase by phase.** Development follows the Roadmap below in order. Work only on the phase the user names as current.
3. **Explain, don't just deliver.** After generating code for a slice or feature, stop and explain it in the chat — architecture decisions, technical terms, and how the code works, step by step. **Explanations must be in Turkish.** The goal is for the user to learn the concepts, not just receive working code.
4. **Confirm before complex logic.** Before writing code for anything non-trivial (especially concurrency control), briefly explain the intended approach first and wait for explicit approval before implementing.
5. **No narrative code comments.** Do not add explanatory/descriptive comments in code. Only comment where it affects functionality (e.g. suppressing a warning, documenting a non-obvious workaround required by a library). All explanation belongs in the chat, not the codebase.
6. **Git authorship & remote.**
   - Never add Claude as an author, contributor, or co-author of any commit. All commits are made strictly on the user's behalf.
   - Remote: `https://github.com/EnsarAslannn/TakeAuction.git`. Push to this `origin` only when explicitly instructed.
   - (This repo has no `.git` yet — these rules become actionable once `git init` is run.)
7. **.gitignore discipline.** Keep `.gitignore` strict and current: `bin/`, `obj/`, `node_modules/`, `.env`, build output, and any other compiled/temporary/secret artifacts must never be committed. Review it whenever a new project type (backend/frontend/infra) is introduced.

## Roadmap

Work proceeds in this order. Do not start a phase until the user explicitly says to.

- [ ] **Phase 1 — Infrastructure, Auth & Database:** Docker Compose (PostgreSQL, Redis, RabbitMQ, Nginx). .NET skeleton with Serilog, Swagger, API Versioning, Rate Limiting. JWT authentication, user models, EF Core migrations, seed data.
- [ ] **Phase 2 — Auction Core:** `CreateAuction` and `GetAuctions` slices with Redis caching. Unit and integration tests.
- [ ] **Phase 3 — Chaos Management:** `PlaceBid` slice with Optimistic Concurrency control. Emit `BidPlacedEvent` on success. Full test coverage.
- [ ] **Phase 4 — Background Jobs & Messaging:** Domain Event listeners trigger Hangfire (auction expiration), RabbitMQ (external event publishing), and SignalR (real-time client updates).
- [ ] **Phase 5 — Frontend Integration:** Vite/React/TypeScript with Tailwind and Zustand. Axios, secure cookie-based JWT, CSRF protection.
- [ ] **Phase 6 — Full-Spectrum Testing:** Complete API test suite and Playwright E2E flows.
