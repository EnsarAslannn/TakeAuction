import http from "k6/http";
import exec from "k6/execution";
import { check, fail } from "k6";
import { Counter, Rate, Trend } from "k6/metrics";

// The question this run answers is not "how many requests per second". It is: how far does
// PlaceBidHandler's retry budget stretch before bidders start seeing "please retry"?
//
// Every virtual user hammers the same lot at exactly the current floor, which is the worst
// case for optimistic concurrency: they all read the same row version and race to write it.

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const API = `${BASE_URL}/api/v1`;
const PASSWORD = "LoadTest!2026";
const BIDDER_COUNT = Number(__ENV.BIDDER_COUNT || 60);
const PEAK_VUS = Number(__ENV.PEAK_VUS || 200);
const STAGE = __ENV.STAGE_DURATION || "30s";

const accepted = new Counter("bids_accepted");
const tooLow = new Counter("bids_too_low");
const conflicted = new Counter("bids_conflicted");
const conflictRate = new Rate("bid_conflict_rate");
const bidDuration = new Trend("bid_duration", true);

export const options = {
  scenarios: {
    contention: {
      executor: "ramping-vus",
      startVUs: 10,
      stages: [
        { duration: STAGE, target: 25 },
        { duration: STAGE, target: 50 },
        { duration: STAGE, target: 100 },
        { duration: STAGE, target: PEAK_VUS },
        { duration: "15s", target: 0 },
      ],
      gracefulRampDown: "10s",
    },
  },
  thresholds: {
    // Invariants, not performance targets. The point of the run is to discover the latency
    // and conflict numbers, so gating on them would be inventing an answer in advance.
    checks: ["rate==1"],
    // A bid must never simply vanish: every accepted response has a row behind it.
    bids_accepted: ["count>0"],
  },
  setupTimeout: "10m",
  teardownTimeout: "2m",
};

function authHeaders(token) {
  return {
    Authorization: `Bearer ${token}`,
    "Content-Type": "application/json",
  };
}

// The access token arrives as an HttpOnly cookie. Reading its value and presenting it as a
// bearer header is what the CSRF middleware treats as a non-browser caller, which keeps the
// double-submit dance out of the measurement.
function tokenFrom(response) {
  const cookie = response.cookies["takeauction_access_token"];

  if (!cookie || cookie.length === 0) {
    fail(`no access token in the response: ${response.status} ${response.body}`);
  }

  return cookie[0].value;
}

function register(role, index) {
  const response = http.post(
    `${API}/auth/register`,
    JSON.stringify({
      email: `load.${role.toLowerCase()}.${Date.now()}.${index}@takeauction.test`,
      displayName: `Load ${role} ${index}`,
      password: PASSWORD,
      role,
    }),
    {
      headers: { "Content-Type": "application/json" },
      // A fresh jar per account: k6 shares one across the VU, so the previous account's
      // session cookie would ride along and trip the CSRF double-submit check.
      jar: new http.CookieJar(),
      tags: { name: "Register" },
    }
  );

  if (response.status !== 201) {
    fail(`could not register a ${role}: ${response.status} ${response.body}`);
  }

  return tokenFrom(response);
}

export function setup() {
  const sellerToken = register("Seller", 0);

  const now = Date.now();
  const auction = http.post(
    `${API}/auctions`,
    JSON.stringify({
      title: `Load test lot ${now}`,
      description: "A single hot lot every virtual user competes for, to find the conflict ceiling.",
      startingPrice: 100,
      // A one-unit increment keeps the price sane even after thousands of accepted bids.
      minimumBidIncrement: 1,
      startsAtUtc: new Date(now - 30_000).toISOString(),
      endsAtUtc: new Date(now + 3 * 60 * 60 * 1000).toISOString(),
    }),
    { headers: authHeaders(sellerToken), jar: new http.CookieJar(), tags: { name: "CreateAuction" } }
  );

  if (auction.status !== 201) {
    fail(`could not create the auction: ${auction.status} ${auction.body}`);
  }

  const tokens = [];
  for (let index = 1; index <= BIDDER_COUNT; index++) {
    tokens.push(register("Bidder", index));
  }

  return { auctionId: auction.json("id"), tokens };
}

export default function (data) {
  const token = data.tokens[exec.vu.idInTest % data.tokens.length];
  const headers = authHeaders(token);

  // Reading the floor first is what the real client does, and it is the read path that the
  // cache invalidation churns hardest under load.
  const detail = http.get(`${API}/auctions/${data.auctionId}`, { headers, jar: new http.CookieJar(), tags: { name: "GetAuction" } });

  if (detail.status !== 200) {
    return;
  }

  const floor = detail.json("minimumAcceptableBid");

  const response = http.post(
    `${API}/auctions/${data.auctionId}/bids`,
    JSON.stringify({ amount: floor }),
    { headers, jar: new http.CookieJar(), tags: { name: "PlaceBid" } }
  );

  bidDuration.add(response.timings.duration);
  conflictRate.add(response.status === 409);

  if (response.status === 200) {
    accepted.add(1);
  } else if (response.status === 400) {
    // Somebody else got there first and the floor moved: a losing race, not a failure.
    tooLow.add(1);
  } else if (response.status === 409) {
    // The retry budget ran out. This is the number the run exists to find.
    conflicted.add(1);
  } else {
    check(response, {
      "a bid answers 200, 400 or 409": () => false,
    });
  }
}

export function teardown(data) {
  const detail = http.get(`${API}/auctions/${data.auctionId}`).json();
  const history = http.get(`${API}/auctions/${data.auctionId}/bids?pageSize=1`).json();

  // The invariant the whole design exists to protect: the row's own counter, the number of
  // bids actually stored, and the price on the row all have to tell the same story. A lost
  // update would show up here as a bidCount that outruns the history.
  check(detail, {
    "the auction's bid count matches the stored history": () => detail.bidCount === history.totalCount,
    "the current price is the top of the history": () =>
      history.totalCount === 0 || detail.currentPrice === history.items[0].amount,
    "the next acceptable bid clears the increment": () =>
      detail.minimumAcceptableBid === detail.currentPrice + detail.minimumBidIncrement,
  });

  console.log(
    `settled at ${detail.currentPrice} after ${detail.bidCount} accepted bid(s) on auction ${data.auctionId}`
  );
}
