import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { auctionHub } from "@/realtime/hub";
import { useAuthStore } from "@/store/authStore";
import { useNotificationStore } from "@/store/notificationStore";
import { useWatchlistStore } from "@/store/watchlistStore";
import { describeNotification } from "@/lib/notificationCopy";
import { useFormat, useT } from "@/i18n";
import type { UserNotification } from "@/api/types";

const DISMISS_AFTER_MS = 12_000;

interface Toast {
  ownerId: string;
  notification: UserNotification;
}

export function NotificationCenter() {
  const userId = useAuthStore((state) => state.user?.id ?? null);
  const [toasts, setToasts] = useState<Toast[]>([]);
  const t = useT();
  const format = useFormat();

  useEffect(() => {
    const notifications = useNotificationStore.getState();
    const watchlist = useWatchlistStore.getState();

    if (!userId) {
      notifications.reset();
      watchlist.reset();
      return;
    }

    void notifications.load();
    void watchlist.load();

    let release: (() => void) | undefined;
    let disposed = false;

    auctionHub.hold().then((fn) => {
      if (disposed) fn();
      else release = fn;
    });

    const off = auctionHub.onNotification((notification) => {
      if (!useNotificationStore.getState().receive(notification)) return;

      setToasts((previous) => [{ ownerId: userId, notification }, ...previous]);

      window.setTimeout(
        () => setToasts((previous) => previous.filter((toast) => toast.notification.id !== notification.id)),
        DISMISS_AFTER_MS
      );
    });

    return () => {
      disposed = true;
      off();
      release?.();
    };
  }, [userId]);

  return (
    <>
      {toasts.filter((toast) => toast.ownerId === userId).map(({ notification: toast }) => {
        const copy = describeNotification(toast, t, format);

        return (
          <div
            key={toast.id}
            role="status"
            className="pointer-events-auto animate-veil-up border border-ink/15 bg-paper-pure p-5 shadow-lg"
          >
            <p className="eyebrow mb-3">{copy.title}</p>
            <p className="font-sans text-sm leading-relaxed text-ink/75">{copy.body}</p>
            <div className="mt-4 flex items-center gap-4">
              <Link to={`/auctions/${toast.auctionId}`} className="btn-ghost">
                {t("notify.open")}
              </Link>
              <button
                type="button"
                onClick={() =>
                  setToasts((previous) => previous.filter((entry) => entry.notification.id !== toast.id))
                }
                className="font-mono text-eyebrow uppercase text-stone transition-colors duration-300 hover:text-ink"
              >
                {t("notify.dismiss")}
              </button>
            </div>
          </div>
        );
      })}
    </>
  );
}
