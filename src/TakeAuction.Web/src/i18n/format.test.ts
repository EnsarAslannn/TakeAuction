import { describe, expect, it } from "vitest";
import { formattersFor } from "./format";
import { translateIn } from "./translate";
import { en } from "./en";
import { tr } from "./tr";
import type { TranslationKey } from "./tr";

describe("money", () => {
  // Lots are priced in lira whoever is reading, so the sign has to survive the English
  // locale rather than turning into a three-letter code.
  it.each(["tr", "en"] as const)("keeps the lira sign in %s", (language) => {
    const formatted = formattersFor(language).money(8500);

    expect(formatted).toContain("₺");
    expect(formatted).not.toContain("TRY");
  });

  it("rounds the headline price to whole lira", () => {
    expect(formattersFor("tr").money(8500.49)).not.toContain(",49");
  });

  it("keeps both decimals where the exact figure matters", () => {
    expect(formattersFor("en").moneyPrecise(8500.5)).toContain(".50");
  });
});

describe("countdown", () => {
  it("floors at zero rather than counting past the close", () => {
    expect(formattersFor("tr").countdown(-5_000)).toBe("00:00:00");
    expect(formattersFor("tr").countdown(0)).toBe("00:00:00");
  });

  it("pads every field so the clock does not jitter", () => {
    expect(formattersFor("tr").countdown(3_661_000)).toBe("01:01:01");
  });

  it("adds a day count only once a day is left", () => {
    const format = formattersFor("en");

    expect(format.countdown(86_400_000 + 3_600_000)).toMatch(/^1\S*\s01:00:00$/);
    expect(format.countdown(3_600_000)).toBe("01:00:00");
  });
});

describe("status", () => {
  it.each(["Scheduled", "Active", "Ended", "Cancelled"])(
    "renders %s in the reader's language",
    (status) => {
      const key = `status.${status}` as TranslationKey;

      expect(formattersFor("tr").status(status)).toBe(translateIn("tr", key));
      expect(formattersFor("en").status(status)).toBe(translateIn("en", key));
    }
  );

  it("passes an unknown status through untouched rather than blanking the badge", () => {
    expect(formattersFor("tr").status("Rescinded")).toBe("Rescinded");
  });
});

describe("the dictionaries", () => {
  it("carry the same keys, so no reader meets a blank string", () => {
    expect(Object.keys(en).sort()).toEqual(Object.keys(tr).sort());
  });

  it("leave no entry empty", () => {
    for (const [key, value] of Object.entries({ ...tr, ...en })) {
      expect(value, `${key} is empty`).not.toBe("");
    }
  });
});

describe("translateIn", () => {
  it("fills a named placeholder", () => {
    expect(translateIn("en", "bid.atLeast", { amount: "₺100" })).toContain("₺100");
  });

  it("leaves an unknown placeholder in place rather than printing undefined", () => {
    const filled = translateIn("en", "bid.atLeast", {});

    expect(filled).not.toContain("undefined");
  });
});
