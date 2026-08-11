import { useEffect, useState } from "react";
import { getAuctions } from "@/api/auctions";
import { SHOWCASE, preload } from "@/content/preload";
import { Hero } from "@/sections/Hero";
import { LiveTicker } from "@/sections/LiveTicker";
import { ShowcaseScroll } from "@/sections/ShowcaseScroll";
import { Manifesto } from "@/sections/Manifesto";
import { HowItWorks } from "@/sections/HowItWorks";
import { Capabilities } from "@/sections/Capabilities";
import type { AuctionListItem } from "@/api/types";

export function Landing() {
  const [auctions, setAuctions] = useState<AuctionListItem[]>([]);

  useEffect(() => {
    getAuctions({ pageSize: 50 })
      .then((result) => setAuctions(result.items))
      .catch(() => setAuctions([]));
  }, []);

  useEffect(() => {
    preload(SHOWCASE.map((item) => item.model));
  }, []);

  useEffect(() => {
    const hash = window.location.hash;
    if (!hash) return;

    const target = document.querySelector(hash);
    if (target) {
      window.setTimeout(() => target.scrollIntoView({ behavior: "smooth" }), 400);
    }
  }, []);

  return (
    <>
      <Hero />
      <LiveTicker />
      <ShowcaseScroll auctions={auctions} />
      <Manifesto />
      <HowItWorks />
      <Capabilities />
    </>
  );
}
