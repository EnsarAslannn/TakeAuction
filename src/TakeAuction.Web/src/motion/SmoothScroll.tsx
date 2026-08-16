import { createContext, useContext, useEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";
import Lenis from "lenis";
import { usePrefersReducedMotion } from "@/lib/hooks";

const LenisContext = createContext<Lenis | null>(null);

const SETTLE_MS = 1500;

export const useLenis = () => useContext(LenisContext);

export function SmoothScroll({ children }: { children: React.ReactNode }) {
  const reducedMotion = usePrefersReducedMotion();
  const [lenis, setLenis] = useState<Lenis | null>(null);
  const rafRef = useRef<number>();

  useEffect(() => {
    if (reducedMotion) {
      setLenis(null);
      return;
    }

    const instance = new Lenis({
      duration: 1.15,
      easing: (t) => Math.min(1, 1.001 - Math.pow(2, -10 * t)),
      smoothWheel: true,
      wheelMultiplier: 0.9,
      touchMultiplier: 1.4,
    });

    setLenis(instance);

    const raf = (time: number) => {
      instance.raf(time);
      rafRef.current = requestAnimationFrame(raf);
    };
    rafRef.current = requestAnimationFrame(raf);

    return () => {
      if (rafRef.current) cancelAnimationFrame(rafRef.current);
      instance.destroy();
      setLenis(null);
    };
  }, [reducedMotion]);

  return <LenisContext.Provider value={lenis}>{children}</LenisContext.Provider>;
}

/** Resets scroll position on route change, bypassing Lenis' animated scroll. */
export function useScrollReset(key: string) {
  const lenis = useLenis();

  useEffect(() => {
    if (lenis) lenis.scrollTo(0, { immediate: true });
    else window.scrollTo(0, 0);
  }, [key, lenis]);
}

/**
 * Scrolls to the section named by the URL hash after every navigation carrying one.
 * Keyed on `location.key` so repeat clicks on the link for the section we are already
 * looking at still scroll, and so hash-only navigations (which leave the route mounted)
 * are not missed.
 */
export function useHashScroll() {
  const { hash, key } = useLocation();
  const lenis = useLenis();

  useEffect(() => {
    if (!hash) return;

    const id = decodeURIComponent(hash.slice(1));

    // The landing page keeps growing while its sections and imagery settle, so a single
    // scroll issued on arrival either aims at a stale offset or gets clamped to a document
    // that is still short. Re-aim for as long as the layout is still moving.
    const deadline = performance.now() + SETTLE_MS;
    let aimedAt = "";
    let frame = window.requestAnimationFrame(function aim() {
      const target = document.getElementById(id);

      if (target) {
        const top = Math.round(target.getBoundingClientRect().top + window.scrollY);
        const signature = `${top}/${document.documentElement.scrollHeight}`;

        if (signature !== aimedAt) {
          aimedAt = signature;
          if (lenis) {
            // Lenis caches the scrollable limit and refreshes it asynchronously, so a
            // scroll issued right after the page grew would be clamped to the old, much
            // shorter document and stop far above the section.
            lenis.resize();
            lenis.scrollTo(target);
          } else {
            target.scrollIntoView({ behavior: "smooth" });
          }
        }
      }

      if (performance.now() < deadline) frame = window.requestAnimationFrame(aim);
    });

    return () => window.cancelAnimationFrame(frame);
  }, [hash, key, lenis]);
}
