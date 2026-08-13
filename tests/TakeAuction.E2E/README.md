# TakeAuction E2E

Playwright specs that drive the real React client against the real API, in a real browser.

## What is covered

| Spec | What it proves |
| --- | --- |
| `tests/auth.spec.ts` | Register, login, cookie session survives a reload, logout. The access token stays `HttpOnly`; the CSRF token stays readable. |
| `tests/auction-journey.spec.ts` | Listing → detail → bid, plus the panel states for anonymous visitors, the seller's own lot, and a lot that has not opened. |
| `tests/concurrent-bidding.spec.ts` | Two (and five) signed-in bidders racing on the same lot, a deliberately late bid, and prices moving over SignalR without a reload. |

Each simulated bidder is a separate `BrowserContext` — its own cookie jar, so they are genuinely
different people rather than one session in two tabs.

## Running locally

The infrastructure has to be up first; Playwright starts the API and the Vite dev server itself.

```bash
docker compose up --detach --wait postgres redis rabbitmq   # from the repo root
cd tests/TakeAuction.E2E
npm ci
npx playwright install chromium
npm test
```

Useful variants:

```bash
npm run test:headed                      # watch the browsers work
npm run test:ui                          # Playwright's interactive runner
npx playwright test concurrent-bidding   # one spec
npm run report                           # open the last HTML report
```

If an API or web server is already running on ports 5080 / 5173, Playwright reuses it instead of
starting its own — except under `CI`, where it always starts a clean pair.

## Notes

- The specs run single-worker on purpose. They assert "exactly one bid won", which overlapping
  workers on one shared database would make impossible to distinguish from a defect.
- The API is started with the auth rate limiter raised: every simulated bidder opens its own
  account from the same host, which the production-shaped limiter would refuse partway through.
- Selectors follow what the visitor reads on screen, so a passing assertion means the browser
  really rendered it.
