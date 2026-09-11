import { expect, test, type Page } from "@playwright/test";
import { registerBidder, seedOpenAuction, uniqueTitle } from "../fixtures/api";
import { AuctionDetailPage } from "../support/auctionDetailPage";

const watchToggle = (page: Page) =>
  page.getByRole("button", { name: /Takibe alın|Takip ediyorsunuz|Takibi bırakın/ });

test.describe("Takip listesi ve bildirimler", () => {
  test("alıcı bir parçayı takibe alır, listesinde görür ve bırakır", async ({ page, context, request }) => {
    const title = uniqueTitle("Takip turu");
    const auction = await seedOpenAuction(request, { title });

    await registerBidder(context.request);

    const detail = new AuctionDetailPage(page, auction.id);
    await detail.goto();

    await expect(watchToggle(page)).toHaveAttribute("aria-pressed", "false");
    await watchToggle(page).click();
    await expect(watchToggle(page)).toHaveAttribute("aria-pressed", "true");

    await page.goto("/watchlist");
    await expect(page.getByRole("link", { name: new RegExp(title) })).toBeVisible();

    await detail.goto();
    await expect(watchToggle(page)).toHaveAttribute("aria-pressed", "true");
    await watchToggle(page).click();
    await expect(watchToggle(page)).toHaveAttribute("aria-pressed", "false");

    await page.goto("/watchlist");
    await expect(page.getByText("Henüz bir parça takip etmiyorsunuz")).toBeVisible();
  });

  test("takip, sayfa yenilendikten sonra da hatırlanır", async ({ page, context, request }) => {
    const auction = await seedOpenAuction(request);

    await registerBidder(context.request);

    const detail = new AuctionDetailPage(page, auction.id);
    await detail.goto();
    await watchToggle(page).click();
    await expect(watchToggle(page)).toHaveAttribute("aria-pressed", "true");

    await page.reload();

    await expect(watchToggle(page)).toHaveAttribute("aria-pressed", "true");
  });

  test("giriş yapmamış ziyaretçiye takip için giriş daveti gösterilir", async ({ page, request }) => {
    const auction = await seedOpenAuction(request);

    await new AuctionDetailPage(page, auction.id).goto();

    await expect(page.getByRole("link", { name: "Takip etmek için giriş yapın" })).toBeVisible();
    await expect(watchToggle(page)).toHaveCount(0);
  });

  test("takip listesi oturum açmamış ziyaretçiyi girişe yollar", async ({ page }) => {
    await page.goto("/watchlist");

    await expect(page).toHaveURL(/\/login$/);
  });

  test("bildirim zili boş kutuyu anlatır", async ({ page, context }) => {
    await registerBidder(context.request);

    await page.goto("/auctions");
    await page.getByRole("button", { name: "Bildirimler" }).first().click();

    const panel = page.getByRole("dialog", { name: "Bildirimler" });
    await expect(panel).toBeVisible();
    await expect(panel).toContainText("Henüz bildiriminiz yok");

    await page.keyboard.press("Escape");
    await expect(panel).toHaveCount(0);
  });
});
