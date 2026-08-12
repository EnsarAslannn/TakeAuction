import { Suspense, lazy, useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { SHOWCASE, showcaseForTitle } from "@/content/catalog";
import { useDragRotate } from "@/three/useDragRotate";
import { useIsVisible } from "@/lib/hooks";
import { formatMoney } from "@/lib/format";
import type { AuctionListItem } from "@/api/types";

// three.js, R3F and drei are the heaviest dependency in the app by a wide margin.
// Loading them lazily keeps them out of the landing page's critical path entirely —
// a visitor who never scrolls to the stage never pays for them.
const ShowcaseCanvas = lazy(() =>
  import("@/three/ShowcaseCanvas").then((module) => ({ default: module.ShowcaseCanvas }))
);

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
  const { ref: stageRef, visible } = useIsVisible<HTMLElement>();

  // Mounting the canvas is what pulls the first ~6MB model and opens the WebGL
  // context, so it waits until the stage is actually reached. It never unmounts
  // again — tearing the context down would re-download everything on the way back.
  const [armed, setArmed] = useState(false);
  useEffect(() => {
    if (visible) setArmed(true);
  }, [visible]);

  // A fresh model should start from its authored angle, not the previous one's.
  useEffect(() => reset(), [index, reset]);

  // The catalogue is ~28MB of geometry, so only the neighbours of the current lot
  // are warmed, and only after the current one has had a head start on the network.
  useEffect(() => {
    if (!visible) return;

    const neighbours = [index + 1, index - 1]
      .filter((target) => target >= 0 && target < SHOWCASE.length)
      .map((target) => SHOWCASE[target].model);

    const id = window.setTimeout(() => {
      void import("@/three/AuctionModel").then((module) => module.preloadShowcase(neighbours));
    }, 1500);
    return () => window.clearTimeout(id);
  }, [visible, index]);

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
      ref={stageRef}
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

      {armed && (
        <Suspense fallback={null}>
          <ShowcaseCanvas
            item={item}
            drag={dragState}
            onDecay={decay}
            active={visible}
            className="absolute inset-0"
          />
        </Suspense>
      )}

      {/* Drag surface. `pan-y` keeps vertical page scrolling available on touch
          while horizontal drags rotate the model. */}
      <div
        {...handlers}
        role="application"
        aria-label={`${item.shortLabel} — sürükleyerek döndürün`}
        className={`absolute inset-0 z-10 ${dragging ? "cursor-grabbing" : "cursor-grab"}`}
        style={{ touchAction: "pan-y" }}
      />

      <div className="shell pointer-events-none relative z-20 mx-auto flex h-full max-w-shell flex-col justify-between py-10 md:py-14">
        <div className="flex items-start justify-between gap-6">
          <p className="eyebrow text-paper/45">Yakından inceleyin</p>
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
              Sürükleyerek döndürün
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
                  Teklif verin
                </Link>
              </>
            ) : (
              <p className="max-w-[24ch] font-sans text-sm text-paper/40 md:text-right">
                Bu parçanın açık artırması yükleniyor.
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
                aria-label={`${entry.shortLabel} parçasını gösterin`}
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
