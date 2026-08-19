import { expect, type Locator, type Page } from "@playwright/test";

export type BidOutcomeKind = "leading" | "answered" | "outbid" | "error";

export interface BidOutcome {
  kind: BidOutcomeKind;
  text: string;
}

export class AuctionDetailPage {
  readonly amountInput: Locator;
  readonly bidForm: Locator;
  readonly submitButton: Locator;
  readonly feedback: Locator;
  readonly currentPrice: Locator;
  readonly bidCounter: Locator;
  readonly liveFeedItems: Locator;
  readonly liveIndicator: Locator;
  readonly signInPrompt: Locator;
  readonly ownLotNotice: Locator;
  readonly closedNotice: Locator;

  constructor(
    readonly page: Page,
    readonly auctionId: string
  ) {
    this.amountInput = page.locator("#bid-amount");
    this.bidForm = page.locator("form").filter({ has: this.amountInput });
    this.submitButton = this.bidForm.getByRole("button", {
      name: /Sınırınızı gönderin|Gönderiliyor/,
    });
    this.feedback = this.bidForm.getByTestId("bid-feedback");

    this.currentPrice = page
      .getByText("Güncel teklif", { exact: true })
      .locator("xpath=following-sibling::p[1]");
    this.bidCounter = page.getByText(/\d+ teklif verildi|Henüz teklif yok/).first();
    this.liveFeedItems = page
      .getByText("Canlı teklif akışı", { exact: true })
      .locator("xpath=following-sibling::ul/li");
    this.liveIndicator = page.getByText("Salon canlı", { exact: true });

    this.signInPrompt = page.getByText("Teklif vermek için", { exact: true });
    this.ownLotNotice = page.getByText("Bu parça sizin", { exact: true });
    this.closedNotice = page.getByText("Teklif kapalı", { exact: true });
  }

  async goto(): Promise<void> {
    await this.page.goto(`/auctions/${this.auctionId}`);
  }

  async waitForLiveConnection(): Promise<void> {
    await expect(this.liveIndicator).toBeVisible();
    await this.page.waitForTimeout(750);
  }

  async setAmount(amount: number): Promise<void> {
    await this.amountInput.fill(amount.toFixed(2));
  }

  async submit(): Promise<void> {
    await this.submitButton.click();
  }

  async bid(amount: number): Promise<BidOutcome> {
    await this.setAmount(amount);
    await this.submit();

    return this.outcome();
  }

  async outcome(): Promise<BidOutcome> {
    await expect(this.feedback).toBeVisible();

    const text = (await this.feedback.innerText()).trim();

    if (/Öndesiniz/.test(text)) {
      return { kind: "leading", text };
    }

    if (/sınırınız yetmedi/.test(text)) {
      return { kind: "answered", text };
    }

    if (/öne geçti/.test(text)) {
      return { kind: "outbid", text };
    }

    return { kind: "error", text };
  }

  async readAmount(): Promise<number> {
    return Number(await this.amountInput.inputValue());
  }
}
