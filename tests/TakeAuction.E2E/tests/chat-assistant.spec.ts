import { expect, test } from "@playwright/test";

test.describe("TakeAuction asistanı", () => {
  test.beforeEach(async ({ page }) => {
    await page.route("**/api/v1/auth/**", (route) => route.fulfill({ status: 401 }));
    await page.route("**/hubs/**", (route) => route.abort());
    await page.route("**/api/v1/auctions**", (route) => route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
        hasNextPage: false,
      }),
    }));
    await page.route("**/api/v1/chat", async (route) => {
      const payload = route.request().postDataJSON() as { language: "tr" | "en" };
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          answer: payload.language === "tr"
            ? "Bir lotta teklif vermek için oturum açın ve lot sayfasındaki teklif alanını kullanın."
            : "Sign in and use the bidding field on the lot page.",
          sources: [{ title: payload.language === "tr" ? "Nasıl çalışır" : "How it works", url: "/#how-it-works" }],
          suggestions: payload.language === "tr"
            ? ["Minimum artış nedir?", "Limitim görünür mü?"]
            : ["What is the minimum increment?", "Can others see my limit?"],
          usedAi: false,
        }),
      });
    });
  });

  test("masaüstünde soru, kaynak, yeni sohbet ve kalıcı geçmiş akışı çalışır", async ({ page }) => {
    await page.goto("/");
    await page.getByRole("button", { name: "TakeAuction asistanını aç" }).click();

    const panel = page.getByRole("dialog", { name: "TakeAuction asistanı" });
    await expect(panel).toBeVisible();
    await panel.getByRole("textbox", { name: "Sorunuz" }).fill("Teklif nasıl verilir?");
    await panel.getByRole("button", { name: "Gönder" }).click();

    await expect(panel.getByText(/teklif/i).last()).toBeVisible();
    await expect(panel.getByRole("link", { name: "Nasıl çalışır" })).toBeVisible();
    await page.reload();
    await page.getByRole("button", { name: "TakeAuction asistanını aç" }).click();
    await expect(page.getByRole("dialog", { name: "TakeAuction asistanı" }).getByText("Teklif nasıl verilir?")).toBeVisible();
  });

  test("mobil panel ekrana sığar ve klavyeyle kapanır", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/");
    const launcher = page.getByRole("button", { name: "TakeAuction asistanını aç" });
    const launcherBox = await launcher.boundingBox();
    expect(launcherBox?.width).toBeGreaterThanOrEqual(128);
    expect(launcherBox?.height).toBeGreaterThanOrEqual(64);
    await launcher.click();

    const panel = page.getByRole("dialog", { name: "TakeAuction asistanı" });
    await expect(panel).toBeVisible();
    const box = await panel.boundingBox();
    expect(box?.width).toBeLessThanOrEqual(390);
    expect(box?.height).toBeLessThanOrEqual(844);

    await page.keyboard.press("Escape");
    await expect(panel).toBeHidden();
    await expect(launcher).toBeFocused();
  });
});
