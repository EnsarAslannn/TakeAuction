import { expect, test } from "@playwright/test";
import { getAuction, seedOpenAuction, uniqueTitle } from "../fixtures/api";
import { openBidder } from "../support/bidder";
import { amountPattern } from "../support/money";

const ACCESS_COOKIE = "takeauction_access_token";
const REFRESH_COOKIE = "takeauction_refresh_token";
const STARTING_PRICE = 1000;

/**
 * Dropping the access cookie is how an expired token looks to the browser: the long-lived
 * refresh cookie survives and the very next call comes back 401. Waiting out a real fifteen
 * minute lifetime would test the same code path and cost fifteen minutes.
 */
test.describe("Oturum sessiz yenileme", () => {
  test("erişim çerezi düştüğünde teklif sessizce yenilenip tamamlanır", async ({ browser, request }) => {
    const auction = await seedOpenAuction(request, {
      title: uniqueTitle("Sessiz yenileme"),
      startingPrice: STARTING_PRICE,
      minimumBidIncrement: 50,
    });

    const bidder = await openBidder(browser, auction);

    try {
      await bidder.context.clearCookies({ name: ACCESS_COOKIE });

      const refreshCall = bidder.page.waitForResponse(
        (response) =>
          response.url().includes("/auth/refresh") && response.request().method() === "POST"
      );

      const outcome = await bidder.panel.bid(STARTING_PRICE);

      const refreshResponse = await refreshCall;
      expect(refreshResponse.status(), "the client should have refreshed on its own").toBe(200);

      // The user never saw a login screen: the bid they clicked is the bid that landed.
      expect(outcome.kind, outcome.text).toBe("accepted");
      await expect(bidder.panel.currentPrice).toHaveText(amountPattern(STARTING_PRICE));

      const persisted = await getAuction(request, auction.id);
      expect(persisted.bidCount).toBe(1);
      expect(persisted.currentPrice).toBe(STARTING_PRICE);

      const cookies = await bidder.context.cookies();
      expect(cookies.find((cookie) => cookie.name === ACCESS_COOKIE)).toBeDefined();
    } finally {
      await bidder.context.close();
    }
  });

  test("yenileme çerezi de yoksa kullanıcı oturumsuz görünüme düşer", async ({ browser, request }) => {
    const auction = await seedOpenAuction(request, { title: uniqueTitle("Yenilenemez oturum") });
    const bidder = await openBidder(browser, auction);

    try {
      await bidder.context.clearCookies({ name: ACCESS_COOKIE });
      await bidder.context.clearCookies({ name: REFRESH_COOKIE });

      await bidder.panel.setAmount(STARTING_PRICE);
      await bidder.panel.submit();

      // Nothing left to refresh with, so the panel falls back to the signed-out invitation
      // instead of leaving the bidder staring at a form that can never succeed.
      await expect(bidder.panel.signInPrompt).toBeVisible();

      const persisted = await getAuction(request, auction.id);
      expect(persisted.bidCount).toBe(0);
    } finally {
      await bidder.context.close();
    }
  });

  test("çıkış yapıldığında yenileme çerezi de tarayıcıdan silinir", async ({ browser, request }) => {
    const auction = await seedOpenAuction(request, { title: uniqueTitle("Çıkış temizliği") });
    const bidder = await openBidder(browser, auction);

    try {
      const before = await bidder.context.cookies();
      expect(before.find((cookie) => cookie.name === REFRESH_COOKIE)).toBeDefined();

      await bidder.page.goto("/auctions");
      await bidder.page.getByRole("button", { name: "Çıkış" }).first().click();

      await expect(bidder.page.getByRole("link", { name: "Kaydolun" }).first()).toBeVisible();

      const after = await bidder.context.cookies();
      expect(after.find((cookie) => cookie.name === REFRESH_COOKIE)).toBeUndefined();
      expect(after.find((cookie) => cookie.name === ACCESS_COOKIE)).toBeUndefined();
    } finally {
      await bidder.context.close();
    }
  });
});
