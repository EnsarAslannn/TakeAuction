import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getWatchlist } from "@/api/watchlist";
import { toApiError } from "@/api/client";
import { AuctionCard } from "@/components/AuctionCard";
import { useLobbyChannel } from "@/realtime/useAuctionHub";
import { SplitLine } from "@/motion/Reveal";
import { useT } from "@/i18n";
import type { WatchlistItem } from "@/api/types";

export function Watchlist() {
  const [items, setItems] = useState<WatchlistItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const t = useT();

  useEffect(() => {
    let cancelled = false;

    getWatchlist()
      .then((data) => {
        if (!cancelled) setItems(data);
      })
      .catch((caught) => {
        if (!cancelled) setError(toApiError(caught).message);
      });

    return () => {
      cancelled = true;
    };
  }, [attempt]);

  const retry = () => {
    setError(null);
    setAttempt((value) => value + 1);
  };

  useLobbyChannel({
    onBidPlaced: (notification) => {
      setItems((previous) =>
        previous?.map((item) =>
          item.id === notification.auctionId && notification.amount > item.currentPrice
            ? { ...item, currentPrice: notification.amount, endsAtUtc: notification.endsAtUtc }
            : item
        ) ?? previous
      );
    },
    onStatusChanged: (notification) => {
      setItems((previous) =>
        previous?.map((item) =>
          item.id === notification.auctionId
            ? { ...item, status: notification.status, currentPrice: notification.currentPrice }
            : item
        ) ?? previous
      );
    },
  });

  return (
    <div className="min-h-screen bg-paper pb-32 pt-36 md:pt-44">
      <div className="shell mx-auto max-w-shell">
        <p className="eyebrow">{t("watchlist.eyebrow")}</p>
        <h1 className="mt-6 font-display text-giant font-light leading-[0.9] text-ink">
          <SplitLine text={t("watchlist.title")} />
        </h1>

        {error && (
          <div className="mt-14 border border-ink/15 bg-paper-pure p-8">
            <p className="font-display text-xl font-light text-ink">{t("watchlist.loadFailed")}</p>
            <p className="mt-2 font-sans text-sm text-ink/60">{error}</p>
            <button type="button" onClick={retry} className="btn-ghost mt-6">
              {t("auctions.retry")}
            </button>
          </div>
        )}

        {!items && !error && (
          <div className="mt-14">
            {Array.from({ length: 3 }).map((_, index) => (
              <div key={index} className="border-t border-ink/10 py-10">
                <div className="h-7 w-2/5 animate-pulse rounded-sm bg-ink/6" />
                <div className="mt-3 h-3 w-1/5 animate-pulse rounded-sm bg-ink/4" />
              </div>
            ))}
          </div>
        )}

        {items && items.length === 0 && (
          <div className="mt-20 text-center">
            <p className="font-display text-3xl font-light text-ink">{t("watchlist.emptyTitle")}</p>
            <p className="mx-auto mt-3 max-w-[48ch] font-sans text-sm text-ink/55">{t("watchlist.emptyBody")}</p>
            <Link to="/auctions" className="btn-ghost mt-8 inline-flex">
              {t("watchlist.browse")}
            </Link>
          </div>
        )}

        {items && items.length > 0 && (
          <>
            <div className="mt-14">
              {items.map((auction, index) => (
                <AuctionCard key={auction.id} auction={auction} index={index} />
              ))}
            </div>
            <div className="border-t border-ink/12" />
          </>
        )}
      </div>
    </div>
  );
}
