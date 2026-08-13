import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getAuctionById } from "@/api/auctions";
import { toApiError } from "@/api/client";
import { AuctionStage } from "@/components/AuctionStage";
import { BidPanel } from "@/components/BidPanel";
import { SELLER_LISTING_CATEGORY, showcaseForAuction } from "@/content/catalog";
import { useAuctionChannel, useConnectionState } from "@/realtime/useAuctionHub";
import { STATUS_LABEL, formatCountdown, formatDateTime, formatMoney } from "@/lib/format";
import { useNow, usePrefersReducedMotion } from "@/lib/hooks";
import type { AuctionDetail as AuctionDetailModel, BidPlacedNotification } from "@/api/types";

interface BidFeedItem {
  id: string;
  amount: number;
  at: string;
  isMine?: boolean;
}

export function AuctionDetail() {
  const { id } = useParams<{ id: string }>();
  const [auction, setAuction] = useState<AuctionDetailModel | null>(null);
  const [feed, setFeed] = useState<BidFeedItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [flash, setFlash] = useState(false);

  const now = useNow(1000);
  const reducedMotion = usePrefersReducedMotion();
  const connection = useConnectionState();

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const data = await getAuctionById(id);
      setAuction(data);
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

        // Simultaneous bids are broadcast from separate handlers, so notifications can reach
        // us out of order — and a live auction's price never moves down. This also absorbs
        // the echo of our own accepted bid, which onAccepted has already applied.
        // The bidCount check matters: the opening bid is allowed to equal the starting price,
        // and skipping it would leave the counter reading zero on a lot that has one.
        if (previous.bidCount > 0 && notification.amount <= previous.currentPrice) {
          return previous;
        }

        return {
          ...previous,
          currentPrice: notification.amount,
          bidCount: previous.bidCount + 1,
          minimumAcceptableBid: notification.amount + previous.minimumBidIncrement,
        };
      });
      setFeed((previous) =>
        [
          { id: notification.bidId, amount: notification.amount, at: notification.occurredAtUtc },
          ...previous,
        ].slice(0, 12)
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
        <p className="font-mono text-eyebrow uppercase text-stone">Yükleniyor…</p>
      </div>
    );
  }

  if (error || !auction) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-6 bg-paper px-6 text-center">
        <p className="font-display text-4xl font-light text-ink">Bu parça salonda değil</p>
        <p className="max-w-[40ch] font-sans text-sm text-ink/60">{error}</p>
        <Link to="/auctions" className="btn-ghost">
          Salona dönün
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
          ← Salon
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
              <p className="eyebrow">Açıklama</p>
              <p className="mt-5 max-w-[60ch] font-sans text-base leading-relaxed text-ink/75">
                {auction.description}
              </p>
            </div>

            <dl className="mt-12 grid grid-cols-2 gap-x-8 gap-y-8 border-t border-ink/12 pt-10 md:grid-cols-4">
              {[
                { label: "Satıcı", value: auction.sellerDisplayName },
                { label: "Kategori", value: showcase?.category ?? SELLER_LISTING_CATEGORY },
                { label: "Başlangıç", value: formatMoney(auction.startingPrice) },
                { label: "Min. artış", value: formatMoney(auction.minimumBidIncrement) },
                { label: "Başlama", value: formatDateTime(auction.startsAtUtc) },
                { label: "Bitiş", value: formatDateTime(auction.endsAtUtc) },
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
              <p className="eyebrow">{showcase?.category ?? SELLER_LISTING_CATEGORY}</p>
              <h1 className="mt-5 font-display text-huge font-light leading-[0.95] text-ink">
                {auction.title}
              </h1>

              <div className="mt-10 flex items-end justify-between gap-6 border-b border-ink/12 pb-8">
                <div>
                  <p className="eyebrow mb-3">Güncel teklif</p>
                  <p
                    className={`font-display text-5xl font-light tabular-nums transition-colors duration-500 ${
                      flash ? "text-sand-deep" : "text-ink"
                    }`}
                  >
                    {formatMoney(auction.currentPrice)}
                  </p>
                </div>

                <div className="text-right">
                  <p className="eyebrow mb-3">{isLive ? "Kalan" : "Durum"}</p>
                  <p className="font-mono text-lg tabular-nums text-ink">
                    {isLive
                      ? formatCountdown(remaining)
                      : STATUS_LABEL[auction.status] ?? auction.status}
                  </p>
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
                      ? "Salon canlı"
                      : connection === "reconnecting"
                        ? "Yeniden bağlanıyor"
                        : "Bağlantı bekleniyor"}
                  </span>
                </span>

                <span className="font-mono text-eyebrow uppercase tabular-nums text-stone">
                  {auction.bidCount === 0
                    ? "Henüz teklif yok"
                    : `${auction.bidCount} teklif verildi`}
                </span>
              </div>

              <div className="mt-8">
                <BidPanel
                  auction={auction}
                  minimumNextBid={minimumNextBid}
                  isLive={isLive}
                  onAccepted={(result) =>
                    setAuction((previous) =>
                      previous
                        ? {
                            ...previous,
                            currentPrice: result.currentPrice,
                            bidCount: result.bidCount,
                            minimumAcceptableBid: result.minimumNextBid,
                          }
                        : previous
                    )
                  }
                />
              </div>

              <div className="mt-10">
                <p className="eyebrow mb-5">Canlı teklif akışı</p>
                {feed.length === 0 ? (
                  <p className="font-sans text-sm text-ink/45">
                    Siz buradayken henüz teklif gelmedi. Yeni teklifler anında bu listeye düşer.
                  </p>
                ) : (
                  <ul className="space-y-0">
                    {feed.map((entry) => (
                      <li
                        key={entry.id}
                        className="flex animate-veil-up items-center justify-between border-b border-ink/8 py-3"
                      >
                        <span className="font-mono text-eyebrow uppercase text-stone">
                          {new Date(entry.at).toLocaleTimeString("tr-TR")}
                        </span>
                        <span className="font-display text-lg font-light tabular-nums text-ink">
                          {formatMoney(entry.amount)}
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
