import { beforeEach, describe, expect, it, vi } from "vitest";
import * as watchlistApi from "@/api/watchlist";
import { useWatchlistStore } from "./watchlistStore";

beforeEach(() => useWatchlistStore.getState().reset());

describe("toggling a lot", () => {
  it("watches a lot that was not on the list", async () => {
    const watch = vi.spyOn(watchlistApi, "watchAuction").mockResolvedValue();

    await useWatchlistStore.getState().toggle("lot-1");

    expect(watch).toHaveBeenCalledWith("lot-1");
    expect(useWatchlistStore.getState().ids.has("lot-1")).toBe(true);
  });

  it("unwatches a lot that was on the list", async () => {
    vi.spyOn(watchlistApi, "watchAuction").mockResolvedValue();
    const unwatch = vi.spyOn(watchlistApi, "unwatchAuction").mockResolvedValue();

    await useWatchlistStore.getState().toggle("lot-1");
    await useWatchlistStore.getState().toggle("lot-1");

    expect(unwatch).toHaveBeenCalledWith("lot-1");
    expect(useWatchlistStore.getState().ids.has("lot-1")).toBe(false);
  });

  it("shows the change before the server answers", async () => {
    let answer!: () => void;
    vi.spyOn(watchlistApi, "watchAuction").mockReturnValue(new Promise<void>((resolve) => (answer = resolve)));

    const pending = useWatchlistStore.getState().toggle("lot-1");

    expect(useWatchlistStore.getState().ids.has("lot-1")).toBe(true);
    expect(useWatchlistStore.getState().pending.has("lot-1")).toBe(true);

    answer();
    await pending;

    expect(useWatchlistStore.getState().pending.has("lot-1")).toBe(false);
  });

  it("puts the list back when the server refuses", async () => {
    vi.spyOn(watchlistApi, "watchAuction").mockRejectedValue(new Error("the lot closed"));

    await expect(useWatchlistStore.getState().toggle("lot-1")).rejects.toThrow();

    expect(useWatchlistStore.getState().ids.has("lot-1")).toBe(false);
    expect(useWatchlistStore.getState().pending.has("lot-1")).toBe(false);
  });

  it("ignores a second click while the first is still in flight", async () => {
    let answer!: () => void;
    const watch = vi
      .spyOn(watchlistApi, "watchAuction")
      .mockReturnValue(new Promise<void>((resolve) => (answer = resolve)));

    const first = useWatchlistStore.getState().toggle("lot-1");
    await useWatchlistStore.getState().toggle("lot-1");
    answer();
    await first;

    expect(watch).toHaveBeenCalledOnce();
    expect(useWatchlistStore.getState().ids.has("lot-1")).toBe(true);
  });
});
