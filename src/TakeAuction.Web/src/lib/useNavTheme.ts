import { useEffect, useState } from "react";

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

    const mutations = new MutationObserver(sync);
    mutations.observe(document.body, { childList: true, subtree: true });

    return () => {
      mutations.disconnect();
      intersection.disconnect();
    };
  }, [routeKey]);

  return theme;
}
