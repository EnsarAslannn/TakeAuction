import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getAuctionBids, getAuctionById } from "@/api/auctions";
import { toApiError } from "@/api/client";
import { useAuthStore } from "@/store/authStore";
import { AuctionStage } from "@/components/AuctionStage";
import { BidPanel } from "@/components/BidPanel";
import { showcaseForAuction } from "@/content/catalog";
import { useAuctionChannel, useConnectionState } from "@/realtime/useAuctionHub";
import { useFormat, useT } from "@/i18n";
import { useNow, usePrefersReducedMotion } from "@/lib/hooks";
import type { AuctionDetail as AuctionDetailModel, BidPlacedNotification } from "@/api/types";

const FEED_LENGTH = 12;

const laterOf = (a: string, b: string) => (new Date(b).getTime() > new Date(a).getTime() ? b : a);

interface BidFeedItem {
  id: string;
  amount: number;
  at: string;
  bidderId: string;
  automatic: boolean;
}

export function AuctionDetail() {
  const { id } = useParams<{ id: string }>();
  const [auction, setAuction] = useState<AuctionDetailModel | null>(null);
  const [feed, setFeed] = useState<BidFeedItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [flash, setFlash] = useState(false);
  const [extended, setExtended] = useState(false);

  const now = useNow(1000);
  const reducedMotion = usePrefersReducedMotion();
  const connection = useConnectionState();
  const user = useAuthStore((state) => state.user);
  const t = useT();
  const format = useFormat();

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const [data, history] = await Promise.all([
        getAuctionById(id),
        getAuctionBids(id, { pageSize: FEED_LENGTH }).catch(() => null),
      ]);

      setAuction(data);
      setFeed(
        (history?.items ?? []).map((bid) => ({
          id: bid.id,
          amount: bid.amount,
          at: bid.placedAtUtc,
          bidderId: bid.bidderId,
          automatic: bid.isAutomatic,
        }))
      );
      setError(null);
    } catch (caught) {
      setError(toApiError(caught).message);
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  useAuctionChannel(id, {
    onBidPlaced: (notification: BidPlacedNotification) => {
      setAuction((previous) => {
        if (!previous) return previous;

        if (previous.bidCount > 0 && notification.amount <= previous.currentPrice) {
          return previous;
        }

        return {
          ...previous,
          currentPrice: notification.amount,
          bidCount: previous.bidCount + 1,
          minimumAcceptableBid: notification.amount + previous.minimumBidIncrement,
          endsAtUtc: laterOf(previous.endsAtUtc, notification.endsAtUtc),
        };
      });

      if (notification.auctionExtended) {
        setExtended(true);
        window.setTimeout(() => setExtended(false), 4000);
      }
      setFeed((previous) =>
        previous.some((entry) => entry.id === notification.bidId)
          ? previous
          : [
              {
                id: notification.bidId,
                amount: notification.amount,
                at: notification.occurredAtUtc,
                bidderId: notification.bidderId,
                automatic: notification.automatic,
              },
              ...previous,
            ].slice(0, FEED_LENGTH)
      );
      setFlash(true);
      window.setTimeout(() => setFlash(false), 700);
    },
    onStatusChanged: (notification) => {
      setAuction((previous) =>
        previous
          ? {
              ...previous,
              status: notification.status,
              currentPrice: notification.currentPrice,
              minimumAcceptableBid:
                previous.bidCount === 0
                  ? previous.startingPrice
                  : notification.currentPrice + previous.minimumBidIncrement,
            }
          : previous
      );
    },
  });

  if (loading && !auction) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-paper">
        <p className="font-mono text-eyebrow uppercase text-stone">{t("app.loading")}</p>
      </div>
    );
  }

  if (error || !auction) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-6 bg-paper px-6 text-center">
        <p className="font-display text-4xl font-light text-ink">{t("detail.notFound")}</p>
        <p className="max-w-[40ch] font-sans text-sm text-ink/60">{error}</p>
        <Link to="/auctions" className="btn-ghost">
          {t("detail.backButton")}
        </Link>
      </div>
    );
  }

  const showcase = showcaseForAuction(auction);
  const endsAt = new Date(auction.endsAtUtc).getTime();
  const startsAt = new Date(auction.startsAtUtc).getTime();
  const remaining = endsAt - now;
  const isLive = auction.status === "Active" && remaining > 0 && now >= startsAt;
  const minimumNextBid = auction.minimumAcceptableBid;

  return (
    <div className="min-h-screen bg-paper pb-32 pt-28 md:pt-32">
      <div className="shell mx-auto max-w-shell">
        <Link
          to="/auctions"
          className="font-mono text-eyebrow uppercase text-stone transition-colors hover:text-ink"
        >
          {t("detail.backToHall")}
        </Link>

        <div className="mt-10 grid gap-14 lg:grid-cols-12 lg:gap-10">
          <div className="lg:col-span-7">
            <AuctionStage
              title={auction.title}
              imageUrl={auction.imageUrl}
              showcase={showcase}
              reducedMotion={reducedMotion}
            />

            <div className="mt-12">
              <p className="eyebrow">{t("detail.description")}</p>
              <p className="mt-5 max-w-[60ch] font-sans text-base leading-relaxed text-ink/75">
                {auction.description}
              </p>
            </div>

            <dl className="mt-12 grid grid-cols-2 gap-x-8 gap-y-8 border-t border-ink/12 pt-10 md:grid-cols-4">
              {[
                { label: t("detail.seller"), value: auction.sellerDisplayName },
                {
                  label: t("detail.category"),
                  value: showcase ? t(showcase.categoryKey) : t("catalog.sellerListing"),
                },
                { label: t("detail.startingPrice"), value: format.money(auction.startingPrice) },
                {
                  label: t("detail.minIncrement"),
                  value: format.money(auction.minimumBidIncrement),
                },
                { label: t("detail.startsAt"), value: format.dateTime(auction.startsAtUtc) },
                { label: t("detail.endsAt"), value: format.dateTime(auction.endsAtUtc) },
              ].map((entry) => (
                <div key={entry.label}>
                  <dt className="font-mono text-eyebrow uppercase text-stone">{entry.label}</dt>
                  <dd className="mt-2 font-sans text-sm text-ink">{entry.value}</dd>
                </div>
              ))}
            </dl>
          </div>

          <div className="lg:col-span-5">
            <div className="lg:sticky lg:top-28">
              <p className="eyebrow">
                {showcase ? t(showcase.categoryKey) : t("catalog.sellerListing")}
              </p>
              <h1 className="mt-5 font-display text-huge font-light leading-[0.95] text-ink">
                {auction.title}
              </h1>

              <div className="mt-10 flex items-end justify-between gap-6 border-b border-ink/12 pb-8">
                <div>
                  <p className="eyebrow mb-3">{t("detail.currentBid")}</p>
                  <p
                    className={`font-display text-5xl font-light tabular-nums transition-colors duration-500 ${
                      flash ? "text-sand-deep" : "text-ink"
                    }`}
                  >
                    {format.money(auction.currentPrice)}
                  </p>
                </div>

                <div className="text-right">
                  <p className="eyebrow mb-3">
                    {isLive ? t("detail.remaining") : t("detail.status")}
                  </p>
                  <p
                    className={`font-mono text-lg tabular-nums transition-colors duration-500 ${
                      extended ? "text-sand-deep" : "text-ink"
                    }`}
                  >
                    {isLive ? format.countdown(remaining) : format.status(auction.status)}
                  </p>
                  {extended && (
                    <p className="mt-2 font-mono text-eyebrow uppercase text-sand-deep">
                      {t("detail.extended")}
                    </p>
                  )}
                </div>
              </div>

              <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-2">
                <span className="flex items-center gap-2.5">
                  <span
                    className={`h-1.5 w-1.5 rounded-full ${
                      connection === "connected" ? "animate-pulse bg-sand-deep" : "bg-stone-light"
                    }`}
                  />
                  <span className="font-mono text-eyebrow uppercase text-stone">
                    {connection === "connected"
                      ? t("detail.hallLive")
                      : connection === "reconnecting"
                        ? t("detail.reconnecting")
                        : t("detail.awaitingConnection")}
                  </span>
                </span>

                <span className="font-mono text-eyebrow uppercase tabular-nums text-stone">
                  {auction.bidCount === 0
                    ? t("detail.noBidsYet")
                    : t("detail.bidCount", { n: auction.bidCount })}
                </span>
              </div>

              <div className="mt-8">
                <BidPanel
                  auction={auction}
                  minimumNextBid={minimumNextBid}
                  isLive={isLive}
                  onAccepted={(result) => {
                    setAuction((previous) =>
                      previous
                        ? {
                            ...previous,
                            currentPrice: result.currentPrice,
                            bidCount: result.bidCount,
                            minimumAcceptableBid: result.minimumNextBid,
                            endsAtUtc: laterOf(previous.endsAtUtc, result.endsAtUtc),
                          }
                        : previous
                    );

                    if (result.auctionExtended) {
                      setExtended(true);
                      window.setTimeout(() => setExtended(false), 4000);
                    }
                  }}
                  onWithdrawn={() =>
                    setAuction((previous) =>
                      previous ? { ...previous, status: "Cancelled" } : previous
                    )
                  }
                />
              </div>

              <div className="mt-10">
                <p className="eyebrow mb-5">{t("detail.feed")}</p>
                {feed.length === 0 ? (
                  <p className="font-sans text-sm text-ink/45">{t("detail.feedEmpty")}</p>
                ) : (
                  <ul className="space-y-0">
                    {feed.map((entry) => (
                      <li
                        key={entry.id}
                        className="flex animate-veil-up items-center justify-between border-b border-ink/8 py-3"
                      >
                        <span className="flex items-center gap-3">
                          <span className="font-mono text-eyebrow uppercase text-stone">
                            {format.time(entry.at)}
                          </span>
                          {user?.id === entry.bidderId && (
                            <span className="font-mono text-eyebrow uppercase text-sand-deep">
                              {t("detail.yours")}
                            </span>
                          )}
                          {entry.automatic && (
                            <span className="font-mono text-eyebrow uppercase text-stone">
                              {t("detail.automatic")}
                            </span>
                          )}
                        </span>
                        <span className="font-display text-lg font-light tabular-nums text-ink">
                          {format.money(entry.amount)}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
