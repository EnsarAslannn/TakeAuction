import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { SHOWCASE, showcaseForTitle, type ShowcaseModel } from "@/content/catalog";
import { useDragSelect } from "@/lib/useDragSelect";
import { formatMoney } from "@/lib/format";
import type { AuctionListItem } from "@/api/types";

interface HeroProps {
  auctions: AuctionListItem[];
}

const BACKGROUND = "/visuals/auction-hall.webp";
const BACKGROUND_FALLBACK = "/visuals/hero-atrium.jpg";
const CARD_GAP = 16;

/**
 * The landing stage: a full-bleed room photograph, the active lot written large
 * on the left, and the catalogue as a card strip on the right. Selection is
 * driven by the arrows, the cards themselves, or a horizontal drag — never by
 * page scroll, so the page scrolls past normally.
 */
export function Hero({ auctions }: HeroProps) {
  const [index, setIndex] = useState(0);
  const total = SHOWCASE.length;

  const goTo = useCallback(
    (target: number) => setIndex(Math.min(total - 1, Math.max(0, target))),
    [total]
  );

  const commit = useCallback((delta: number) => setIndex((i) => Math.min(total - 1, Math.max(0, i + delta))), [total]);
  const { offset, dragging, didMove, handlers } = useDragSelect({ onCommit: commit });

  // Measure the strip so the track stops once the last card reaches the right
  // edge. Without this the leading cards slide out of the section and become
  // both invisible and unclickable, leaving dead space on the right.
  const stripRef = useRef<HTMLDivElement>(null);
  const trackRef = useRef<HTMLUListElement>(null);
  const [metrics, setMetrics] = useState({ step: 0, maxShift: 0 });

  const measure = useCallback(() => {
    const strip = stripRef.current;
    const track = trackRef.current;
    const card = track?.firstElementChild as HTMLElement | null;
    if (!strip || !track || !card) return;

    setMetrics({
      step: card.offsetWidth + CARD_GAP,
      maxShift: Math.max(0, track.scrollWidth - strip.clientWidth),
    });
  }, []);

  useLayoutEffect(measure, [measure]);

  useEffect(() => {
    window.addEventListener("resize", measure);
    return () => window.removeEventListener("resize", measure);
  }, [measure]);

  const shift = Math.min(index * metrics.step, metrics.maxShift);

  const item = SHOWCASE[index];
  const active = auctions.find((auction) => showcaseForTitle(auction.title)?.slug === item.slug);
  const atStart = index === 0;
  const atEnd = index === total - 1;

  return (
    <section id="hero" data-nav-theme="dark" className="relative h-[100svh] overflow-hidden bg-ink">
      <div className="absolute inset-0">
        <img
          src={BACKGROUND}
          alt=""
          aria-hidden
          onError={(event) => {
            const img = event.currentTarget;
            if (!img.src.endsWith(BACKGROUND_FALLBACK)) img.src = BACKGROUND_FALLBACK;
          }}
          className="h-full w-full object-cover object-center"
        />
        <div className="absolute inset-0 bg-gradient-to-r from-ink via-ink/78 to-ink/55" />
        <div className="absolute inset-0 bg-gradient-to-t from-ink via-transparent to-ink/60" />
      </div>

      <div className="grain absolute inset-0" />

      <div className="shell relative z-10 mx-auto flex h-full max-w-shell flex-col pb-10 pt-28 md:pb-14 md:pt-32">
        <div className="grid flex-1 items-center gap-10 lg:grid-cols-[minmax(0,40%)_1fr]">
          <div className="max-w-[34rem]">
            <p className="mb-5 flex items-center gap-3 font-mono text-eyebrow uppercase text-paper/60">
              <span aria-hidden className="h-px w-7 bg-sand" />
              {item.category}
            </p>

            <h1
              key={item.slug}
              className="animate-veil-up font-display text-giant font-light leading-[0.88] text-paper"
            >
              {item.shortLabel}
            </h1>

            <p className="mt-6 max-w-[38ch] font-sans text-sm leading-relaxed text-paper/60">
              {item.provenance} — her teklif, veritabanı seviyesinde çözülen bir yarışın sonucudur.
              Kaybolan teklif yok, yanlış kazanan yok.
            </p>

            <div className="mt-9 flex flex-wrap items-center gap-4">
              {active ? (
                <Link
                  to={`/auctions/${active.id}`}
                  className="btn bg-sand text-ink hover:bg-paper"
                >
                  Teklif ver · {formatMoney(active.currentPrice)}
                </Link>
              ) : (
                <span className="btn border border-paper/20 text-paper/40">Kayıt yükleniyor…</span>
              )}
              <Link
                to="/auctions"
                className="btn border border-paper/25 text-paper hover:border-paper hover:bg-paper hover:text-ink"
              >
                Salona gir
              </Link>
            </div>
          </div>

          <div ref={stripRef} className="relative -mr-[var(--shell-pad)] overflow-hidden">
            <div
              {...handlers}
              className={`select-none ${dragging ? "cursor-grabbing" : "cursor-grab"}`}
              style={{ touchAction: "pan-y" }}
            >
              <ul
                ref={trackRef}
                className="flex items-end will-change-transform"
                style={{
                  gap: `${CARD_GAP}px`,
                  transform: `translate3d(${-shift + offset}px, 0, 0)`,
                  transition: dragging ? "none" : "transform 900ms cubic-bezier(0.16,1,0.3,1)",
                }}
              >
                {SHOWCASE.map((entry, entryIndex) => (
                  <CarouselCard
                    key={entry.slug}
                    item={entry}
                    isActive={entryIndex === index}
                    auction={auctions.find(
                      (auction) => showcaseForTitle(auction.title)?.slug === entry.slug
                    )}
                    onSelect={() => {
                      if (!didMove()) goTo(entryIndex);
                    }}
                  />
                ))}
              </ul>
            </div>
          </div>
        </div>

        <div className="mt-10 flex items-end justify-between gap-8">
          <div className="flex items-center gap-3">
            <ArrowButton
              direction="prev"
              disabled={atStart}
              onClick={() => goTo(index - 1)}
              label="Önceki parça"
            />
            <ArrowButton
              direction="next"
              disabled={atEnd}
              onClick={() => goTo(index + 1)}
              label="Sonraki parça"
            />
          </div>

          <div className="flex flex-1 items-center gap-6">
            <div className="relative h-px flex-1 bg-paper/15">
              <div
                className="absolute inset-y-0 left-0 bg-sand transition-[width] duration-700 ease-editorial"
                style={{ width: `${((index + 1) / total) * 100}%` }}
              />
            </div>
            <p className="font-display text-4xl font-light tabular-nums leading-none text-paper">
              {String(index + 1).padStart(2, "0")}
              <span className="ml-2 font-mono text-eyebrow text-paper/35">
                / {String(total).padStart(2, "0")}
              </span>
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}

function CarouselCard({
  item,
  isActive,
  auction,
  onSelect,
}: {
  item: ShowcaseModel;
  isActive: boolean;
  auction?: AuctionListItem;
  onSelect: () => void;
}) {
  const [failed, setFailed] = useState(false);

  return (
    <li className="shrink-0" style={{ width: "var(--card-w)" }}>
      <button
        type="button"
        onClick={onSelect}
        aria-current={isActive}
        aria-label={`${item.shortLabel} parçasını seç`}
        className={`group relative block h-[var(--card-h)] w-full overflow-hidden rounded-2xl bg-ink text-left transition-all duration-700 ease-editorial ${
          isActive ? "shadow-[0_28px_70px_-30px_rgba(0,0,0,0.9)] ring-1 ring-sand/50" : ""
        }`}
      >
        {failed ? (
          <div className="absolute inset-0 bg-gradient-to-br from-stone-dark via-ink-soft to-ink" />
        ) : (
          <img
            src={item.card}
            alt={item.shortLabel}
            loading="lazy"
            draggable={false}
            onError={() => setFailed(true)}
            className="absolute inset-0 h-full w-full object-cover transition-transform duration-[1200ms] ease-editorial group-hover:scale-105"
          />
        )}

        <div className="absolute inset-0 bg-gradient-to-t from-ink via-ink/25 to-transparent" />

        {/* Dim inactive cards with a scrim rather than opacity, so the room
            behind the section never shows through the card. */}
        <div
          className={`absolute inset-0 bg-ink transition-opacity duration-700 ease-editorial ${
            isActive ? "opacity-0" : "opacity-45 group-hover:opacity-20"
          }`}
        />

        {auction && (
          <span className="absolute right-3 top-3 rounded-full bg-paper/90 px-3 py-1 font-mono text-[0.6rem] uppercase tracking-[0.14em] tabular-nums text-ink backdrop-blur-md">
            {formatMoney(auction.currentPrice)}
          </span>
        )}

        <div className="absolute inset-x-4 bottom-4">
          <p className="font-mono text-[0.6rem] uppercase tracking-[0.18em] text-paper/60">
            {item.category}
          </p>
          <p className="mt-1.5 font-display text-lg font-light leading-tight text-paper">
            {item.shortLabel}
          </p>
        </div>
      </button>
    </li>
  );
}

function ArrowButton({
  direction,
  disabled,
  onClick,
  label,
}: {
  direction: "prev" | "next";
  disabled: boolean;
  onClick: () => void;
  label: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      className="group flex h-12 w-12 items-center justify-center rounded-full border border-paper/25 transition-all duration-500 ease-editorial hover:border-sand hover:bg-sand disabled:pointer-events-none disabled:opacity-25"
    >
      <span
        aria-hidden
        className={`font-mono text-lg text-paper transition-transform duration-500 group-hover:text-ink ${
          direction === "prev" ? "group-hover:-translate-x-0.5" : "group-hover:translate-x-0.5"
        }`}
      >
        {direction === "prev" ? "←" : "→"}
      </span>
    </button>
  );
}
