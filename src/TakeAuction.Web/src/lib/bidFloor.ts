import type { AuctionDetail, BidPlacedNotification, BidStanding, PlaceBidResponse } from "@/api/types";

export interface Standing {
  isLeading: boolean;
  maxAmount: number | null;
}

export const NOT_LEADING: Standing = { isLeading: false, maxAmount: null };

type Lot = Pick<AuctionDetail, "bidCount" | "currentPrice" | "minimumAcceptableBid" | "minimumBidIncrement">;

const cents = (value: number) => Math.round(value * 100) / 100;

export function publicFloor(currentPrice: number, increment: number): number {
  return cents(currentPrice + increment);
}

export function minimumBidFor(lot: Lot, standing: Standing | null): number {
  if (standing?.isLeading && standing.maxAmount !== null) {
    return cents(standing.maxAmount + lot.minimumBidIncrement);
  }

  return lot.minimumAcceptableBid;
}

export function standingFrom(source: BidStanding | PlaceBidResponse): Standing {
  return source.isLeading ? { isLeading: true, maxAmount: source.maxAmount } : NOT_LEADING;
}

export function standingAfterBid(
  standing: Standing | null,
  lot: Lot,
  notification: Pick<BidPlacedNotification, "bidderId" | "amount">,
  userId: string | undefined
): Standing | null {
  if (!standing?.isLeading || !userId || notification.bidderId === userId) {
    return standing;
  }

  if (lot.bidCount > 0 && notification.amount <= lot.currentPrice) {
    return standing;
  }

  return NOT_LEADING;
}
