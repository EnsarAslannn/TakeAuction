import { VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";

const CAPABILITIES = [
  {
    label: "Alıcı olarak",
    items: [
      "Canlı açık artırmaları filtrele, ara ve sayfa sayfa gez",
      "Minimum artış kuralına uyan teklifler ver",
      "Başkası seni geçtiğinde anında bildirim al",
      "Kalan süreyi saniye saniye izle",
    ],
  },
  {
    label: "Satıcı olarak",
    items: [
      "Başlangıç fiyatı ve minimum artış belirleyerek ilan aç",
      "Başlangıç ve bitiş zamanını dakikası dakikasına planla",
      "Kendi ilanına teklif verilmesi otomatik engellenir",
      "Kapanış ve kazanan tespiti tamamen otomatik",
    ],
  },
];

export function Capabilities() {
  return (
    <section id="capabilities" className="relative bg-paper-warm py-28 md:py-40">
      <div className="shell mx-auto max-w-shell">
        <div className="grid gap-16 md:grid-cols-12 md:gap-8">
          <div className="md:col-span-5">
            <p className="eyebrow">03 — Yapabildiklerin</p>
            <h2 className="mt-10 font-display text-giant font-light leading-[0.9] text-ink">
              <SplitLine text="iki taraf," className="block" />
              <SplitLine text="tek salon" className="block text-sand-deep" delay={90} />
            </h2>

            <Reveal delay={160}>
              <p className="mt-8 max-w-[38ch] font-sans text-base leading-relaxed text-ink/65">
                Aynı platform hem koleksiyonundan bir parça çıkarmak isteyen satıcıya, hem de o
                parçanın peşindeki alıcıya hizmet eder. Roller kayıt anında belirlenir ve yetkiler
                sunucu tarafında zorlanır.
              </p>
            </Reveal>

            <Reveal delay={240}>
              <div className="mt-12 aspect-[16/10] overflow-hidden">
                <img
                  src={VISUALS.capabilities}
                  alt="Spot ışıkları altında, kaide üzerinde sergilenen altın bir saat"
                  loading="lazy"
                  className="h-full w-full object-cover"
                />
              </div>
            </Reveal>
          </div>

          <div className="md:col-span-6 md:col-start-7">
            <div className="flex flex-col">
              {CAPABILITIES.map((group, groupIndex) => (
                <Reveal key={group.label} delay={groupIndex * 110}>
                  <div className="border-t border-ink/12 py-10 first:border-t-0 first:pt-0">
                    <p className="eyebrow mb-6 text-sand-deep">{group.label}</p>
                    <ul className="space-y-4">
                      {group.items.map((item) => (
                        <li key={item} className="flex gap-4">
                          <span
                            aria-hidden
                            className="mt-2.5 h-px w-5 shrink-0 bg-ink/25"
                          />
                          <span className="font-sans text-base leading-relaxed text-ink/80">
                            {item}
                          </span>
                        </li>
                      ))}
                    </ul>
                  </div>
                </Reveal>
              ))}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
