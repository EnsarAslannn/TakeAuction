import { http } from "./client";
import type { WatchlistItem } from "./types";

export async function getWatchlist(): Promise<WatchlistItem[]> {
  const { data } = await http.get<WatchlistItem[]>("/watchlist");
  return data;
}

export async function watchAuction(auctionId: string): Promise<void> {
  await http.put(`/auctions/${auctionId}/watch`);
}

export async function unwatchAuction(auctionId: string): Promise<void> {
  await http.delete(`/auctions/${auctionId}/watch`);
}
