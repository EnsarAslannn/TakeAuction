import { useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { useAuthStore } from "@/store/authStore";
import { useWatchlistStore } from "@/store/watchlistStore";
import { useT } from "@/i18n";
import type { AuctionStatus } from "@/api/types";

const quiet = "font-mono text-eyebrow uppercase transition-colors duration-300";

export function WatchButton({ auctionId, status }: { auctionId: string; status: AuctionStatus }) {
  const user = useAuthStore((state) => state.user);
  const watching = useWatchlistStore((state) => state.ids.has(auctionId));
  const pending = useWatchlistStore((state) => state.pending.has(auctionId));
  const toggle = useWatchlistStore((state) => state.toggle);
  const [failed, setFailed] = useState(false);
  const location = useLocation();
  const t = useT();

  const closed = status === "Ended" || status === "Cancelled";

  if (!user) {
    return closed ? null : (
      <Link to="/login" state={{ from: location.pathname }} className={`${quiet} text-stone hover:text-ink`}>
        {t("watch.signIn")}
      </Link>
    );
  }

  if (closed && !watching) {
    return null;
  }

  const onClick = async () => {
    setFailed(false);
    try {
      await toggle(auctionId);
    } catch {
      setFailed(true);
    }
  };

  return (
    <span className="flex flex-col items-start gap-1">
      <button
        type="button"
        onClick={onClick}
        disabled={pending}
        aria-pressed={watching}
        title={watching ? t("watch.stop") : t("watch.hint")}
        className={`${quiet} flex items-center gap-2 disabled:opacity-50 ${
          watching ? "text-sand-deep hover:text-ink" : "text-stone hover:text-ink"
        }`}
      >
        <svg viewBox="0 0 24 24" aria-hidden className="h-3.5 w-3.5" stroke="currentColor" strokeWidth={1.5} fill={watching ? "currentColor" : "none"}>
          <path d="M12 20s-7-4.35-7-10a4 4 0 0 1 7-2.65A4 4 0 0 1 19 10c0 5.65-7 10-7 10Z" strokeLinejoin="round" />
        </svg>
        {watching ? t("watch.watching") : t("watch.start")}
      </button>
      {failed && <span className="font-sans text-xs text-sand-deep">{t("watch.failed")}</span>}
    </span>
  );
}
