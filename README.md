# TakeAuction
A high-traffic, concurrent live auction system built with Vertical Slice Architecture

## Running the whole thing

Everything, containerised, behind the nginx gateway on <http://localhost:8080>:

```bash
docker compose up --detach --wait
```

The gateway routes `/api`, `/hubs` and `/uploads` to the API and everything else to the SPA.
The API migrates and seeds itself on start, so the salon has lots in it straight away.

## Running for development

Bring up only the backing services and run the API and the web client on the host, where
hot reload and the debugger work:

```bash
docker compose up --detach --wait postgres redis rabbitmq

dotnet run --project src/TakeAuction.Api        # http://localhost:5080
npm --prefix src/TakeAuction.Web run dev        # http://localhost:5173
```

## How an event leaves the API

A bid and the event announcing it are written in the same transaction: the bid row and an
`outbox_messages` row commit together, so there is no window where the database and the rest
of the system disagree about what happened.

A dispatcher then moves queued messages to RabbitMQ. It wakes on the commit itself, so a live
bid still reaches watchers in milliseconds, and sweeps on a timer as well, so a message
survives a broker outage or the death of the instance that wrote it. Claims are leased and
taken with `FOR UPDATE SKIP LOCKED`, so running several API instances does not send anything
twice. Delivery is at-least-once — consumers are expected to tolerate a repeat.

## Bidding

A bid is a ceiling, not a price. The house bids on the bidder's behalf only as far as it takes
to lead, so a winner pays one increment over the next-highest ceiling rather than everything
they were prepared to spend, and nobody has to sit at the screen defending a lot by hand. A
challenger who cannot clear the leader's ceiling is answered automatically; a tie goes to the
incumbent, because matching a maximum is not beating it.

Ceilings are never published — not on the detail endpoint, not in the bid history, not over the
hub. Knowing a leader's ceiling is knowing the exact figure that takes the lot, which is the one
thing a sealed maximum exists to prevent.

## Closing a lot

Each lot books its own close for the second it is due, so a sold lot does not sit open waiting
for the next sweep. The recurring sweep stays as the safety net for bookings that were lost.
Closing is idempotent, so whichever of the two arrives second finds the lot already closed.

A bid placed inside the closing window pushes the end out, so a lot only settles once a bid
goes unanswered. The clock is set from the bid rather than added to the old end, which means
every snipe buys the room the same reply window instead of stacking. The window and the
extension are stored on the lot when it is listed, so changing the defaults never moves the
goalposts under an auction that is already running.

## Health

| Path | Answers |
| --- | --- |
| `/health/live` | The process is up. Consults nothing, so a database blip never restarts a healthy instance. |
| `/health/ready` | PostgreSQL, Redis and the RabbitMQ bus are all reachable. |

## What the instruments say

Request counts and latencies come free from the ASP.NET Core instrumentation. What does not,
and what this system is actually judged by, is measured explicitly: how often a bid lost the
row-version race (`takeauction.bids.concurrency_conflicts`), how many passes through the retry
loop a bid took to settle (`takeauction.bids.attempts`), how long that took end to end
(`takeauction.bids.duration`, tagged by outcome), how often a proxy answered for a leader, how
often a lot's close was pushed out, and whether the outbox is keeping up
(`takeauction.outbox.batch_size` — a batch that keeps arriving full is a backlog).

Set `Telemetry__OtlpEndpoint` to ship traces and metrics to a collector. Set
`Telemetry__PrometheusEndpointEnabled` to serve `/metrics` for scraping — the gateway returns
404 for that path, so a collector has to reach the API on the internal network.

## Tests

```bash
dotnet test                                     # unit, integration and API contract suites
npm --prefix tests/TakeAuction.E2E test         # Playwright, see tests/TakeAuction.E2E/README.md
```

The integration and API suites start their own PostgreSQL, Redis and RabbitMQ through
Testcontainers, so Docker has to be running but nothing needs to be up first.

## Deploying

The SPA goes to Vercel, the API and its Postgres and Redis go to Railway. Step by step in
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## Configuration

Secrets are never committed. `Jwt:SigningKey` has no default outside Development and the API
refuses to start without one — supply it through `Jwt__SigningKey`, user secrets or your
platform's secret store.

Connection strings follow the same rule: the localhost values live in
`appsettings.Development.json`, not the base file. Anywhere else they have to be supplied
explicitly, so a missing one fails loudly instead of quietly dialling localhost.
