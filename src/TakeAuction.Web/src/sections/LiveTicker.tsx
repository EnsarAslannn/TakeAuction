import { useEffect, useState } from "react";
import { useLobbyChannel } from "@/realtime/useAuctionHub";
import { useConnectionState } from "@/realtime/useAuctionHub";
import { formatMoney } from "@/lib/format";
import type { BidPlacedNotification } from "@/api/types";

interface TickerEntry {
  id: string;
  amount: number;
  at: number;
}

/**
 * A live proof-of-life strip. It listens on the lobby group so any bid anywhere on
 * the platform scrolls past, which is the fastest way to show the realtime layer works.
 */
export function LiveTicker() {
  const [entries, setEntries] = useState<TickerEntry[]>([]);
  const state = useConnectionState();

  useLobbyChannel({
    onBidPlaced: (notification: BidPlacedNotification) => {
      setEntries((previous) =>
        [{ id: notification.bidId, amount: notification.amount, at: Date.now() }, ...previous].slice(0, 8)
      );
    },
  });

  useEffect(() => {
    const id = window.setInterval(() => {
      setEntries((previous) => previous.filter((entry) => Date.now() - entry.at < 45_000));
    }, 5_000);
    return () => window.clearInterval(id);
  }, []);

  const label =
    state === "connected"
      ? "Canlı yayın açık"
      : state === "reconnecting"
        ? "Yeniden bağlanıyor"
        : state === "connecting"
          ? "Bağlanıyor"
          : "Bağlantı yok";

  return (
    <div className="border-y border-ink/10 bg-paper-pure">
      <div className="shell mx-auto flex max-w-shell items-center gap-6 py-4">
        <span className="flex shrink-0 items-center gap-2.5">
          <span
            className={`h-1.5 w-1.5 rounded-full ${
              state === "connected" ? "animate-pulse bg-sand-deep" : "bg-stone-light"
            }`}
          />
          <span className="font-mono text-eyebrow uppercase text-stone">{label}</span>
        </span>

        <div className="h-4 w-px shrink-0 bg-ink/10" />

        <div className="flex-1 overflow-hidden">
          {entries.length === 0 ? (
            <p className="font-mono text-eyebrow uppercase text-stone/60">
              Yeni teklif bekleniyor…
            </p>
          ) : (
            <ul className="flex gap-8">
              {entries.map((entry) => (
                <li
                  key={entry.id}
                  className="shrink-0 animate-veil-up font-mono text-eyebrow uppercase tabular-nums text-ink/70"
                >
                  <span className="text-sand-deep">▲</span> {formatMoney(entry.amount)}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
