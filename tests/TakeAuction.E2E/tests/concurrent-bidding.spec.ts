import { expect, test } from "@playwright/test";
import { getAuction, placeBid, registerBidder, seedOpenAuction, uniqueTitle } from "../fixtures/api";
import { openBidder, type Bidder } from "../support/bidder";
import { AuctionsPage } from "../support/auctionsPage";
import { amountPattern } from "../support/money";

const STARTING_PRICE = 1000;
const INCREMENT = 50;

// Five browsers plus the whole stack on two shared cores: the runner starves before the code does.
const CROWDED_RUNNER_TIMEOUT_MS = 45_000;

test.describe("Eşzamanlı teklif ve canlı fiyat", () => {
  test("aynı anda gelen iki eşit teklifden yalnızca biri kabul edilir", async ({ browser, request }) => {
    const auction = await seedOpenAuction(request, {
      title: uniqueTitle("Eşzamanlı yarış"),
      startingPrice: STARTING_PRICE,
      minimumBidIncrement: INCREMENT,
    });

    const alice = await openBidder(browser, auction);
    const bob = await openBidder(browser, auction);

    try {
      expect(await alice.panel.readAmount()).toBe(STARTING_PRICE);
      expect(await bob.panel.readAmount()).toBe(STARTING_PRICE);

      await Promise.all([
        alice.panel.setAmount(STARTING_PRICE),
        bob.panel.setAmount(STARTING_PRICE),
      ]);

      await Promise.all([alice.panel.submit(), bob.panel.submit()]);

      const [aliceOutcome, bobOutcome] = await Promise.all([
        alice.panel.outcome(),
        bob.panel.outcome(),
      ]);

      const outcomes = [aliceOutcome, bobOutcome];
      const winners = outcomes.filter((outcome) => outcome.kind === "leading");

      expect(
        winners,
        `expected exactly one winner, got: ${outcomes.map((o) => `${o.kind} (${o.text})`).join(" | ")}`
      ).toHaveLength(1);

      const persisted = await getAuction(request, auction.id);
      expect(persisted.bidCount).toBe(1);
      expect(persisted.currentPrice).toBe(STARTING_PRICE);
      expect(persisted.minimumAcceptableBid).toBe(STARTING_PRICE + INCREMENT);

      const loser = aliceOutcome.kind === "leading" ? bob : alice;

      await expect(loser.panel.currentPrice).toHaveText(amountPattern(STARTING_PRICE));
      await expect(loser.panel.liveFeedItems).toHaveCount(1);
      await expect(loser.panel.bidCounter).toHaveText(/1 teklif verildi/);
    } finally {
      await Promise.all([alice.context.close(), bob.context.close()]);
    }
  });

  test("geç kalan teklif reddedilir ve ekran canlı fiyatla düzeltilir", async ({
    browser,
    request,
  }) => {
    const auction = await seedOpenAuction(request, {
      title: uniqueTitle("Geç kalan teklif"),
      startingPrice: STARTING_PRICE,
      minimumBidIncrement: INCREMENT,
    });

    const bob = await openBidder(browser, auction);
    const aliceApi = await browser.newContext();

    try {
      await registerBidder(aliceApi.request);

      let release: () => void = () => undefined;
      let parked: () => void = () => undefined;

      const held = new Promise<void>((resolve) => {
        release = resolve;
      });
      const bidLeftTheBrowser = new Promise<void>((resolve) => {
        parked = resolve;
      });

      await bob.page.route("**/api/v1/auctions/*/bids", async (route) => {
        parked();
        await held;
        await route.continue();
      });

      await bob.panel.setAmount(STARTING_PRICE);
      await bob.panel.submit();
      await bidLeftTheBrowser;

      await placeBid(aliceApi.request, auction.id, STARTING_PRICE);

      await expect(bob.panel.currentPrice).toHaveText(amountPattern(STARTING_PRICE));
      await expect(bob.panel.liveFeedItems).toHaveCount(1);

      release();

      const outcome = await bob.panel.outcome();

      expect(outcome.kind, outcome.text).not.toBe("leading");
      expect(outcome.text).toMatch(/1050\.00|öne geçti/);

      const persisted = await getAuction(request, auction.id);
      expect(persisted.bidCount).toBe(1);
      expect(persisted.currentPrice).toBe(STARTING_PRICE);
    } finally {
      await Promise.all([bob.context.close(), aliceApi.close()]);
    }
  });

  test("beş alıcı aynı anda teklif verdiğinde fiyat tek bir değerde uzlaşır", async ({
    browser,
    request,
  }) => {
    const auction = await seedOpenAuction(request, {
      title: uniqueTitle("Beşli yarış"),
      startingPrice: STARTING_PRICE,
      minimumBidIncrement: INCREMENT,
    });

    const bidders: Bidder[] = [];

    for (let index = 0; index < 5; index += 1) {
      bidders.push(await openBidder(browser, auction));
    }

    try {
      const amounts = bidders.map((_, index) => STARTING_PRICE + index * INCREMENT);

      await Promise.all(bidders.map((bidder, index) => bidder.panel.setAmount(amounts[index])));
      await Promise.all(bidders.map((bidder) => bidder.panel.submit()));

      const outcomes = await Promise.all(
        bidders.map((bidder) => bidder.panel.outcome(CROWDED_RUNNER_TIMEOUT_MS))
      );
      const accepted = outcomes.filter(
        (outcome) => outcome.kind === "leading" || outcome.kind === "answered"
      );

      expect(accepted.length, "at least one bid has to get through").toBeGreaterThanOrEqual(1);

      const persisted = await getAuction(request, auction.id);

      expect(persisted.bidCount).toBeGreaterThanOrEqual(accepted.length);
      expect(persisted.minimumAcceptableBid).toBe(persisted.currentPrice + INCREMENT);

      for (const bidder of bidders) {
        await expect(bidder.panel.currentPrice).toHaveText(amountPattern(persisted.currentPrice));
      }
    } finally {
      await Promise.all(bidders.map((bidder) => bidder.context.close()));
    }
  });

  test("salon listesi teklif geldiğinde yenilenmeden güncellenir", async ({
    page,
    browser,
    request,
  }) => {
    const title = uniqueTitle("Lobi yayını");
    const auction = await seedOpenAuction(request, {
      title,
      startingPrice: STARTING_PRICE,
      minimumBidIncrement: INCREMENT,
    });

    const auctions = new AuctionsPage(page);
    await auctions.gotoAndConnect();
    await auctions.filterBy(title);

    await expect(auctions.priceOn(title)).toHaveText(amountPattern(STARTING_PRICE));

    const leaderContext = await browser.newContext();
    const rivalContext = await browser.newContext();

    try {
      await registerBidder(leaderContext.request);
      await placeBid(leaderContext.request, auction.id, 2000);

      await registerBidder(rivalContext.request);
      await placeBid(rivalContext.request, auction.id, 1500);

      await expect(auctions.priceOn(title)).toHaveText(amountPattern(1550));
    } finally {
      await Promise.all([leaderContext.close(), rivalContext.close()]);
    }
  });
});
