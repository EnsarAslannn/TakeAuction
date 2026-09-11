import { describe, expect, it } from "vitest";
import { formattersFor, translateIn } from "@/i18n";
import type { TranslationKey, TranslationParams } from "@/i18n";
import type { NotificationKind, UserNotification } from "@/api/types";
import { describeNotification } from "./notificationCopy";

const KINDS: NotificationKind[] = ["AuctionWon", "AuctionSold", "AuctionUnsold", "AuctionClosingSoon"];

function notification(kind: NotificationKind, amount: number | null = 12500): UserNotification {
  return {
    id: "n-1",
    kind,
    auctionId: "a-1",
    auctionTitle: "Chesterfield",
    amount,
    auctionEndsAtUtc: "2026-09-11T12:00:00Z",
    createdAtUtc: "2026-09-11T11:55:00Z",
    readAtUtc: null,
  };
}

const describeIn = (language: "tr" | "en", item: UserNotification) =>
  describeNotification(
    item,
    (key: TranslationKey, params?: TranslationParams) => translateIn(language, key, params),
    formattersFor(language)
  );

describe("describing a notification", () => {
  it.each(KINDS.flatMap((kind) => [["tr", kind] as const, ["en", kind] as const]))(
    "leaves no placeholder behind (%s, %s)",
    (language, kind) => {
      const copy = describeIn(language, notification(kind, kind === "AuctionSold" || kind === "AuctionWon" ? 12500 : null));

      expect(copy.title).not.toMatch(/\{\w+\}/);
      expect(copy.body).not.toMatch(/\{\w+\}/);
      expect(copy.body).toContain("Chesterfield");
    }
  );

  it("names the price a lot went for", () => {
    expect(describeIn("en", notification("AuctionWon")).body).toContain("₺");
    expect(describeIn("tr", notification("AuctionSold")).body).toContain("₺");
  });

  it("names the time a watched lot closes", () => {
    const body = describeIn("en", notification("AuctionClosingSoon", null)).body;

    expect(body).toContain(formattersFor("en").clock("2026-09-11T12:00:00Z"));
    expect(body).not.toContain(formattersFor("en").time("2026-09-11T12:00:00Z"));
  });

  it("gives each kind its own heading", () => {
    const titles = new Set(KINDS.map((kind) => describeIn("tr", notification(kind)).title));

    expect(titles.size).toBe(KINDS.length);
  });
});
