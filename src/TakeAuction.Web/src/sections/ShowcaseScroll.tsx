import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { SHOWCASE, showcaseForTitle } from "@/content/catalog";
import { ShowcaseCanvas } from "@/three/ShowcaseCanvas";
import { useDragRotate } from "@/three/useDragRotate";
import { smoothScrollTo } from "@/lib/scrollTo";
import { formatMoney } from "@/lib/format";
import type { AuctionListItem } from "@/api/types";

interface ShowcaseScrollProps {
  auctions: AuctionListItem[];
}

/**
 * A tall scroll track with a pinned viewport. Scroll position selects which of the
 * five models is on stage; the arrows drive the same selection by scrolling to that
 * model's slice, so both inputs stay in agreement. Rotation is user-controlled
 * (drag + idle spin) rather than scroll-driven, so the two never fight.
 */
export function ShowcaseScroll({ auctions }: ShowcaseScrollProps) {
  const trackRef = useRef<HTMLDivElement>(null);
  const [index, setIndex] = useState(0);
  const [progress, setProgress] = useState(0);

  const { state: dragState, dragging, reset, decay, handlers } = useDragRotate();

  useEffect(() => {
    let frame = 0;

    const update = () => {
      frame = 0;
      const track = trackRef.current;
      if (!track) return;

      const rect = track.getBoundingClientRect();
      const scrollable = rect.height - window.innerHeight;
      if (scrollable <= 0) return;

      const scrolled = Math.min(Math.max(-rect.top, 0), scrollable);
      const overall = scrolled / scrollable;

      const slice = 1 / SHOWCASE.length;
      const active = Math.min(SHOWCASE.length - 1, Math.floor(overall / slice));

      setIndex(active);
      setProgress((overall - active * slice) / slice);
    };

    const onScroll = () => {
      if (!frame) frame = requestAnimationFrame(update);
    };

    update();
    window.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll);

    return () => {
      if (frame) cancelAnimationFrame(frame);
      window.removeEventListener("scroll", onScroll);
      window.removeEventListener("resize", onScroll);
    };
  }, []);

  // A fresh model should start from its authored angle, not the previous one's.
  useEffect(() => reset(), [index, reset]);

  const goTo = useCallback((target: number) => {
    const track = trackRef.current;
    if (!track) return;

    const clamped = Math.min(SHOWCASE.length - 1, Math.max(0, target));
    const rect = track.getBoundingClientRect();
    const trackTop = window.scrollY + rect.top;
    const scrollable = rect.height - window.innerHeight;
    const slice = scrollable / SHOWCASE.length;

    // Land mid-slice so the item is unambiguously selected.
    smoothScrollTo(trackTop + clamped * slice + slice * 0.5);
  }, []);

  const item = SHOWCASE[index];
  const liveAuction = auctions.find((auction) => showcaseForTitle(auction.title)?.slug === item.slug);
  const atStart = index === 0;
  const atEnd = index === SHOWCASE.length - 1;

  return (
    <section
      id="showcase"
      ref={trackRef}
      data-nav-theme="dark"
      className="relative bg-ink text-paper"
      style={{ height: `${SHOWCASE.length * 100}vh` }}
    >
      <div className="sticky top-0 h-[100svh] overflow-hidden">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 opacity-[0.35]"
          style={{
            background:
              "radial-gradient(70% 55% at 50% 42%, rgba(192,160,112,0.28) 0%, transparent 70%)",
          }}
        />

        <ShowcaseCanvas
          item={item}
          drag={dragState}
          onDecay={decay}
          className="absolute inset-0"
        />

        {/* Drag surface. `pan-y` keeps vertical page scrolling available on touch
            while horizontal drags rotate the model. */}
        <div
          {...handlers}
          role="application"
          aria-label={`${item.shortLabel} — sürükleyerek döndür`}
          className={`absolute inset-0 z-10 ${dragging ? "cursor-grabbing" : "cursor-grab"}`}
          style={{ touchAction: "pan-y" }}
        />

        <div className="shell pointer-events-none relative z-20 mx-auto flex h-full max-w-shell flex-col justify-between py-10 md:py-14">
          <div className="flex items-start justify-between gap-6">
            <p className="eyebrow text-paper/45">Açık artırmadaki parçalar</p>
            <p className="font-mono text-eyebrow uppercase tracking-[0.22em] text-paper/45">
              {String(index + 1).padStart(2, "0")} / {String(SHOWCASE.length).padStart(2, "0")}
            </p>
          </div>

          <div className="flex flex-col gap-10 md:flex-row md:items-end md:justify-between">
            <div className="max-w-[34rem]">
              <p className="eyebrow mb-4 text-sand">{item.category}</p>
              <h2
                key={item.slug}
                className="animate-veil-up font-display text-huge font-light leading-[0.92] text-paper"
              >
                {item.shortLabel}
              </h2>
              <p className="mt-5 font-sans text-sm leading-relaxed text-paper/55">
                {item.provenance}
              </p>
              <p
                className={`mt-7 flex items-center gap-2.5 font-mono text-eyebrow uppercase text-paper/30 transition-opacity duration-500 ${
                  dragging ? "opacity-0" : "opacity-100"
                }`}
              >
                <span aria-hidden className="text-sand/60">
                  ↔
                </span>
                Sürükleyerek döndür
              </p>
            </div>

            <div className="pointer-events-auto flex flex-col items-start gap-5 md:items-end">
              {liveAuction ? (
                <>
                  <div className="md:text-right">
                    <p className="eyebrow mb-2 text-paper/40">Güncel teklif</p>
                    <p className="font-display text-4xl font-light tabular-nums text-sand">
                      {formatMoney(liveAuction.currentPrice)}
                    </p>
                  </div>
                  <Link
                    to={`/auctions/${liveAuction.id}`}
                    className="btn border border-paper/25 text-paper transition-colors hover:border-sand hover:bg-sand hover:text-ink"
                  >
                    Teklif ver
                  </Link>
                </>
              ) : (
                <p className="max-w-[24ch] font-sans text-sm text-paper/40 md:text-right">
                  Bu parça için canlı kayıt yükleniyor.
                </p>
              )}
            </div>
          </div>

          <div className="mt-8 flex items-center gap-6">
            <div className="flex flex-1 gap-1.5">
              {SHOWCASE.map((entry, entryIndex) => (
                <button
                  key={entry.slug}
                  type="button"
                  onClick={() => goTo(entryIndex)}
                  aria-label={`${entry.shortLabel} parçasına git`}
                  aria-current={entryIndex === index}
                  className="pointer-events-auto group h-4 flex-1 pt-[7px]"
                >
                  <span className="block h-px w-full overflow-hidden bg-paper/15 transition-colors group-hover:bg-paper/35">
                    <span
                      className="block h-full bg-sand transition-[width] duration-200 ease-out"
                      style={{
                        width:
                          entryIndex < index
                            ? "100%"
                            : entryIndex === index
                              ? `${progress * 100}%`
                              : "0%",
                      }}
                    />
                  </span>
                </button>
              ))}
            </div>

            <div className="pointer-events-auto flex items-center gap-2">
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
          </div>
        </div>

      </div>
    </section>
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
      className="group flex h-12 w-12 items-center justify-center rounded-full border border-paper/20 transition-all duration-500 ease-editorial hover:border-sand hover:bg-sand disabled:pointer-events-none disabled:opacity-25"
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
