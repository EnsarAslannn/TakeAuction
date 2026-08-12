import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { placeBid } from "@/api/auctions";
import { ApiError, toApiError } from "@/api/client";
import { formatMoney, formatMoneyPrecise } from "@/lib/format";
import { useAuthStore } from "@/store/authStore";
import type { AuctionDetail, PlaceBidResponse } from "@/api/types";

interface BidPanelProps {
  auction: AuctionDetail;
  minimumNextBid: number;
  isLive: boolean;
  onAccepted: (result: PlaceBidResponse) => void;
}

type Feedback =
  | { kind: "idle" }
  | { kind: "pending" }
  | { kind: "accepted"; amount: number }
  | { kind: "outbid"; message: string }
  | { kind: "error"; message: string };

export function BidPanel({ auction, minimumNextBid, isLive, onAccepted }: BidPanelProps) {
  const user = useAuthStore((state) => state.user);
  const [amount, setAmount] = useState("");
  const [feedback, setFeedback] = useState<Feedback>({ kind: "idle" });

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
      setFeedback({ kind: "error", message: "Geçerli bir tutar gir." });
      return;
    }

    setFeedback({ kind: "pending" });

    try {
      const response = await placeBid(auction.id, Number(parsed.toFixed(2)));
      setFeedback({ kind: "accepted", amount: response.amount });
      onAccepted(response);
      setAmount(response.minimumNextBid.toFixed(2));
    } catch (caught) {
      const error = toApiError(caught);

      if (error instanceof ApiError && error.status === 409) {
        setFeedback({
          kind: "outbid",
          message:
            "Sen tutarı yazarken başka biri öne geçti. Güncel fiyat yukarıda güncellendi — üzerine çıkıp tekrar dene.",
        });
      } else {
        setFeedback({ kind: "error", message: error.message });
      }
    }
  };

  if (!user) {
    return (
      <div className="border border-ink/15 bg-paper-pure p-8">
        <p className="eyebrow mb-4">Teklif vermek için</p>
        <p className="font-sans text-base leading-relaxed text-ink/70">
          Salona girmen yeterli. Hesap açmak bir dakika sürer; sonrasında bu parçaya ve
          salondaki diğer her şeye teklif verebilirsin.
        </p>
        <div className="mt-7 flex flex-wrap gap-3">
          <Link to="/login" className="btn-primary">
            Giriş yap
          </Link>
          <Link to="/register" className="btn-ghost">
            Hesap aç
          </Link>
        </div>
      </div>
    );
  }

  if (isOwnAuction) {
    return (
      <div className="border border-ink/15 bg-paper-pure p-8">
        <p className="eyebrow mb-4">Bu parça senin</p>
        <p className="font-sans text-base leading-relaxed text-ink/70">
          Kendi ilanına teklif veremezsin. Kapanışta en yüksek teklifi veren alıcı otomatik
          olarak belirlenir; senin bir şey yapman gerekmez.
        </p>
      </div>
    );
  }

  if (!isLive) {
    return (
      <div className="border border-ink/15 bg-paper-pure p-8">
        <p className="eyebrow mb-4">Teklif kapalı</p>
        <p className="font-sans text-base leading-relaxed text-ink/70">
          {auction.status === "Scheduled"
            ? "Bu parça henüz açık artırmaya çıkmadı. Başlama saati aşağıda yazıyor."
            : "Bu açık artırma kapandı. Salonda başka parçalar var."}
        </p>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="border border-ink/15 bg-paper-pure p-8">
      <div className="flex items-baseline justify-between gap-4">
        <p className="eyebrow">Teklifin</p>
        <p className="font-mono text-eyebrow uppercase text-stone">
          en az {formatMoneyPrecise(minimumNextBid)}
        </p>
      </div>

      <div className="mt-6">
        <label htmlFor="bid-amount" className="sr-only">
          Teklif tutarı
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
            {formatMoney(Math.ceil(value))}
          </button>
        ))}
      </div>

      <button
        type="submit"
        disabled={feedback.kind === "pending"}
        className="btn-primary mt-7 w-full"
      >
        {feedback.kind === "pending" ? "Gönderiliyor…" : "Teklifi gönder"}
      </button>

      {feedback.kind === "accepted" && (
        <p className="mt-5 animate-veil-up font-sans text-sm leading-relaxed text-sand-deep">
          {formatMoneyPrecise(feedback.amount)} tutarındaki teklifin kabul edildi. Şu an öndesin.
        </p>
      )}

      {feedback.kind === "outbid" && (
        <p className="mt-5 border-l-2 border-slate pl-4 font-sans text-sm leading-relaxed text-ink/70">
          {feedback.message}
        </p>
      )}

      {feedback.kind === "error" && (
        <p className="mt-5 border-l-2 border-sand-deep pl-4 font-sans text-sm leading-relaxed text-ink/70">
          {feedback.message}
        </p>
      )}
    </form>
  );
}
