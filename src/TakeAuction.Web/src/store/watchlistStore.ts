import { create } from "zustand";
import * as watchlistApi from "@/api/watchlist";
import { toApiError } from "@/api/client";

interface WatchlistState {
  ids: ReadonlySet<string>;
  pending: ReadonlySet<string>;
  status: "idle" | "loading" | "ready" | "error";
  load: () => Promise<void>;
  toggle: (auctionId: string) => Promise<void>;
  reset: () => void;
}

const without = (set: ReadonlySet<string>, id: string) => {
  const next = new Set(set);
  next.delete(id);
  return next;
};

const including = (set: ReadonlySet<string>, id: string) => new Set(set).add(id);

export const useWatchlistStore = create<WatchlistState>((set, get) => ({
  ids: new Set(),
  pending: new Set(),
  status: "idle",

  load: async () => {
    set({ status: "loading" });
    try {
      const items = await watchlistApi.getWatchlist();
      set({ ids: new Set(items.map((item) => item.id)), status: "ready" });
    } catch {
      set({ status: "error" });
    }
  },

  toggle: async (auctionId) => {
    if (get().pending.has(auctionId)) return;

    const watching = get().ids.has(auctionId);

    set((state) => ({
      ids: watching ? without(state.ids, auctionId) : including(state.ids, auctionId),
      pending: including(state.pending, auctionId),
    }));

    try {
      if (watching) {
        await watchlistApi.unwatchAuction(auctionId);
      } else {
        await watchlistApi.watchAuction(auctionId);
      }
    } catch (error) {
      set((state) => ({
        ids: watching ? including(state.ids, auctionId) : without(state.ids, auctionId),
      }));
      throw toApiError(error);
    } finally {
      set((state) => ({ pending: without(state.pending, auctionId) }));
    }
  },

  reset: () => set({ ids: new Set(), pending: new Set(), status: "idle" }),
}));
