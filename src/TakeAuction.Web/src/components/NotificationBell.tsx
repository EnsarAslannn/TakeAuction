import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useNotificationStore } from "@/store/notificationStore";
import { describeNotification } from "@/lib/notificationCopy";
import { useFormat, useT } from "@/i18n";

export function NotificationBell({ dark }: { dark: boolean }) {
  const items = useNotificationStore((state) => state.items);
  const unreadCount = useNotificationStore((state) => state.unreadCount);
  const status = useNotificationStore((state) => state.status);
  const markAllRead = useNotificationStore((state) => state.markAllRead);
  const [open, setOpen] = useState(false);
  const [freshIds, setFreshIds] = useState<ReadonlySet<string>>(new Set());
  const root = useRef<HTMLDivElement>(null);
  const t = useT();
  const format = useFormat();

  useEffect(() => {
    if (!open) return;

    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const toggle = () => {
    if (open) {
      setOpen(false);
      return;
    }

    setFreshIds(new Set(items.filter((item) => item.readAtUtc === null).map((item) => item.id)));
    setOpen(true);
    void markAllRead();
  };

  return (
    <div ref={root} className="relative">
      <button
        type="button"
        onClick={toggle}
        aria-label={unreadCount > 0 ? `${t("notify.bell")} — ${t("notify.unread", { n: unreadCount })}` : t("notify.bell")}
        aria-expanded={open}
        aria-haspopup="true"
        className={`relative flex h-9 w-9 items-center justify-center rounded-full transition-colors duration-500 ${
          dark ? "text-paper/70 hover:text-paper" : "text-stone hover:text-ink"
        }`}
      >
        <svg viewBox="0 0 24 24" aria-hidden className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth={1.25}>
          <path d="M6 16.5V11a6 6 0 1 1 12 0v5.5l1.5 1.5h-15L6 16.5Z" strokeLinejoin="round" />
          <path d="M10 20.25a2.1 2.1 0 0 0 4 0" strokeLinecap="round" />
        </svg>
        {unreadCount > 0 && (
          <span
            data-testid="notification-badge"
            className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-sand-deep px-1 font-mono text-[10px] leading-none tabular-nums text-paper"
          >
            {unreadCount > 9 ? "9+" : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div
          role="dialog"
          aria-label={t("notify.bell")}
          className="absolute right-0 top-full z-50 mt-3 w-[min(24rem,calc(100vw-2rem))] animate-veil-up border border-ink/15 bg-paper-pure shadow-lg"
        >
          <p className="eyebrow border-b border-ink/10 px-5 py-4">{t("notify.bell")}</p>

          {status === "error" && items.length === 0 ? (
            <p className="px-5 py-6 font-sans text-sm text-ink/60">{t("notify.loadFailed")}</p>
          ) : items.length === 0 ? (
            <p className="px-5 py-6 font-sans text-sm leading-relaxed text-ink/55">{t("notify.empty")}</p>
          ) : (
            <ul className="max-h-[60vh] overflow-y-auto" data-lenis-prevent>
              {items.map((item) => {
                const copy = describeNotification(item, t, format);
                const fresh = freshIds.has(item.id);

                return (
                  <li key={item.id} className="border-b border-ink/8 last:border-b-0">
                    <Link
                      to={`/auctions/${item.auctionId}`}
                      onClick={() => setOpen(false)}
                      className="flex gap-3 px-5 py-4 transition-colors duration-300 hover:bg-paper"
                    >
                      <span
                        aria-hidden
                        className={`mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full ${fresh ? "bg-sand-deep" : "bg-transparent"}`}
                      />
                      <span className="min-w-0">
                        <span className="block font-mono text-eyebrow uppercase text-stone">{copy.title}</span>
                        <span className="mt-1.5 block font-sans text-sm leading-relaxed text-ink/80">
                          {copy.body}
                        </span>
                        <span className="mt-2 block font-mono text-[10px] uppercase tracking-wider text-stone-light">
                          {format.dateTime(item.createdAtUtc)}
                        </span>
                      </span>
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
