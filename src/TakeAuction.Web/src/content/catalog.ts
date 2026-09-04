import type { TranslationKey } from "@/i18n";

export interface ShowcaseModel {
  slug: string;
  title: string;
  labelKey: TranslationKey;
  categoryKey: TranslationKey;
  provenanceKey: TranslationKey;
  model: string;
  card: string;
  scale: number;
  lift: number;
  spin: number;
}

export const SHOWCASE: ShowcaseModel[] = [
  {
    slug: "bmw-m5",
    title: "BMW M5 G90 — 2024 Sıfır Ayarında",
    labelKey: "catalog.bmw-m5.label",
    categoryKey: "catalog.bmw-m5.category",
    provenanceKey: "catalog.bmw-m5.provenance",
    model: "/models/bmw-m5.glb",
    card: "/cards/bmw-m5.webp",
    scale: 1.0,
    lift: 0,
    spin: -0.5,
  },
  {
    slug: "iphone-17",
    title: "iPhone 17 Pro Max — Mühürlü Kutu",
    labelKey: "catalog.iphone-17.label",
    categoryKey: "catalog.iphone-17.category",
    provenanceKey: "catalog.iphone-17.provenance",
    model: "/models/iphone-17.glb",
    card: "/cards/iphone-17.webp",
    scale: 1.0,
    lift: 0,
    spin: 0.6,
  },
  {
    slug: "canon-5d",
    title: "Canon EOS 5D Mark IV — Stüdyo Seti",
    labelKey: "catalog.canon-5d.label",
    categoryKey: "catalog.canon-5d.category",
    provenanceKey: "catalog.canon-5d.provenance",
    model: "/models/canon-5d.glb",
    card: "/cards/canon-5d.webp",
    scale: 1.0,
    lift: 0,
    spin: 0.7,
  },
  {
    slug: "vintage-sofa",
    title: "Vintage Deri Chesterfield Koltuk",
    labelKey: "catalog.vintage-sofa.label",
    categoryKey: "catalog.vintage-sofa.category",
    provenanceKey: "catalog.vintage-sofa.provenance",
    model: "/models/sofa.glb",
    card: "/cards/sofa.webp",
    scale: 1.0,
    lift: 0,
    spin: -0.35,
  },
  {
    slug: "satellite",
    title: "Referans Serisi Satellite Hoparlör",
    labelKey: "catalog.satellite.label",
    categoryKey: "catalog.satellite.category",
    provenanceKey: "catalog.satellite.provenance",
    model: "/models/satellite.glb",
    card: "/cards/satellite.webp",
    scale: 1.0,
    lift: 0,
    spin: 0.9,
  },
  {
    slug: "fridge",
    title: "Ankastre Panel Uyumlu Buzdolabı",
    labelKey: "catalog.fridge.label",
    categoryKey: "catalog.fridge.category",
    provenanceKey: "catalog.fridge.provenance",
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

export function showcaseForAuction(auction: { title: string }): ShowcaseModel | undefined {
  return showcaseForTitle(auction.title);
}

export const VISUALS = {
  hero: "/visuals/hero-atrium.webp",
  login: "/visuals/login-hall.webp",
  create: "/visuals/create-vitrine.webp",
  gallery: "/visuals/gallery-vitrine.webp",
  capabilities: "/visuals/capabilities-room.webp",
  realtime: "/visuals/realtime-signal.webp",
  vault: "/visuals/vault-archive.webp",
  plaster: "/visuals/texture-plaster.webp",
} as const;
