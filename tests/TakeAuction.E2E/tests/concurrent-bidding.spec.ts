import { expect, test } from "@playwright/test";
import { getAuction, placeBid, registerBidder, seedOpenAuction, uniqueTitle } from "../fixtures/api";
import { openBidder, type Bidder } from "../support/bidder";
import { AuctionsPage } from "../support/auctionsPage";
import { amountPattern } from "../support/money";

const STARTING_PRICE = 1000;
const INCREMENT = 50;

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
      // Both screens agree on the floor before anybody moves, which is what makes the
      // two submissions collide on the same row version instead of queueing politely.
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
      const winners = outcomes.filter((outcome) => outcome.kind === "accepted");

      expect(
        winners,
        `expected exactly one winner, got: ${outcomes.map((o) => `${o.kind} (${o.text})`).join(" | ")}`
      ).toHaveLength(1);

      // The optimistic concurrency check is only worth anything if the database agrees
      // with what the two screens were told.
      const persisted = await getAuction(request, auction.id);
      expect(persisted.bidCount).toBe(1);
      expect(persisted.currentPrice).toBe(STARTING_PRICE);
      expect(persisted.minimumAcceptableBid).toBe(STARTING_PRICE + INCREMENT);

      const loser = aliceOutcome.kind === "accepted" ? bob : alice;

      // The loser never reloaded: everything below arrived over the hub.
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

      // Hold Bob's bid on the wire. His payload is now fixed at the old floor while the
      // world moves on — the deterministic version of losing a race.
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

      // Alice wins the lot while Bob's request is still parked.
      await placeBid(aliceApi.request, auction.id, STARTING_PRICE);

      // Bob's screen is corrected by the hub before his own answer comes back.
      await expect(bob.panel.currentPrice).toHaveText(amountPattern(STARTING_PRICE));
      await expect(bob.panel.liveFeedItems).toHaveCount(1);

      release();

      const outcome = await bob.panel.outcome();

      expect(outcome.kind, outcome.text).not.toBe("accepted");
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

      const outcomes = await Promise.all(bidders.map((bidder) => bidder.panel.outcome()));
      const accepted = outcomes.filter((outcome) => outcome.kind === "accepted");

      expect(accepted.length, "at least one bid has to get through").toBeGreaterThanOrEqual(1);

      const persisted = await getAuction(request, auction.id);

      // No lost updates: the row reflects exactly the bids the clients were told won.
      expect(persisted.bidCount).toBe(accepted.length);
      expect(persisted.minimumAcceptableBid).toBe(persisted.currentPrice + INCREMENT);

      // Every screen converges on the settled price over the hub.
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

    const bidderContext = await browser.newContext();

    try {
      await registerBidder(bidderContext.request);
      await placeBid(bidderContext.request, auction.id, 1500);

      await expect(auctions.priceOn(title)).toHaveText(amountPattern(1500));
    } finally {
      await bidderContext.close();
    }
  });
});
