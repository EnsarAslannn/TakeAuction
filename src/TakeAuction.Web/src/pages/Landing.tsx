import { useEffect, useState } from "react";
import { getAuctions } from "@/api/auctions";
import { Hero } from "@/sections/Hero";
import { LiveTicker } from "@/sections/LiveTicker";
import { Showcase } from "@/sections/Showcase";
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
    const hash = window.location.hash;
    if (!hash) return;

    const target = document.querySelector(hash);
    if (target) {
      window.setTimeout(() => target.scrollIntoView({ behavior: "smooth" }), 400);
    }
  }, []);

  return (
    <>
      <Hero auctions={auctions} />
      <LiveTicker />
      <Showcase auctions={auctions} />
      <Manifesto />
      <HowItWorks />
      <Capabilities />
    </>
  );
}
