import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { cancelAuction, placeBid } from "@/api/auctions";
import { ApiError, toApiError } from "@/api/client";
import { useFormat, useT } from "@/i18n";
import { useAuthStore } from "@/store/authStore";
import type { AuctionDetail, PlaceBidResponse } from "@/api/types";

interface BidPanelProps {
  auction: AuctionDetail;
  minimumNextBid: number;
  isLive: boolean;
  onAccepted: (result: PlaceBidResponse) => void;
  onWithdrawn: () => void;
}

type Withdrawal =
  | { kind: "idle" }
  | { kind: "pending" }
  | { kind: "conflict" }
  | { kind: "error"; message: string };

type Feedback =
  | { kind: "idle" }
  | { kind: "pending" }
  | { kind: "leading"; price: number; max: number }
  | { kind: "answered"; price: number; max: number }
  | { kind: "outbid" }
  | { kind: "invalid" }
  | { kind: "error"; message: string };

export function BidPanel({ auction, minimumNextBid, isLive, onAccepted, onWithdrawn }: BidPanelProps) {
  const user = useAuthStore((state) => state.user);
  const [amount, setAmount] = useState("");
  const [feedback, setFeedback] = useState<Feedback>({ kind: "idle" });
  const [withdrawal, setWithdrawal] = useState<Withdrawal>({ kind: "idle" });
  const t = useT();
  const format = useFormat();

  const pendingKey = useRef<{ amount: number; key: string } | null>(null);

  const keyFor = (value: number) => {
    if (pendingKey.current?.amount !== value) {
      pendingKey.current = { amount: value, key: crypto.randomUUID() };
    }

    return pendingKey.current.key;
  };

  const isOwnAuction = user?.id === auction.sellerId;

  useEffect(() => {
    setAmount(minimumNextBid.toFixed(2));
  }, [minimumNextBid]);

  const quickBids = useMemo(
    () => [minimumNextBid, minimumNextBid + auction.minimumBidIncrement * 2, minimumNextBid * 1.1],
    [minimumNextBid, auction.minimumBidIncrement]
  );

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();

    const parsed = Number(amount);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setFeedback({ kind: "invalid" });
      return;
    }

    setFeedback({ kind: "pending" });

    const bid = Number(parsed.toFixed(2));

    try {
      const response = await placeBid(auction.id, bid, keyFor(bid));
      pendingKey.current = null;
      setFeedback(
        response.isLeading
          ? { kind: "leading", price: response.currentPrice, max: response.maxAmount }
          : { kind: "answered", price: response.currentPrice, max: response.maxAmount }
      );
      onAccepted(response);
      setAmount(response.minimumNextBid.toFixed(2));
    } catch (caught) {
      const error = toApiError(caught);

      if (error instanceof ApiError && error.status === 409) {
        setFeedback({ kind: "outbid" });
      } else {
        setFeedback({ kind: "error", message: error.message });
      }
    }
  };

  const withdraw = async () => {
    setWithdrawal({ kind: "pending" });

    try {
      await cancelAuction(auction.id);
      setWithdrawal({ kind: "idle" });
      onWithdrawn();
    } catch (caught) {
      const error = toApiError(caught);

      setWithdrawal(
        error instanceof ApiError && error.status === 409
          ? { kind: "conflict" }
          : { kind: "error", message: error.message }
      );
    }
  };

  if (!user) {
    return (
      <div className="border border-ink/15 bg-paper-pure p-8">
        <p className="eyebrow mb-4">{t("bid.signedOut.eyebrow")}</p>
        <p className="font-sans text-base leading-relaxed text-ink/70">{t("bid.signedOut.body")}</p>
        <div className="mt-7 flex flex-wrap gap-3">
          <Link to="/login" className="btn-primary">
            {t("bid.signIn")}
          </Link>
          <Link to="/register" className="btn-ghost">
            {t("bid.openAccount")}
          </Link>
        </div>
      </div>
    );
  }

  if (isOwnAuction) {
    const withdrawable = auction.bidCount === 0 && auction.status !== "Ended" && auction.status !== "Cancelled";

    return (
      <div className="border border-ink/15 bg-paper-pure p-8">
        <p className="eyebrow mb-4">{t("bid.own.eyebrow")}</p>
        <p className="font-sans text-base leading-relaxed text-ink/70">{t("bid.own.body")}</p>

        {withdrawable ? (
          <>
            <p className="mt-6 font-sans text-sm leading-relaxed text-ink/60">
              {t("bid.own.withdrawable")}
            </p>
            <button
              type="button"
              onClick={withdraw}
              disabled={withdrawal.kind === "pending"}
              className="btn-ghost mt-5"
            >
              {withdrawal.kind === "pending" ? t("bid.own.withdrawing") : t("bid.own.withdraw")}
            </button>
          </>
        ) : (
          auction.status !== "Ended" &&
          auction.status !== "Cancelled" && (
            <p className="mt-6 border-l-2 border-slate pl-4 font-sans text-sm leading-relaxed text-ink/60">
              {t("bid.own.locked")}
            </p>
          )
        )}

        {(withdrawal.kind === "conflict" || withdrawal.kind === "error") && (
          <p className="mt-5 border-l-2 border-sand-deep pl-4 font-sans text-sm leading-relaxed text-ink/70">
            {withdrawal.kind === "conflict" ? t("bid.own.raceError") : withdrawal.message}
          </p>
        )}
      </div>
    );
  }

  if (!isLive) {
    return (
      <div className="border border-ink/15 bg-paper-pure p-8">
        <p className="eyebrow mb-4">{t("bid.closed.eyebrow")}</p>
        <p className="font-sans text-base leading-relaxed text-ink/70">
          {auction.status === "Scheduled"
            ? t("bid.closed.scheduled")
            : auction.status === "Cancelled"
              ? t("bid.closed.cancelled")
              : t("bid.closed.ended")}
        </p>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="border border-ink/15 bg-paper-pure p-8">
      <div className="flex items-baseline justify-between gap-4">
        <p className="eyebrow">{t("bid.limit")}</p>
        <p className="font-mono text-eyebrow uppercase text-stone">
          {t("bid.atLeast", { amount: format.moneyPrecise(minimumNextBid) })}
        </p>
      </div>

      <p className="mt-4 font-sans text-sm leading-relaxed text-ink/60">{t("bid.explain")}</p>

      <div className="mt-6">
        <label htmlFor="bid-amount" className="sr-only">
          {t("bid.inputLabel")}
        </label>
        <input
          id="bid-amount"
          type="number"
          step="0.01"
          min={minimumNextBid}
          value={amount}
          onChange={(event) => setAmount(event.target.value)}
          className="field font-display text-3xl tabular-nums"
          disabled={feedback.kind === "pending"}
        />
      </div>

      <div className="mt-5 flex flex-wrap gap-2">
        {quickBids.map((value) => (
          <button
            key={value}
            type="button"
            onClick={() => setAmount(value.toFixed(2))}
            className="rounded-full border border-ink/15 px-4 py-1.5 font-mono text-eyebrow uppercase tabular-nums text-stone transition-colors duration-300 hover:border-ink/40 hover:text-ink"
          >
            {format.money(Math.ceil(value))}
          </button>
        ))}
      </div>

      <button
        type="submit"
        disabled={feedback.kind === "pending"}
        className="btn-primary mt-7 w-full"
      >
        {feedback.kind === "pending" ? t("bid.submitting") : t("bid.submit")}
      </button>

      {feedback.kind === "leading" && (
        <p
          data-testid="bid-feedback"
          className="mt-5 animate-veil-up font-sans text-sm leading-relaxed text-sand-deep"
        >
          {t("bid.leading", {
            price: format.moneyPrecise(feedback.price),
            max: format.moneyPrecise(feedback.max),
          })}
        </p>
      )}

      {feedback.kind === "answered" && (
        <p
          data-testid="bid-feedback"
          className="mt-5 animate-veil-up border-l-2 border-slate pl-4 font-sans text-sm leading-relaxed text-ink/70"
        >
          {t("bid.answered", {
            price: format.moneyPrecise(feedback.price),
            max: format.moneyPrecise(feedback.max),
          })}
        </p>
      )}

      {feedback.kind === "outbid" && (
        <p
          data-testid="bid-feedback"
          className="mt-5 border-l-2 border-slate pl-4 font-sans text-sm leading-relaxed text-ink/70"
        >
          {t("bid.conflict")}
        </p>
      )}

      {(feedback.kind === "invalid" || feedback.kind === "error") && (
        <p
          data-testid="bid-feedback"
          className="mt-5 border-l-2 border-sand-deep pl-4 font-sans text-sm leading-relaxed text-ink/70"
        >
          {feedback.kind === "invalid" ? t("bid.invalidAmount") : feedback.message}
        </p>
      )}
    </form>
  );
}
