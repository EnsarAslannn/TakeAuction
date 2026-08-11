import { Link } from "react-router-dom";
import { VISUALS } from "@/content/catalog";
import { SplitLine } from "@/motion/Reveal";

export function Hero() {
  return (
    <section className="relative min-h-[100svh] overflow-hidden bg-paper">
      <div className="absolute inset-0">
        <img
          src={VISUALS.hero}
          alt=""
          aria-hidden
          className="h-full w-full object-cover object-center opacity-[0.62]"
        />
        <div className="absolute inset-0 bg-gradient-to-b from-paper/70 via-paper/25 to-paper" />
        <div className="absolute inset-0 bg-gradient-to-r from-paper/60 via-transparent to-paper/40" />
      </div>

      <div className="grain absolute inset-0" />

      <div className="shell relative z-10 mx-auto flex min-h-[100svh] max-w-shell flex-col justify-between pb-12 pt-32 md:pt-40">
        <div className="flex items-start justify-between gap-8">
          <p className="eyebrow max-w-[16ch] leading-[1.9]">
            Gerçek zamanlı
            <br />
            açık artırma altyapısı
          </p>
          <p className="hidden max-w-[26ch] text-right font-sans text-sm leading-relaxed text-ink/70 md:block">
            Aynı saniyede gelen binlerce teklif, veritabanı seviyesinde çözülür. Kaybolan teklif yok,
            yanlış kazanan yok.
          </p>
        </div>

        <div className="mt-auto">
          <h1 className="font-display text-mega font-light text-ink">
            <SplitLine text="take" className="block" />
            <SplitLine text="auction" className="block pl-[0.08em] text-sand-deep" delay={120} />
          </h1>

          <div className="mt-10 flex flex-col gap-8 border-t border-ink/12 pt-8 md:flex-row md:items-end md:justify-between">
            <p className="max-w-[42ch] font-sans text-lg leading-relaxed text-ink/75">
              Beş nadir parça, tek bir canlı salonda. Her teklif anında herkese ulaşır — sayfa
              yenilemeden, gecikmesiz.
            </p>

            <div className="flex flex-wrap items-center gap-3">
              <Link to="/auctions" className="btn-primary">
                Salona gir
              </Link>
              <a href="#how-it-works" className="btn-ghost">
                Nasıl çalışır
              </a>
            </div>
          </div>
        </div>
      </div>

      <div className="pointer-events-none absolute bottom-8 left-1/2 z-10 -translate-x-1/2">
        <div className="h-14 w-px animate-drift bg-gradient-to-b from-transparent via-ink/30 to-transparent" />
      </div>
    </section>
  );
}
