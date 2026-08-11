import { VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";

export function Manifesto() {
  return (
    <section id="about" className="relative overflow-hidden bg-paper py-28 md:py-40">
      <div className="shell mx-auto max-w-shell">
        <div className="flex items-baseline justify-between gap-8">
          <p className="eyebrow">01 — Amaç</p>
          <p className="eyebrow hidden md:block">Neden var</p>
        </div>

        <h2 className="mt-10 max-w-[20ch] font-display text-giant font-light leading-[0.9] text-ink">
          <SplitLine text="açık artırma" className="block" />
          <SplitLine text="zor kısmı" className="block text-stone" delay={90} />
          <SplitLine text="kalabalıktır" className="block text-sand-deep" delay={180} />
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
                Bir açık artırmanın son on saniyesinde yüzlerce kişi aynı anda teklif verir. Klasik
                bir CRUD uygulaması burada sessizce yanlış cevap üretir: iki teklif birbirinin
                üzerine yazar, biri kaybolur, yanlış kişi kazanır.
              </p>
            </Reveal>

            <Reveal delay={160}>
              <p className="mt-8 font-sans text-base leading-relaxed text-ink/65">
                TakeAuction bu problemi uygulama katmanında değil, veritabanı katmanında çözer.
                PostgreSQL üzerinde satır sürümüne dayalı iyimser eşzamanlılık kontrolü (optimistic
                concurrency) kullanılır: iki teklif aynı satıra çarptığında biri reddedilir ve
                yeniden denenir. Kimse sessizce ezilmez.
              </p>
            </Reveal>

            <Reveal delay={240}>
              <dl className="mt-12 grid grid-cols-2 gap-x-8 gap-y-10 border-t border-ink/12 pt-10">
                {[
                  { value: "5", label: "Canlı parça" },
                  { value: "< 100ms", label: "Teklif yayılma hedefi" },
                  { value: "0", label: "Kaybolan teklif" },
                  { value: "24/7", label: "Otonom kapanış" },
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
