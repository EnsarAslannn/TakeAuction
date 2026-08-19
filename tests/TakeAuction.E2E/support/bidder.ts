import { type Browser, type BrowserContext, type Page } from "@playwright/test";
import { registerBidder, type CreatedAuction } from "../fixtures/api";
import { AuctionDetailPage } from "./auctionDetailPage";

export interface Bidder {
  context: BrowserContext;
  page: Page;
  panel: AuctionDetailPage;
}

export async function openBidder(browser: Browser, auction: CreatedAuction): Promise<Bidder> {
  const context = await browser.newContext();
  await registerBidder(context.request);

  const page = await context.newPage();
  const panel = new AuctionDetailPage(page, auction.id);

  await panel.goto();
  await panel.waitForLiveConnection();

  return { context, page, panel };
}
