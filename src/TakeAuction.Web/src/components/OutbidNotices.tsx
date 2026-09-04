import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { auctionHub } from "@/realtime/hub";
import { useFormat, useT } from "@/i18n";
import { useAuthStore } from "@/store/authStore";
import type { OutbidNotification } from "@/api/types";

const DISMISS_AFTER_MS = 12_000;

interface Notice extends OutbidNotification {
  key: string;
}

export function OutbidNotices() {
  const user = useAuthStore((state) => state.user);
  const [notices, setNotices] = useState<Notice[]>([]);
  const t = useT();
  const format = useFormat();

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
          : [{ ...notification, key }, ...previous.filter((n) => n.auctionId !== notification.auctionId)]
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
      {notices.map((notice) => {
        const [before, after = ""] = t("outbid.body", {
          price: format.money(notice.currentPrice),
        }).split("{title}");

        return (
        <div
          key={notice.key}
          role="status"
          className="pointer-events-auto animate-veil-up border border-ink/15 bg-paper-pure p-5 shadow-lg"
        >
          <p className="eyebrow mb-3">{t("outbid.title")}</p>
          <p className="font-sans text-sm leading-relaxed text-ink/75">
            {before}
            <span className="text-ink">{notice.auctionTitle}</span>
            {after}
          </p>
          <div className="mt-4 flex items-center gap-4">
            <Link to={`/auctions/${notice.auctionId}`} className="btn-ghost">
              {t("outbid.back")}
            </Link>
            <button
              type="button"
              onClick={() =>
                setNotices((previous) => previous.filter((entry) => entry.key !== notice.key))
              }
              className="font-mono text-eyebrow uppercase text-stone transition-colors duration-300 hover:text-ink"
            >
              {t("outbid.dismiss")}
            </button>
          </div>
        </div>
        );
      })}
    </div>
  );
}
