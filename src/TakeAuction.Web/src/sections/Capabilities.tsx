import { VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";

const CAPABILITIES = [
  {
    label: "Alıcı olarak",
    items: [
      "Salondaki parçaları arayın, filtreleyin ve kalan süreye göre gezin",
      "Bazı parçaları üç boyutlu çevirip her açıdan inceleyin",
      "Teklifinizi verin, alt sınırı ekranda görün",
      "Biri sizi geçtiğinde sayfa yenilemeden anında öğrenin",
    ],
  },
  {
    label: "Satıcı olarak",
    items: [
      "Başlangıç fiyatını ve teklif adımını kendiniz belirleyin",
      "Açılış ve kapanış saatini dakikası dakikasına planlayın",
      "Teklifleri geldikçe canlı izleyin",
      "Kapanışta kazananı beklemeyin — sistem kendisi belirler",
    ],
  },
];

export function Capabilities() {
  return (
    <section id="capabilities" className="relative bg-paper-warm py-28 md:py-40">
      <div className="shell mx-auto max-w-shell">
        <div className="grid gap-16 md:grid-cols-12 md:gap-8">
          <div className="md:col-span-5">
            <p className="eyebrow">03 — İki taraf</p>
            <h2 className="mt-10 font-display text-giant font-light leading-[0.9] text-ink">
              <SplitLine text="iki taraf," className="block" />
              <SplitLine text="tek salon" className="block text-sand-deep" delay={90} />
            </h2>

            <Reveal delay={160}>
              <p className="mt-8 max-w-[38ch] font-sans text-base leading-relaxed text-ink/65">
                Aynı salon hem koleksiyonundan bir parça çıkarmak isteyene, hem de o parçanın
                peşinde olana açık. Hangi taraftan gireceğinizi kayıt olurken seçersiniz.
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
