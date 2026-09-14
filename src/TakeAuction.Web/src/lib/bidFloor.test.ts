import { describe, expect, it } from "vitest";
import { NOT_LEADING, minimumBidFor, publicFloor, standingAfterBid, standingFrom } from "./bidFloor";
import type { Standing } from "./bidFloor";
import type { PlaceBidResponse } from "@/api/types";

const ME = "bidder-me";
const RIVAL = "bidder-rival";

const lot = {
  bidCount: 3,
  currentPrice: 178650,
  minimumBidIncrement: 1000,
  minimumAcceptableBid: 179650,
};

const leading = (maxAmount: number): Standing => ({ isLeading: true, maxAmount });

describe("minimumBidFor", () => {
  it("asks a bidder who is not in front for the public floor", () => {
    expect(minimumBidFor(lot, NOT_LEADING)).toBe(179650);
  });

  it("asks a signed-out visitor for the public floor", () => {
    expect(minimumBidFor(lot, null)).toBe(179650);
  });

  it("asks the leader to clear their own ceiling rather than the price", () => {
    expect(minimumBidFor(lot, leading(200000))).toBe(201000);
  });

  it("keeps the leader's floor on whole cents so the input's min never rejects it", () => {
    const pennyLot = { ...lot, minimumBidIncrement: 0.2 };

    expect(minimumBidFor(pennyLot, leading(0.1))).toBe(0.3);
  });
});

describe("publicFloor", () => {
  it("adds the increment on whole cents", () => {
    expect(publicFloor(0.1, 0.2)).toBe(0.3);
  });
});

describe("standingFrom", () => {
  const receipt = (isLeading: boolean): PlaceBidResponse => ({
    bidId: "b-1",
    auctionId: "a-1",
    amount: 178650,
    currentPrice: 178650,
    maxAmount: 200000,
    minimumNextBid: 201000,
    bidCount: 4,
    isLeading,
    answeredByProxy: !isLeading,
    placedAtUtc: "2026-09-14T10:00:00Z",
    endsAtUtc: "2026-09-15T10:00:00Z",
    auctionExtended: false,
  });

  it("keeps the ceiling of a bid that took the lead", () => {
    expect(standingFrom(receipt(true))).toEqual(leading(200000));
  });

  it("forgets the ceiling of a bid the leader's proxy answered", () => {
    expect(standingFrom(receipt(false))).toEqual(NOT_LEADING);
  });
});

describe("standingAfterBid", () => {
  it("drops the leader back once a rival sets a higher price", () => {
    const next = standingAfterBid(leading(200000), lot, { bidderId: RIVAL, amount: 201000 }, ME);

    expect(next).toEqual(NOT_LEADING);
  });

  it("keeps the lead when the price was set on the leader's behalf", () => {
    const standing = leading(200000);

    expect(standingAfterBid(standing, lot, { bidderId: ME, amount: 180650 }, ME)).toBe(standing);
  });

  it("ignores a late broadcast that does not move the price", () => {
    const standing = leading(200000);

    expect(standingAfterBid(standing, lot, { bidderId: RIVAL, amount: 177650 }, ME)).toBe(standing);
  });

  it("leaves a bidder who is not in front where they are", () => {
    expect(standingAfterBid(NOT_LEADING, lot, { bidderId: RIVAL, amount: 181000 }, ME)).toBe(NOT_LEADING);
  });

  it("has nothing to track for a signed-out visitor", () => {
    expect(standingAfterBid(null, lot, { bidderId: RIVAL, amount: 181000 }, undefined)).toBeNull();
  });
});
