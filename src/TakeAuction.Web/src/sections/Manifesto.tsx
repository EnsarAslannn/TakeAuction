import { SHOWCASE, VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";

export function Manifesto() {
  return (
    <section id="about" className="relative overflow-hidden bg-paper py-28 md:py-40">
      <div className="shell mx-auto max-w-shell">
        <div className="flex items-baseline justify-between gap-8">
          <p className="eyebrow">01 — Salon</p>
          <p className="eyebrow hidden md:block">Neden burada</p>
        </div>

        <h2 className="mt-10 max-w-[20ch] font-display text-giant font-light leading-[0.9] text-ink">
          <SplitLine text="her parçanın" className="block" />
          <SplitLine text="bir hikâyesi," className="block text-stone" delay={90} />
          <SplitLine text="bir sahibi var" className="block text-sand-deep" delay={180} />
        </h2>

        <div className="mt-16 grid gap-12 md:grid-cols-12 md:gap-8">
          <Reveal className="md:col-span-5">
            <div className="relative aspect-[4/5] overflow-hidden">
              <img
                src={VISUALS.vault}
                alt="Sessiz bir özel koleksiyon kasası"
                loading="lazy"
                className="h-full w-full object-cover transition-transform duration-[1400ms] ease-editorial hover:scale-[1.04]"
              />
            </div>
          </Reveal>

          <div className="md:col-span-6 md:col-start-7">
            <Reveal delay={80}>
              <p className="font-sans text-2xl font-light leading-[1.5] text-ink md:text-[1.75rem]">
                Buraya her gün yüzlerce ilan girmez. Salona alınan her parça tek tek seçilir —
                sayısı azdır, bu yüzden karşındaki rekabet gerçektir.
              </p>
            </Reveal>

            <Reveal delay={160}>
              <p className="mt-8 font-sans text-base leading-relaxed text-ink/65">
                Bir açık artırmanın en sinir bozucu yanı, son saniyede ne olduğunu bilememektir.
                Burada her teklif geldiği anda sıraya girer ve salondaki herkesin ekranında aynı
                anda görünür. Sayaç sıfırlandığında açık artırma kendiliğinden kapanır: uzatma yok,
                pazarlık yok, en yüksek teklif kazanır.
              </p>
            </Reveal>

            <Reveal delay={240}>
              <dl className="mt-12 grid grid-cols-2 gap-x-8 gap-y-10 border-t border-ink/12 pt-10">
                {[
                  { value: String(SHOWCASE.length), label: "Salondaki parça" },
                  { value: "Anlık", label: "Fiyat güncellemesi" },
                  { value: "0", label: "Kaybolan teklif" },
                  { value: "7/24", label: "Salon açık" },
                ].map((stat) => (
                  <div key={stat.label}>
                    <dt className="font-display text-4xl font-light tabular-nums text-ink">
                      {stat.value}
                    </dt>
                    <dd className="mt-2 font-mono text-eyebrow uppercase text-stone">{stat.label}</dd>
                  </div>
                ))}
              </dl>
            </Reveal>
          </div>
        </div>
      </div>
    </section>
  );
}
