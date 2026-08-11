import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { SHOWCASE, showcaseForTitle } from "@/content/catalog";
import { ShowcaseCanvas } from "@/three/ShowcaseCanvas";
import { useDragRotate } from "@/three/useDragRotate";
import { formatMoney } from "@/lib/format";
import type { AuctionListItem } from "@/api/types";

interface ShowcaseProps {
  auctions: AuctionListItem[];
}

/**
 * The 3D stage. Where the hero is for browsing the catalogue, this is for
 * inspecting one lot: the real GLB model, rotatable by dragging. Selection is
 * driven only by the arrows, so page scroll passes through untouched.
 */
export function Showcase({ auctions }: ShowcaseProps) {
  const [index, setIndex] = useState(0);
  const { state: dragState, dragging, reset, decay, handlers } = useDragRotate();

  // A fresh model should start from its authored angle, not the previous one's.
  useEffect(() => reset(), [index, reset]);

  const goTo = useCallback(
    (target: number) => setIndex(Math.min(SHOWCASE.length - 1, Math.max(0, target))),
    []
  );

  const item = SHOWCASE[index];
  const live = auctions.find((auction) => showcaseForTitle(auction.title)?.slug === item.slug);
  const atStart = index === 0;
  const atEnd = index === SHOWCASE.length - 1;

  return (
    <section
      id="showcase"
      data-nav-theme="dark"
      className="relative h-[100svh] overflow-hidden bg-ink text-paper"
    >
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 opacity-[0.35]"
        style={{
          background:
            "radial-gradient(70% 55% at 50% 42%, rgba(192,160,112,0.28) 0%, transparent 70%)",
        }}
      />

      <ShowcaseCanvas item={item} drag={dragState} onDecay={decay} className="absolute inset-0" />

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
          <p className="eyebrow text-paper/45">Yakından incele</p>
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
            <p className="mt-5 font-sans text-sm leading-relaxed text-paper/55">{item.provenance}</p>
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
            {live ? (
              <>
                <div className="md:text-right">
                  <p className="eyebrow mb-2 text-paper/40">Güncel teklif</p>
                  <p className="font-display text-4xl font-light tabular-nums text-sand">
                    {formatMoney(live.currentPrice)}
                  </p>
                </div>
                <Link
                  to={`/auctions/${live.id}`}
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
                aria-label={`${entry.shortLabel} parçasını göster`}
                aria-current={entryIndex === index}
                className="pointer-events-auto group h-4 flex-1 pt-[7px]"
              >
                <span className="block h-px w-full overflow-hidden bg-paper/15 transition-colors group-hover:bg-paper/35">
                  <span
                    className="block h-full bg-sand transition-[width] duration-500 ease-editorial"
                    style={{ width: entryIndex === index ? "100%" : "0%" }}
                  />
                </span>
              </button>
            ))}
          </div>

          <div className="pointer-events-auto flex items-center gap-2">
            <ShowcaseArrow
              direction="prev"
              disabled={atStart}
              onClick={() => goTo(index - 1)}
              label="Önceki modeli göster"
            />
            <ShowcaseArrow
              direction="next"
              disabled={atEnd}
              onClick={() => goTo(index + 1)}
              label="Sonraki modeli göster"
            />
          </div>
        </div>
      </div>
    </section>
  );
}

function ShowcaseArrow({
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
