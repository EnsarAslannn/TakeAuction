import { useEffect, useState } from "react";

/**
 * Reports whether a section marked `data-nav-theme="dark"` currently sits under the
 * fixed header, so the nav can invert instead of laying a pale bar over dark artwork.
 * The observer root is collapsed to a thin band at the top of the viewport.
 */
export function useNavTheme(routeKey: string): "light" | "dark" {
  const [theme, setTheme] = useState<"light" | "dark">("light");

  useEffect(() => {
    const visible = new Set<Element>();
    const observed = new Set<Element>();

    const intersection = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) visible.add(entry.target);
          else visible.delete(entry.target);
        });
        setTheme(visible.size > 0 ? "dark" : "light");
      },
      { rootMargin: "0px 0px -94% 0px", threshold: 0 }
    );

    const sync = () => {
      const targets = new Set<Element>(document.querySelectorAll("[data-nav-theme='dark']"));

      observed.forEach((target) => {
        if (targets.has(target)) return;
        intersection.unobserve(target);
        observed.delete(target);
        visible.delete(target);
      });

      targets.forEach((target) => {
        if (observed.has(target)) return;
        intersection.observe(target);
        observed.add(target);
      });

      setTheme(visible.size > 0 ? "dark" : "light");
    };

    sync();

    // Auth-guarded and lazily loaded routes mount their dark sections well after this
    // effect first runs, so a one-shot query would never see them. Text-only updates
    // (countdowns, live bids) are characterData mutations and do not trigger this.
    const mutations = new MutationObserver(sync);
    mutations.observe(document.body, { childList: true, subtree: true });

    return () => {
      mutations.disconnect();
      intersection.disconnect();
    };
    // Re-scanned per route: the nav outlives the page under it, so the dark
    // sections it watches are swapped out from underneath it on navigation.
  }, [routeKey]);

  return theme;
}
