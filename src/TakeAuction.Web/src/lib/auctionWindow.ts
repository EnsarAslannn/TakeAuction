import type { AuctionStatus } from "@/api/types";

export interface AuctionWindow {
  status: AuctionStatus;
  startsAtUtc: string;
  endsAtUtc: string;
}

export function msRemaining(auction: AuctionWindow, now: number): number {
  return new Date(auction.endsAtUtc).getTime() - now;
}

export function hasOpened(auction: AuctionWindow, now: number): boolean {
  return now >= new Date(auction.startsAtUtc).getTime();
}

export function isBiddable(auction: AuctionWindow, now: number): boolean {
  return (
    auction.status === "Active" && msRemaining(auction, now) > 0 && hasOpened(auction, now)
  );
}
