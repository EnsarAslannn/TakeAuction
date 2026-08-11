const EDITORIAL_EASE = (t: number) => 1 - Math.pow(1 - t, 3);

let activeFrame = 0;

/**
 * Animated scroll driven by our own rAF loop.
 *
 * Lenis' `scrollTo` is deliberately not used here: with this page's setup it resolves
 * without ever moving the window, so programmatic navigation silently did nothing.
 * A plain tween works whether or not Lenis is mounted, and keeps the site's easing.
 */
export function smoothScrollTo(top: number, duration = 750) {
  if (activeFrame) cancelAnimationFrame(activeFrame);

  const from = window.scrollY;
  const max = document.documentElement.scrollHeight - window.innerHeight;
  const to = Math.min(Math.max(top, 0), Math.max(max, 0));
  const distance = to - from;

  if (Math.abs(distance) < 1) return;

  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    window.scrollTo(0, to);
    return;
  }

  const start = performance.now();

  const step = (now: number) => {
    const progress = Math.min(1, (now - start) / duration);
    window.scrollTo(0, from + distance * EDITORIAL_EASE(progress));

    if (progress < 1) {
      activeFrame = requestAnimationFrame(step);
    } else {
      activeFrame = 0;
    }
  };

  activeFrame = requestAnimationFrame(step);
}
