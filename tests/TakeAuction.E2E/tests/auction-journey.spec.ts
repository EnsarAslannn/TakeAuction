import { expect, test } from "@playwright/test";
import {
  createOpenAuction,
  createScheduledAuction,
  getAuction,
  registerBidder,
  registerSeller,
  seedOpenAuction,
  uniqueTitle,
} from "../fixtures/api";
import { AuctionDetailPage } from "../support/auctionDetailPage";
import { AuctionsPage } from "../support/auctionsPage";
import { amountPattern } from "../support/money";

test.describe("Salondan teklife giden yol", () => {
  test("ziyaretçi listeden parçayı bulup detayına iner", async ({ page, request }) => {
    const title = uniqueTitle("Ziyaretçi turu");
    const auction = await seedOpenAuction(request, { title, startingPrice: 1000 });

    const auctions = new AuctionsPage(page);
    await auctions.goto();
    await auctions.filterBy(title);

    const card = auctions.card(title);
    await expect(card).toBeVisible();
    await expect(auctions.priceOn(title)).toHaveText(amountPattern(1000));

    await card.click();

    await expect(page).toHaveURL(new RegExp(`/auctions/${auction.id}$`));
    await expect(page.getByRole("heading", { name: title })).toBeVisible();
  });

  test("giriş yapmamış ziyaretçiye teklif yerine davet gösterilir", async ({ page, request }) => {
    const auction = await seedOpenAuction(request);

    const detail = new AuctionDetailPage(page, auction.id);
    await detail.goto();

    await expect(detail.signInPrompt).toBeVisible();
    await expect(detail.amountInput).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Giriş yapın" })).toBeVisible();
  });

  test("alıcı teklif verir, panel onaylar ve fiyat yükselir", async ({ page, context, request }) => {
    const auction = await seedOpenAuction(request, { startingPrice: 1000, minimumBidIncrement: 50 });

    await registerBidder(context.request);

    const detail = new AuctionDetailPage(page, auction.id);
    await detail.goto();
    await detail.waitForLiveConnection();

    await expect(detail.currentPrice).toHaveText(amountPattern(1000));
    expect(await detail.readAmount(), "panel opens on the current floor").toBe(1000);

    const outcome = await detail.bid(1200);

    expect(outcome.kind, outcome.text).toBe("accepted");
    await expect(detail.currentPrice).toHaveText(amountPattern(1200));

    const persisted = await getAuction(request, auction.id);
    expect(persisted.currentPrice).toBe(1200);
    expect(persisted.bidCount).toBe(1);
    expect(persisted.minimumAcceptableBid).toBe(1250);
  });

  test("tabanın altındaki tutar daha tarayıcıdan çıkamaz", async ({ page, context, request }) => {
    const auction = await seedOpenAuction(request, { startingPrice: 1000, minimumBidIncrement: 50 });

    await registerBidder(context.request);

    const detail = new AuctionDetailPage(page, auction.id);
    await detail.goto();

    await detail.setAmount(400);
    await detail.submit();

    // The panel carries the floor as the input's `min`, so the browser refuses the submit
    // outright — the request never leaves and there is nothing for the server to reject.
    await expect(page.locator("#bid-amount:invalid")).toHaveCount(1);
    await expect(detail.feedback).toHaveCount(0);

    const persisted = await getAuction(request, auction.id);
    expect(persisted.bidCount).toBe(0);
    expect(persisted.currentPrice).toBe(1000);
  });

  test("teklif akışı sayfa yenilendikten sonra da dolu kalır", async ({ page, context, request }) => {
    const auction = await seedOpenAuction(request, { startingPrice: 1000, minimumBidIncrement: 50 });

    await registerBidder(context.request);

    const detail = new AuctionDetailPage(page, auction.id);
    await detail.goto();
    await detail.waitForLiveConnection();

    expect((await detail.bid(1200)).kind).toBe("accepted");
    await expect(detail.liveFeedItems).toHaveCount(1);

    // Before the history endpoint existed the feed lived only in memory, so a reload made a
    // busy lot read as though nobody had ever bid on it.
    await page.reload();

    await expect(detail.liveFeedItems).toHaveCount(1);
    await expect(detail.liveFeedItems.first()).toContainText(amountPattern(1200));
    await expect(detail.liveFeedItems.first()).toContainText("sizin");
  });

  test("satıcı kendi parçasına teklif veremez", async ({ page, context, request }) => {
    const seller = await registerSeller(context.request);
    const auction = await createOpenAuction(context.request, {
      title: uniqueTitle(`${seller.displayName} lot`),
    });

    const detail = new AuctionDetailPage(page, auction.id);
    await detail.goto();

    await expect(detail.ownLotNotice).toBeVisible();
    await expect(detail.amountInput).toHaveCount(0);

    // The listing helper is unrelated to the panel, but it proves the lot really is live.
    const persisted = await getAuction(request, auction.id);
    expect(persisted.status).toBe("Active");
  });

  test("henüz açılmamış parçada teklif paneli kapalıdır", async ({ page, context, request }) => {
    await registerSeller(request);
    const auction = await createScheduledAuction(request);

    await registerBidder(context.request);

    const detail = new AuctionDetailPage(page, auction.id);
    await detail.goto();

    await expect(detail.closedNotice).toBeVisible();
    await expect(detail.amountInput).toHaveCount(0);
  });
});
