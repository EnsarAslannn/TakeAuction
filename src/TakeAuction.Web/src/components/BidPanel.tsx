import { useEffect, useMemo, useRef, useState } from "react";
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
  | { kind: "leading"; price: number; max: number }
  | { kind: "answered"; price: number; max: number }
  | { kind: "outbid"; message: string }
  | { kind: "error"; message: string };

export function BidPanel({ auction, minimumNextBid, isLive, onAccepted }: BidPanelProps) {
  const user = useAuthStore((state) => state.user);
  const [amount, setAmount] = useState("");
  const [feedback, setFeedback] = useState<Feedback>({ kind: "idle" });

  // One key per intended bid, not per request. A second click on the same amount is the same
  // bid being sent again — the server recognises the key and hands back the first answer
  // instead of raising the price twice. Typing a new amount is a new intent, so a new key.
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
      setFeedback({ kind: "error", message: "Geçerli bir tutar girin." });
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
        setFeedback({
          kind: "outbid",
          message:
            "Siz tutarı yazarken başka biri öne geçti. Güncel fiyat yukarıda güncellendi — üzerine çıkıp tekrar deneyin.",
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
          Salona girmeniz yeterli. Hesap açmak bir dakika sürer; sonrasında bu parçaya ve
          salondaki diğer her şeye teklif verebilirsiniz.
        </p>
        <div className="mt-7 flex flex-wrap gap-3">
          <Link to="/login" className="btn-primary">
            Giriş yapın
          </Link>
          <Link to="/register" className="btn-ghost">
            Hesap açın
          </Link>
        </div>
      </div>
    );
  }

  if (isOwnAuction) {
    return (
      <div className="border border-ink/15 bg-paper-pure p-8">
        <p className="eyebrow mb-4">Bu parça sizin</p>
        <p className="font-sans text-base leading-relaxed text-ink/70">
          Kendi ilanınıza teklif veremezsiniz. Kapanışta en yüksek teklifi veren alıcı otomatik
          olarak belirlenir; sizin bir şey yapmanız gerekmez.
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
        <p className="eyebrow">Sınırınız</p>
        <p className="font-mono text-eyebrow uppercase text-stone">
          en az {formatMoneyPrecise(minimumNextBid)}
        </p>
      </div>

      <p className="mt-4 font-sans text-sm leading-relaxed text-ink/60">
        Ödeyeceğiniz en yüksek tutarı yazın. Bu rakamı kimse görmez; sizin adınıza yalnızca önde
        kalmaya yetecek kadar artırırız.
      </p>

      <div className="mt-6">
        <label htmlFor="bid-amount" className="sr-only">
          En yüksek teklif tutarınız
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
        {feedback.kind === "pending" ? "Gönderiliyor…" : "Sınırınızı gönderin"}
      </button>

      {feedback.kind === "leading" && (
        <p
          data-testid="bid-feedback"
          className="mt-5 animate-veil-up font-sans text-sm leading-relaxed text-sand-deep"
        >
          Öndesiniz. Parça şu an {formatMoneyPrecise(feedback.price)} ve sizin adınıza en fazla{" "}
          {formatMoneyPrecise(feedback.max)} verilecek. Rakip çıkarsa gerektiği kadar artırırız —
          ekranın başında beklemenize gerek yok.
        </p>
      )}

      {feedback.kind === "answered" && (
        <p
          data-testid="bid-feedback"
          className="mt-5 animate-veil-up border-l-2 border-slate pl-4 font-sans text-sm leading-relaxed text-ink/70"
        >
          {formatMoneyPrecise(feedback.max)} sınırınız yetmedi: önde olan alıcının sınırı daha
          yüksekti ve parça {formatMoneyPrecise(feedback.price)} seviyesine çıktı. Daha yüksek bir
          sınır verirseniz öne geçebilirsiniz.
        </p>
      )}

      {feedback.kind === "outbid" && (
        <p
          data-testid="bid-feedback"
          className="mt-5 border-l-2 border-slate pl-4 font-sans text-sm leading-relaxed text-ink/70"
        >
          {feedback.message}
        </p>
      )}

      {feedback.kind === "error" && (
        <p
          data-testid="bid-feedback"
          className="mt-5 border-l-2 border-sand-deep pl-4 font-sans text-sm leading-relaxed text-ink/70"
        >
          {feedback.message}
        </p>
      )}
    </form>
  );
}
