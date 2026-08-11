import { Link } from "react-router-dom";
import { showcaseForAuction } from "@/content/catalog";
import { STATUS_LABEL, formatMoney, formatCountdown } from "@/lib/format";
import { useNow } from "@/lib/hooks";
import type { AuctionListItem } from "@/api/types";

const STATUS_TONE: Record<string, string> = {
  Active: "text-sand-deep",
  Scheduled: "text-slate-deep",
  Ended: "text-stone",
  Cancelled: "text-stone",
};

export function AuctionCard({ auction, index }: { auction: AuctionListItem; index: number }) {
  const now = useNow(1000);
  const showcase = showcaseForAuction(auction);
  const remaining = new Date(auction.endsAtUtc).getTime() - now;
  const isLive = auction.status === "Active" && remaining > 0;

  return (
    <Link
      to={`/auctions/${auction.id}`}
      className="group relative block border-t border-ink/12 py-8 transition-colors duration-500 hover:bg-paper-pure md:py-10"
    >
      <div className="flex flex-col gap-6 md:grid md:grid-cols-12 md:items-center md:gap-6">
        <span className="font-mono text-eyebrow uppercase text-stone md:col-span-1">
          {String(index + 1).padStart(2, "0")}
        </span>

        <div className="min-w-0 md:col-span-4 md:pr-8">
          <h3 className="text-balance font-display text-2xl font-light leading-tight text-ink transition-transform duration-700 ease-editorial group-hover:translate-x-2 md:text-[1.6rem]">
            {auction.title}
          </h3>
          <p className="mt-2 font-mono text-eyebrow uppercase text-stone">{showcase.category}</p>
        </div>

        <div className="md:col-span-2">
          <p className="font-mono text-eyebrow uppercase text-stone">Güncel</p>
          <p className="mt-1.5 font-display text-xl font-light tabular-nums text-ink">
            {formatMoney(auction.currentPrice)}
          </p>
        </div>

        <div className="md:col-span-2">
          <p className="font-mono text-eyebrow uppercase text-stone">
            {isLive ? "Kalan süre" : "Durum"}
          </p>
          <p
            className={`mt-1.5 font-mono text-sm tabular-nums ${STATUS_TONE[auction.status] ?? "text-ink"}`}
          >
            {isLive ? formatCountdown(remaining) : STATUS_LABEL[auction.status] ?? auction.status}
          </p>
        </div>

        <div className="flex items-center justify-between md:col-span-3 md:justify-end md:gap-6">
          {isLive && (
            <span className="flex items-center gap-2">
              <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-sand-deep" />
              <span className="font-mono text-eyebrow uppercase text-sand-deep">Canlı</span>
            </span>
          )}
          <span
            aria-hidden
            className="font-mono text-lg text-ink/25 transition-all duration-500 group-hover:translate-x-1 group-hover:text-sand-deep"
          >
            →
          </span>
        </div>
      </div>
    </Link>
  );
}
