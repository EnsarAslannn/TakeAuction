# TakeAuction on Vercel

The .NET API uses the Dockerfile in `src/TakeAuction.Api`. Deploy the source at
the repository root with that Dockerfile at `Dockerfile.vercel` and
`api.vercel.json` at `vercel.json`, as the `take-auction-api` container
project. Connect Neon and Upstash Redis in the same region. The scheduler is a
separate Next.js project rooted at `src/TakeAuction.Scheduler`.

Set these production variables in the API project:

| Variable | Value |
| --- | --- |
| `PORT` | `8080` |
| `Jwt__SigningKey` | A unique random signing key, stored as Sensitive |
| `Database__MigrateOnStartup` | `true` |
| `Seed__Enabled` | `false` |
| `Media__StoreInPostgres` | `true` |
| `Media__MaxImageSizeInBytes` | `4194304` |
| `Cors__AllowedOrigins__0` | The frontend's HTTPS origin |
| `DurableJobs__Enabled` | `true` |
| `DurableJobs__SchedulerUrl` | The scheduler URL ending in `/api/schedule` |
| `DurableJobs__Secret` | A random secret of at least 32 characters, stored as Sensitive |

In the scheduler project, set `DURABLE_JOBS_SECRET` to the same job secret,
`CRON_SECRET` to a different random secret, and `TAKEAUCTION_API_JOB_URL` to
the API URL ending in `/internal/durable-jobs`. Do not expose these values to
the Vite app. The scheduler's production configuration registers a recurring
reconciliation and a daily cleanup. The individual Workflow timers use the
auction's own start and end times; the sweep checks for timers that were missed.

Before routing the existing frontend to the API, verify readiness, the auction
list, login, authenticated bidding, image upload and retrieval, a SignalR
connection, and a complete auction lifecycle against an isolated database.
Keep the old deployment and an export until the frontend cutover is verified.
