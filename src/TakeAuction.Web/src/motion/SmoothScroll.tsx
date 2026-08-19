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

export function useScrollReset(key: string) {
  const lenis = useLenis();

  useEffect(() => {
    if (lenis) lenis.scrollTo(0, { immediate: true });
    else window.scrollTo(0, 0);
  }, [key, lenis]);
}

export function useHashScroll() {
  const { hash, key } = useLocation();
  const lenis = useLenis();

  useEffect(() => {
    if (!hash) return;

    const id = decodeURIComponent(hash.slice(1));

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
