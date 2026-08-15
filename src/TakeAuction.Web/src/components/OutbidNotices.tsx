import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { auctionHub } from "@/realtime/hub";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/authStore";
import type { OutbidNotification } from "@/api/types";

const DISMISS_AFTER_MS = 12_000;

interface Notice extends OutbidNotification {
  key: string;
}

/**
 * Keeps the hub connection open for the length of a session and surfaces the one message that
 * is addressed to this bidder rather than to a lot. Without it a bidder only ever finds out
 * they lost a lot by going back and looking at it.
 */
export function OutbidNotices() {
  const user = useAuthStore((state) => state.user);
  const [notices, setNotices] = useState<Notice[]>([]);

  useEffect(() => {
    if (!user) {
      setNotices([]);
      return;
    }

    let release: (() => void) | undefined;
    let disposed = false;

    auctionHub.hold().then((fn) => {
      if (disposed) fn();
      else release = fn;
    });

    const off = auctionHub.onOutbid((notification) => {
      const key = `${notification.auctionId}:${notification.occurredAtUtc}`;

      setNotices((previous) =>
        previous.some((notice) => notice.key === key)
          ? previous
          : // One notice per lot: a lot being bid up repeatedly is one piece of news, and
            // stacking it would bury everything else under the same auction.
            [{ ...notification, key }, ...previous.filter((n) => n.auctionId !== notification.auctionId)]
      );

      window.setTimeout(
        () => setNotices((previous) => previous.filter((notice) => notice.key !== key)),
        DISMISS_AFTER_MS
      );
    });

    return () => {
      disposed = true;
      off();
      release?.();
    };
  }, [user]);

  if (notices.length === 0) {
    return null;
  }

  return (
    <div className="pointer-events-none fixed bottom-6 right-6 z-50 flex w-[min(24rem,calc(100vw-3rem))] flex-col gap-3">
      {notices.map((notice) => (
        <div
          key={notice.key}
          role="status"
          className="pointer-events-auto animate-veil-up border border-ink/15 bg-paper-pure p-5 shadow-lg"
        >
          <p className="eyebrow mb-3">Geçildiniz</p>
          <p className="font-sans text-sm leading-relaxed text-ink/75">
            <span className="text-ink">{notice.auctionTitle}</span> için verdiğiniz sınır aşıldı.
            Parça şu an {formatMoney(notice.currentPrice)}.
          </p>
          <div className="mt-4 flex items-center gap-4">
            <Link to={`/auctions/${notice.auctionId}`} className="btn-ghost">
              Parçaya dönün
            </Link>
            <button
              type="button"
              onClick={() =>
                setNotices((previous) => previous.filter((entry) => entry.key !== notice.key))
              }
              className="font-mono text-eyebrow uppercase text-stone transition-colors duration-300 hover:text-ink"
            >
              Kapat
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
