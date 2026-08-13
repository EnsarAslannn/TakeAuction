# TakeAuction load tests

One k6 scenario, pointed at the question the other suites cannot answer: **how far does
`PlaceBidHandler`'s retry budget stretch before bidders start seeing "please retry"?**

Every virtual user bids on the same lot at exactly the current floor. That is the worst case
for optimistic concurrency — they all read the same row version and race to write it.

## What it measures

| Metric | Meaning |
| --- | --- |
| `bids_accepted` | Bids that won their race (200). |
| `bids_too_low` | Bids that lost cleanly because the floor had already moved (400). |
| `bids_conflicted` | Bids that exhausted `MaxAttempts` retries (409). **This is the number the run exists to find.** |
| `bid_conflict_rate` | Share of attempts that ended in a conflict. |
| `bid_duration` | Latency of the bid itself, separate from the read that precedes it. |

The thresholds encode invariants, not performance targets — gating on a latency number would
be inventing the answer the run is supposed to discover. What must never break is checked in
`teardown`: the auction's own bid counter, the number of stored bids and the price on the row
all have to tell the same story. A lost update shows up there as a counter that outruns the
history.

## Running it

The rate limiter has to be lifted first, or the run measures the limiter rather than the
database — two hundred virtual users from one host all land in the same partition.

```bash
docker compose -f docker-compose.yml -f docker-compose.load.yml up --detach --wait

docker run --rm --network takeauction_takeauction-network \
  -v "$PWD/tests/TakeAuction.LoadTests:/scripts" \
  -e BASE_URL=http://nginx \
  grafana/k6 run /scripts/bidding-contention.js
```

Knobs, all optional: `PEAK_VUS` (default 200), `STAGE_DURATION` (default 30s),
`BIDDER_COUNT` (default 60), `BASE_URL`.

A quick shape check before the real thing:

```bash
docker run --rm --network takeauction_takeauction-network \
  -v "$PWD/tests/TakeAuction.LoadTests:/scripts" \
  -e BASE_URL=http://nginx -e PEAK_VUS=40 -e STAGE_DURATION=10s -e BIDDER_COUNT=15 \
  grafana/k6 run /scripts/bidding-contention.js
```

## What the first run found

At 200 virtual users on a single lot, against the containerised stack on one developer
machine: ~238k bid attempts, ~10.5k accepted, **zero conflicts**. The retry loop absorbed all
of the contention and turned it into clean "your bid is too low" answers, which is the
behaviour a bidder can act on. `MaxAttempts = 3` was not the ceiling at that scale.

The run did surface something else: the auction detail endpoint could serve a stale price for
up to its cache TTL, because a reader that missed the cache could publish its snapshot *after*
a bid had invalidated the entry. The detail cache is now keyed by a generation the way the
listing already was, so a late writer lands on a key nobody reads.
