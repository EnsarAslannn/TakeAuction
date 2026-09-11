import { describe, expect, it } from "vitest";
import { hasOpened, isBiddable, msRemaining } from "./auctionWindow";
import type { AuctionWindow } from "./auctionWindow";
import type { AuctionStatus } from "@/api/types";

const NOON = Date.parse("2026-09-11T12:00:00.000Z");

function lot(status: AuctionStatus, startsOffsetMs: number, endsOffsetMs: number): AuctionWindow {
  return {
    status,
    startsAtUtc: new Date(NOON + startsOffsetMs).toISOString(),
    endsAtUtc: new Date(NOON + endsOffsetMs).toISOString(),
  };
}

const HOUR = 3_600_000;

describe("isBiddable", () => {
  it("opens a running lot", () => {
    expect(isBiddable(lot("Active", -HOUR, HOUR), NOON)).toBe(true);
  });

  it("keeps a lot shut before its start time", () => {
    expect(isBiddable(lot("Active", HOUR, 2 * HOUR), NOON)).toBe(false);
  });

  it("keeps a lot shut once its window has elapsed", () => {
    expect(isBiddable(lot("Active", -2 * HOUR, -HOUR), NOON)).toBe(false);
  });

  it("shuts on the closing instant rather than a moment after", () => {
    expect(isBiddable(lot("Active", -HOUR, 0), NOON)).toBe(false);
  });

  it("opens on the starting instant rather than a moment after", () => {
    expect(isBiddable(lot("Active", 0, HOUR), NOON)).toBe(true);
  });

  it.each<AuctionStatus>(["Scheduled", "Ended", "Cancelled"])(
    "never opens a lot the API reports as %s",
    (status) => {
      expect(isBiddable(lot(status, -HOUR, HOUR), NOON)).toBe(false);
    }
  );

  // The salon hides the bid form on anything that is not biddable, so a lot that had
  // started but was still reported as Scheduled could never take the bid that would have
  // opened it. The API moves the status now; this holds the client to the same reading.
  it("defers to the status the API reports rather than guessing from the clock", () => {
    expect(isBiddable(lot("Scheduled", -HOUR, HOUR), NOON)).toBe(false);
  });
});

describe("msRemaining", () => {
  it("counts down towards the close", () => {
    expect(msRemaining(lot("Active", -HOUR, HOUR), NOON)).toBe(HOUR);
  });

  it("goes negative once the lot is past its close", () => {
    expect(msRemaining(lot("Ended", -2 * HOUR, -HOUR), NOON)).toBe(-HOUR);
  });
});

describe("hasOpened", () => {
  it("is false a millisecond before the start", () => {
    expect(hasOpened(lot("Scheduled", 1, HOUR), NOON)).toBe(false);
  });

  it("is true on the start", () => {
    expect(hasOpened(lot("Active", 0, HOUR), NOON)).toBe(true);
  });
});
