import { expect, test } from "@playwright/test";
import { newAccount } from "../fixtures/api";

const DEMO_BIDDER = "Demo Bidder";

test.describe("Kimlik akışı", () => {
  test("yeni bir alıcı kaydolur ve doğrudan salona düşer", async ({ page }) => {
    const account = newAccount("Bidder");

    await page.goto("/register");

    await page.locator("#displayName").fill(account.displayName);
    await page.locator("#reg-email").fill(account.email);
    await page.locator("#reg-password").fill(account.password);
    await page.getByRole("button", { name: "Alıcı" }).click();
    await page.getByRole("button", { name: "Hesap açın" }).click();

    await expect(page).toHaveURL(/\/auctions$/);
    await expect(page.getByText(account.displayName).first()).toBeVisible();
  });

  test("satıcı olarak kaydolan kullanıcı ilan açma yetkisini görür", async ({ page }) => {
    const account = newAccount("Seller");

    await page.goto("/register");

    await page.locator("#displayName").fill(account.displayName);
    await page.locator("#reg-email").fill(account.email);
    await page.locator("#reg-password").fill(account.password);
    await page.getByRole("button", { name: "Satıcı" }).click();
    await page.getByRole("button", { name: "Hesap açın" }).click();

    await expect(page).toHaveURL(/\/auctions$/);
    await expect(page.getByRole("link", { name: "İlan açın" }).first()).toBeVisible();
  });

  test("sunucu tarafı parola kuralı formda alan hatası olarak görünür", async ({ page }) => {
    const account = newAccount("Bidder");

    await page.goto("/register");

    await page.locator("#displayName").fill(account.displayName);
    await page.locator("#reg-email").fill(account.email);
    await page.locator("#reg-password").fill("kisa");
    await page.getByRole("button", { name: "Hesap açın" }).click();

    await expect(page).toHaveURL(/\/register$/);
    await expect(page.getByText("One or more validation errors occurred.")).toBeVisible();
    await expect(page.getByText(/'Password'/).first()).toBeVisible();
  });

  test("oturum çerezi sayfa yenilendikten sonra da geçerlidir", async ({ page, context }) => {
    const account = newAccount("Bidder");

    await page.goto("/register");
    await page.locator("#displayName").fill(account.displayName);
    await page.locator("#reg-email").fill(account.email);
    await page.locator("#reg-password").fill(account.password);
    await page.getByRole("button", { name: "Hesap açın" }).click();
    await expect(page).toHaveURL(/\/auctions$/);

    await page.reload();
    await expect(page.getByText(account.displayName).first()).toBeVisible();

    const cookies = await context.cookies();
    const accessToken = cookies.find((cookie) => cookie.name === "takeauction_access_token");
    const csrf = cookies.find((cookie) => cookie.name === "takeauction_csrf");

    expect(accessToken?.httpOnly, "the access token must stay out of reach of scripts").toBe(true);
    expect(csrf?.httpOnly, "the double-submit token has to be readable by the client").toBe(false);
  });

  test("demo hesabı tek tıkla giriş yapar", async ({ page }) => {
    await page.goto("/login");

    await page.getByRole("button", { name: "Alıcı" }).click();
    await page.getByRole("button", { name: "Giriş yapın" }).click();

    await expect(page).toHaveURL(/\/auctions$/);
    await expect(page.getByText(DEMO_BIDDER).first()).toBeVisible();
  });

  test("yanlış parola girişi salona sokmaz", async ({ page }) => {
    await page.goto("/login");

    await page.locator("#email").fill("bidder@takeauction.local");
    await page.locator("#password").fill("Definitely!Wrong9");
    await page.getByRole("button", { name: "Giriş yapın" }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByText("The email or password is incorrect.")).toBeVisible();
  });

  test("çıkış oturumu kapatır ve ziyaretçi görünümüne döner", async ({ page }) => {
    const account = newAccount("Bidder");

    await page.goto("/register");
    await page.locator("#displayName").fill(account.displayName);
    await page.locator("#reg-email").fill(account.email);
    await page.locator("#reg-password").fill(account.password);
    await page.getByRole("button", { name: "Hesap açın" }).click();
    await expect(page).toHaveURL(/\/auctions$/);

    await page.getByRole("button", { name: "Çıkış" }).first().click();

    await expect(page.getByRole("link", { name: "Kaydolun" }).first()).toBeVisible();
    await expect(page.getByText(account.displayName)).toHaveCount(0);
  });
});
