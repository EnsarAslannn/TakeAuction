import { preloadShowcase } from "@/three/AuctionModel";

export { SHOWCASE } from "./catalog";

/**
 * Warms the GLTF cache after first paint. Models are pulled one at a time so the
 * first showcase item is interactive long before the heavier ones finish.
 */
export function preload(urls: string[]) {
  let index = 0;

  const next = () => {
    if (index >= urls.length) return;
    preloadShowcase([urls[index]]);
    index += 1;
    window.setTimeout(next, 600);
  };

  const start = () => window.setTimeout(next, 1200);

  if ("requestIdleCallback" in window) {
    (window as unknown as { requestIdleCallback: (cb: () => void) => void }).requestIdleCallback(start);
  } else {
    start();
  }
}
