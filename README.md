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

## Health

| Path | Answers |
| --- | --- |
| `/health/live` | The process is up. Consults nothing, so a database blip never restarts a healthy instance. |
| `/health/ready` | PostgreSQL, Redis and the RabbitMQ bus are all reachable. |

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
