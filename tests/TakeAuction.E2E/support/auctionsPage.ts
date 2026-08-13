import { type Locator, type Page } from "@playwright/test";

/** The salon listing: the shelf a visitor scans before opening a lot. */
export class AuctionsPage {
  readonly search: Locator;
  readonly emptyState: Locator;

  constructor(readonly page: Page) {
    this.search = page.getByRole("searchbox");
    this.emptyState = page.getByText("Aradığınız parça salonda yok");
  }

  async goto(): Promise<void> {
    await this.page.goto("/auctions");
  }

  /**
   * The listing subscribes to the lobby channel on mount. Waiting for the hub's negotiate
   * response — which every transport performs — keeps "the price moved without a reload" an
   * assertion about SignalR rather than about luck.
   */
  async gotoAndConnect(): Promise<void> {
    const negotiated = this.page.waitForResponse(
      (response) => response.url().includes("/hubs/auctions/negotiate") && response.ok(),
      { timeout: 30_000 }
    );

    await this.goto();
    await negotiated;

    // SubscribeToLobby is the next call the client makes over the connection it just opened.
    await this.page.waitForTimeout(750);
  }

  async filterBy(title: string): Promise<void> {
    await this.search.fill(title);
  }

  card(title: string): Locator {
    return this.page.getByRole("link").filter({ hasText: title });
  }

  priceOn(title: string): Locator {
    return this.card(title)
      .getByText("Güncel", { exact: true })
      .locator("xpath=following-sibling::p[1]");
  }
}
