export interface ShowcaseModel {
  slug: string;
  /** Must match the seeded auction title exactly. */
  title: string;
  shortLabel: string;
  category: string;
  provenance: string;
  model: string;
  /** Product photo used on the hero carousel card. */
  card: string;
  /** Uniform scale applied after auto-centering. */
  scale: number;
  /** Extra Y offset in normalised units, applied after centering. */
  lift: number;
  /** Initial Y rotation in radians. */
  spin: number;
}

export const SHOWCASE: ShowcaseModel[] = [
  {
    slug: "bmw-m5",
    title: "BMW M5 G90 — 2024 Sıfır Ayarında",
    shortLabel: "M5 G90",
    category: "Otomotiv",
    provenance: "Tek sahipli · Tam servis geçmişi",
    model: "/models/bmw-m5.glb",
    card: "/cards/bmw-m5.webp",
    scale: 1.0,
    lift: 0,
    spin: -0.5,
  },
  {
    slug: "iphone-17",
    title: "iPhone 17 Pro Max — Mühürlü Kutu",
    shortLabel: "iPhone 17 Pro Max",
    category: "Teknoloji",
    provenance: "Mühürlü kutu · Aktifleştirilmemiş",
    model: "/models/iphone-17.glb",
    card: "/cards/iphone-17.webp",
    scale: 1.0,
    lift: 0,
    spin: 0.6,
  },
  {
    slug: "canon-5d",
    title: "Canon EOS 5D Mark IV — Stüdyo Seti",
    shortLabel: "EOS 5D IV",
    category: "Optik",
    provenance: "Stüdyo bakımlı · 40 bin altı deklanşör",
    model: "/models/canon-5d.glb",
    card: "/cards/canon-5d.webp",
    scale: 1.0,
    lift: 0,
    spin: 0.7,
  },
  {
    slug: "vintage-sofa",
    title: "Vintage Deri Chesterfield Koltuk",
    shortLabel: "Chesterfield",
    category: "Mobilya",
    provenance: "Mid-century · Orijinal patina",
    model: "/models/sofa.glb",
    card: "/cards/sofa.webp",
    scale: 1.0,
    lift: 0,
    spin: -0.35,
  },
  {
    slug: "satellite",
    title: "Referans Serisi Satellite Hoparlör",
    shortLabel: "Satellite Hoparlör",
    category: "Ses Sistemi",
    provenance: "Stüdyo referansı · Bi-wiring destekli",
    model: "/models/satellite.glb",
    card: "/cards/satellite.webp",
    scale: 1.0,
    lift: 0,
    spin: 0.9,
  },
  {
    slug: "fridge",
    title: "Ankastre Panel Uyumlu Buzdolabı",
    shortLabel: "Kolon Buzdolabı",
    category: "Beyaz Eşya",
    provenance: "Teşhir ünitesi · Garantili",
    model: "/models/fridge.glb",
    card: "/cards/fridge.webp",
    scale: 1.0,
    lift: 0,
    spin: 0.25,
  },
];

const BY_TITLE = new Map(SHOWCASE.map((item) => [item.title.toLowerCase(), item]));
const BY_SLUG = new Map(SHOWCASE.map((item) => [item.slug, item]));

export function showcaseForTitle(title: string): ShowcaseModel | undefined {
  return BY_TITLE.get(title.trim().toLowerCase());
}

export function showcaseForSlug(slug: string): ShowcaseModel | undefined {
  return BY_SLUG.get(slug);
}

/**
 * Auctions the user creates themselves have no bespoke model, so they fall back to
 * a deterministic pick keyed off the auction id — stable across renders and reloads.
 */
export function showcaseForAuction(auction: { id: string; title: string }): ShowcaseModel {
  const exact = showcaseForTitle(auction.title);
  if (exact) return exact;

  let hash = 0;
  for (let i = 0; i < auction.id.length; i += 1) {
    hash = (hash * 31 + auction.id.charCodeAt(i)) >>> 0;
  }
  return SHOWCASE[hash % SHOWCASE.length];
}

export const VISUALS = {
  hero: "/visuals/hero-atrium.webp",
  login: "/visuals/login-hall.webp",
  gallery: "/visuals/gallery-vitrine.webp",
  capabilities: "/visuals/capabilities-room.webp",
  realtime: "/visuals/realtime-signal.webp",
  vault: "/visuals/vault-archive.webp",
  plaster: "/visuals/texture-plaster.webp",
} as const;
