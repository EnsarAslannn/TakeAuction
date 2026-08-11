import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import gsap from "gsap";
import { SHOWCASE, showcaseForTitle, type ShowcaseModel } from "@/content/catalog";
import { useDragSelect } from "@/lib/useDragSelect";
import { usePrefersReducedMotion } from "@/lib/hooks";
import { SplitLine } from "@/motion/Reveal";
import { formatMoney } from "@/lib/format";
import type { AuctionListItem } from "@/api/types";

interface HeroProps {
  auctions: AuctionListItem[];
}

const BACKGROUND = "/visuals/auction-hall.webp";
const BACKGROUND_FALLBACK = "/visuals/hero-atrium.jpg";

const FAN = {
  /** Card-width fraction each step is offset by, before rotation adds its own spread. */
  spacingRatio: 0.72,
  /**
   * Rotating about a pivot below the card drags each neighbour back toward the
   * middle, so the step has to stay wide enough that the neighbour's centre
   * clears the selected card — otherwise half of it stops being clickable.
   */
  minSpacingRatio: 0.68,
  /** Degrees of tilt per step away from the selected lot. */
  tilt: 5,
  liftPerStep: 10,
  scalePerStep: 0.09,
  /** Pivot below the card, so rotation swings like a held hand of cards. */
  origin: "50% 120%",
  duration: 0.85,
  ease: "power3.out",
};

interface Placement {
  x: number;
  y: number;
  rotation: number;
  scale: number;
  opacity: number;
  zIndex: number;
  pointerEvents: "auto" | "none";
}

function placeCard(offset: number, spacing: number, visible: number): Placement {
  const distance = Math.abs(offset);
  const hidden = distance > visible;

  return {
    x: offset * spacing,
    y: distance * FAN.liftPerStep,
    rotation: offset * FAN.tilt,
    scale: Math.max(0.7, 1 - distance * FAN.scalePerStep),
    // Cards stay fully opaque inside the fan — depth comes from scale and the
    // scrim, not translucency, so the room behind never shows through them.
    opacity: hidden ? 0 : 1,
    zIndex: 100 - distance,
    pointerEvents: hidden ? "none" : "auto",
  };
}

/**
 * The landing stage: a full-bleed room photograph, the platform's own statement
 * on the left, and the catalogue fanned out on the right. Selection is driven by
 * the arrows, the cards themselves, or a horizontal drag — never by page scroll,
 * so the page scrolls past normally.
 */
export function Hero({ auctions }: HeroProps) {
  const [index, setIndex] = useState(0);
  const total = SHOWCASE.length;
  const reducedMotion = usePrefersReducedMotion();

  const goTo = useCallback(
    (target: number) => setIndex(Math.min(total - 1, Math.max(0, target))),
    [total]
  );

  const commit = useCallback(
    (delta: number) => setIndex((i) => Math.min(total - 1, Math.max(0, i + delta))),
    [total]
  );
  const { offset, dragging, didMove, handlers } = useDragSelect({ onCommit: commit });

  const stageRef = useRef<HTMLDivElement>(null);
  const cardRefs = useRef<(HTMLLIElement | null)[]>([]);
  const settled = useRef(false);
  const [layout, setLayout] = useState({ spacing: 0, visible: 2 });

  // The fan has to stay inside its column, so the step is the smaller of the
  // ideal spread and whatever the measured stage can actually hold.
  const measure = useCallback(() => {
    const stage = stageRef.current;
    const card = cardRefs.current[0];
    if (!stage || !card) return;

    const stageWidth = stage.clientWidth;
    const cardWidth = card.offsetWidth;
    if (!stageWidth || !cardWidth) return;

    const visible = stageWidth < 560 ? 1 : 2;
    const ideal = cardWidth * FAN.spacingRatio;
    const fitted = (stageWidth - cardWidth) / 2 / visible;
    const floor = cardWidth * FAN.minSpacingRatio;

    setLayout({ spacing: Math.max(floor, Math.min(ideal, fitted)), visible });
  }, []);

  useLayoutEffect(measure, [measure]);

  useEffect(() => {
    window.addEventListener("resize", measure);
    return () => window.removeEventListener("resize", measure);
  }, [measure]);

  useLayoutEffect(() => {
    if (!layout.spacing) return;

    const tweens: gsap.core.Tween[] = [];
    // The first pass and reduced-motion both place cards without travel.
    const instant = !settled.current || reducedMotion;

    cardRefs.current.forEach((card, cardIndex) => {
      if (!card) return;

      const target = placeCard(cardIndex - index, layout.spacing, layout.visible);

      gsap.set(card, {
        xPercent: -50,
        transformOrigin: FAN.origin,
        zIndex: target.zIndex,
        pointerEvents: target.pointerEvents,
      });

      const motion = {
        x: target.x,
        y: target.y,
        rotation: target.rotation,
        scale: target.scale,
        opacity: target.opacity,
      };

      if (instant) {
        gsap.set(card, motion);
        return;
      }

      tweens.push(
        gsap.to(card, {
          ...motion,
          duration: FAN.duration,
          ease: FAN.ease,
          delay: Math.min(0.15, Math.abs(cardIndex - index) * 0.04),
          overwrite: "auto",
        })
      );
    });

    settled.current = true;

    // Killing mid-flight leaves each card where it stands, so the next
    // selection tweens on from there instead of snapping.
    return () => tweens.forEach((tween) => tween.kill());
  }, [index, layout, reducedMotion]);

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

      <div className="shell relative z-10 mx-auto flex h-full max-w-shell flex-col pb-8 pt-24 md:pb-14 md:pt-32">
        <div className="grid flex-1 items-center gap-10 lg:grid-cols-[minmax(0,40%)_1fr]">
          <div className="max-w-[34rem]">
            <p className="mb-5 flex items-center gap-3 font-mono text-eyebrow uppercase text-paper/60">
              <span aria-hidden className="h-px w-7 bg-sand" />
              Canlı müzayede salonu
            </p>

            <h1 className="font-display text-giant font-light leading-[0.88] text-paper">
              <SplitLine text="Nadir olanı hak eden alır" />
            </h1>

            <p className="mt-6 max-w-[38ch] font-sans text-sm leading-relaxed text-paper/60">
              Seçilmiş, sınırlı sayıda parça tek bir salonda toplanır. Her teklif veritabanı
              seviyesinde çözülür — kaybolan teklif yok, yanlış kazanan yok.
            </p>

            <div className="mt-9 flex flex-wrap items-center gap-4">
              <Link to="/auctions" className="btn bg-sand text-ink hover:bg-paper">
                Salona gir
              </Link>
              <a
                href="#how-it-works"
                className="btn border border-paper/25 text-paper hover:border-paper hover:bg-paper hover:text-ink"
              >
                Nasıl işliyor
              </a>
            </div>
          </div>

          <div className="min-w-0">
            <div className="mb-4 flex items-center justify-center gap-3">
              <span aria-hidden className="h-px w-7 bg-sand/70" />
              <p className="font-mono text-eyebrow uppercase text-paper/55">Şu an açık artırmada</p>
              <span className="font-mono text-eyebrow uppercase tabular-nums text-paper/30">
                {String(total).padStart(2, "0")} parça
              </span>
            </div>

            <div
              ref={stageRef}
              {...handlers}
              className={`relative h-[calc(var(--card-h)+2.5rem)] select-none overflow-hidden md:h-[calc(var(--card-h)+5rem)] ${
                dragging ? "cursor-grabbing" : "cursor-grab"
              }`}
              style={{
                touchAction: "pan-y",
                // Feathered edges so a card leaving the fan dissolves instead of
                // meeting a hard clip against the stage bounds.
                maskImage:
                  "linear-gradient(to right, transparent 0%, #000 7%, #000 93%, transparent 100%)",
                WebkitMaskImage:
                  "linear-gradient(to right, transparent 0%, #000 7%, #000 93%, transparent 100%)",
              }}
            >
              <ul
                className="absolute inset-0 will-change-transform"
                style={{
                  transform: `translate3d(${offset}px, 0, 0)`,
                  transition: dragging ? "none" : "transform 700ms cubic-bezier(0.16,1,0.3,1)",
                }}
              >
                {SHOWCASE.map((entry, entryIndex) => (
                  <li
                    key={entry.slug}
                    ref={(node) => {
                      cardRefs.current[entryIndex] = node;
                    }}
                    className="absolute left-1/2 top-3 will-change-transform md:top-6"
                    style={{ width: "var(--card-w)" }}
                  >
                    <CarouselCard
                      item={entry}
                      isActive={entryIndex === index}
                      auction={auctions.find(
                        (auction) => showcaseForTitle(auction.title)?.slug === entry.slug
                      )}
                      onSelect={() => {
                        if (!didMove()) goTo(entryIndex);
                      }}
                    />
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>

        <div className="mt-6 flex items-end justify-between gap-4 md:mt-10 md:gap-8">
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

            {active ? (
              <Link
                to={`/auctions/${active.id}`}
                className="btn ml-1 whitespace-nowrap border border-paper/25 px-5 text-paper hover:border-sand hover:bg-sand hover:text-ink md:ml-2 md:px-7"
              >
                Teklif ver
                <span className="hidden sm:inline"> · {formatMoney(active.currentPrice)}</span>
              </Link>
            ) : (
              <span className="btn ml-1 border border-paper/10 text-paper/30 md:ml-2">
                Yükleniyor…
              </span>
            )}
          </div>

          <div className="flex items-center gap-6 md:flex-1">
            <div className="relative hidden h-px flex-1 bg-paper/15 md:block">
              <div
                className="absolute inset-y-0 left-0 bg-sand transition-[width] duration-700 ease-editorial"
                style={{ width: `${((index + 1) / total) * 100}%` }}
              />
            </div>
            <p className="whitespace-nowrap font-display text-3xl font-light tabular-nums leading-none text-paper md:text-4xl">
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
    <button
      type="button"
      onClick={onSelect}
      aria-current={isActive}
      aria-label={`${item.shortLabel} parçasını seç`}
      className={`group relative block h-[var(--card-h)] w-full overflow-hidden rounded-2xl bg-ink text-left transition-shadow duration-700 ease-editorial ${
        isActive
          ? "shadow-[0_38px_90px_-34px_rgba(0,0,0,0.95)] ring-1 ring-sand/50"
          : "shadow-[0_18px_50px_-30px_rgba(0,0,0,0.8)]"
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

      {/* Only the selected lot is captioned — captions on the fanned cards
          behind it read as loose text floating over the room. */}
      {auction && (
        <span
          className={`absolute right-3 top-3 rounded-full bg-paper/90 px-3 py-1 font-mono text-[0.6rem] uppercase tracking-[0.14em] tabular-nums text-ink backdrop-blur-md transition-opacity duration-500 ease-editorial ${
            isActive ? "opacity-100" : "opacity-0"
          }`}
        >
          {formatMoney(auction.currentPrice)}
        </span>
      )}

      <div
        className={`absolute inset-x-4 bottom-4 transition-opacity duration-500 ease-editorial ${
          isActive ? "opacity-100" : "opacity-0"
        }`}
      >
        <p className="font-mono text-[0.6rem] uppercase tracking-[0.18em] text-paper/60">
          {item.category}
        </p>
        <p className="mt-1.5 font-display text-lg font-light leading-tight text-paper">
          {item.shortLabel}
        </p>
      </div>
    </button>
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
