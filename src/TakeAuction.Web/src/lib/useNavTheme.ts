import { useEffect, useState } from "react";

/**
 * Reports whether a section marked `data-nav-theme="dark"` currently sits under the
 * fixed header, so the nav can invert instead of laying a pale bar over dark artwork.
 * The observer root is collapsed to a thin band at the top of the viewport.
 */
export function useNavTheme(): "light" | "dark" {
  const [theme, setTheme] = useState<"light" | "dark">("light");

  useEffect(() => {
    const targets = Array.from(document.querySelectorAll<HTMLElement>("[data-nav-theme='dark']"));
    if (targets.length === 0) {
      setTheme("light");
      return;
    }

    const visible = new Set<Element>();

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) visible.add(entry.target);
          else visible.delete(entry.target);
        });
        setTheme(visible.size > 0 ? "dark" : "light");
      },
      { rootMargin: "0px 0px -94% 0px", threshold: 0 }
    );

    targets.forEach((target) => observer.observe(target));
    return () => observer.disconnect();
  });

  return theme;
}
