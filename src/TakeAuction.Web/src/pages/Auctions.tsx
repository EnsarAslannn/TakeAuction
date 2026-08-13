import { useCallback, useEffect, useState } from "react";
import { getAuctions } from "@/api/auctions";
import { toApiError } from "@/api/client";
import { AuctionCard } from "@/components/AuctionCard";
import { useLobbyChannel } from "@/realtime/useAuctionHub";
import { SplitLine } from "@/motion/Reveal";
import type { AuctionListItem, AuctionStatus, PagedResult } from "@/api/types";

const FILTERS: { label: string; value: AuctionStatus | "All" }[] = [
  { label: "Tümü", value: "All" },
  { label: "Canlı", value: "Active" },
  { label: "Planlandı", value: "Scheduled" },
  { label: "Sona erdi", value: "Ended" },
];

const PAGE_SIZE = 12;

export function Auctions() {
  const [result, setResult] = useState<PagedResult<AuctionListItem> | null>(null);
  const [status, setStatus] = useState<AuctionStatus | "All">("All");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAuctions({
        page,
        pageSize: PAGE_SIZE,
        status: status === "All" ? undefined : status,
        search: search.trim() || undefined,
      });
      setResult(data);
    } catch (caught) {
      setError(toApiError(caught).message);
      setResult(null);
    } finally {
      setLoading(false);
    }
  }, [page, status, search]);

  useEffect(() => {
    const timer = window.setTimeout(load, search ? 350 : 0);
    return () => window.clearTimeout(timer);
  }, [load, search]);

  useLobbyChannel({
    onBidPlaced: (notification) => {
      setResult((previous) =>
        previous
          ? {
              ...previous,
              items: previous.items.map((item) =>
                // Broadcasts from simultaneous bids can arrive out of order, and a live
                // auction's price only ever climbs.
                item.id === notification.auctionId && notification.amount > item.currentPrice
                  ? { ...item, currentPrice: notification.amount }
                  : item
              ),
            }
          : previous
      );
    },
    onStatusChanged: (notification) => {
      setResult((previous) =>
        previous
          ? {
              ...previous,
              items: previous.items.map((item) =>
                item.id === notification.auctionId
                  ? { ...item, status: notification.status, currentPrice: notification.currentPrice }
                  : item
              ),
            }
          : previous
      );
    },
  });

  return (
    <div className="min-h-screen bg-paper pb-32 pt-36 md:pt-44">
      <div className="shell mx-auto max-w-shell">
        <p className="eyebrow">Salon</p>
        <h1 className="mt-6 font-display text-giant font-light leading-[0.9] text-ink">
          <SplitLine text="açık artırmalar" />
        </h1>

        <div className="mt-14 flex flex-col gap-6 border-b border-ink/12 pb-6 md:flex-row md:items-end md:justify-between">
          <div className="flex flex-wrap gap-2">
            {FILTERS.map((filter) => (
              <button
                key={filter.value}
                type="button"
                onClick={() => {
                  setStatus(filter.value);
                  setPage(1);
                }}
                className={`rounded-full px-5 py-2 font-mono text-eyebrow uppercase transition-all duration-500 ease-editorial ${
                  status === filter.value
                    ? "bg-ink text-paper"
                    : "border border-ink/15 text-stone hover:border-ink/40 hover:text-ink"
                }`}
              >
                {filter.label}
              </button>
            ))}
          </div>

          <div className="w-full md:max-w-xs">
            <input
              type="search"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
              placeholder="Parça arayın…"
              className="field"
            />
          </div>
        </div>

        {error && (
          <div className="mt-10 border border-ink/15 bg-paper-pure p-8">
            <p className="font-display text-xl font-light text-ink">Liste yüklenemedi</p>
            <p className="mt-2 font-sans text-sm text-ink/60">{error}</p>
            <button type="button" onClick={load} className="btn-ghost mt-6">
              Tekrar deneyin
            </button>
          </div>
        )}

        {loading && !result && (
          <div className="mt-4">
            {Array.from({ length: 5 }).map((_, index) => (
              <div key={index} className="border-t border-ink/10 py-10">
                <div className="h-7 w-2/5 animate-pulse rounded bg-ink/[0.06]" />
                <div className="mt-3 h-3 w-1/5 animate-pulse rounded bg-ink/[0.04]" />
              </div>
            ))}
          </div>
        )}

        {result && result.items.length === 0 && !loading && (
          <div className="mt-20 text-center">
            <p className="font-display text-3xl font-light text-ink">Aradığınız parça salonda yok</p>
            <p className="mt-3 font-sans text-sm text-ink/55">
              Filtreyi değiştirin ya da aramayı temizleyin.
            </p>
          </div>
        )}

        {result && result.items.length > 0 && (
          <>
            <div className="mt-4">
              {result.items.map((auction, index) => (
                <AuctionCard
                  key={auction.id}
                  auction={auction}
                  index={(result.page - 1) * result.pageSize + index}
                />
              ))}
            </div>
            <div className="border-t border-ink/12" />

            {result.totalPages > 1 && (
              <div className="mt-12 flex items-center justify-between">
                <button
                  type="button"
                  disabled={result.page <= 1}
                  onClick={() => setPage((value) => Math.max(1, value - 1))}
                  className="btn-ghost"
                >
                  ← Önceki
                </button>
                <span className="font-mono text-eyebrow uppercase tabular-nums text-stone">
                  {result.page} / {result.totalPages}
                </span>
                <button
                  type="button"
                  disabled={!result.hasNextPage}
                  onClick={() => setPage((value) => value + 1)}
                  className="btn-ghost"
                >
                  Sonraki →
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
