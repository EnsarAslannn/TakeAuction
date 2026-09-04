import { VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";
import { useT, type TranslationKey } from "@/i18n";

const CAPABILITIES: { labelKey: TranslationKey; itemKeys: TranslationKey[] }[] = [
  {
    labelKey: "cap.buyer.label",
    itemKeys: ["cap.buyer.item1", "cap.buyer.item2", "cap.buyer.item3", "cap.buyer.item4"],
  },
  {
    labelKey: "cap.seller.label",
    itemKeys: ["cap.seller.item1", "cap.seller.item2", "cap.seller.item3", "cap.seller.item4"],
  },
];

export function Capabilities() {
  const t = useT();

  return (
    <section id="capabilities" className="relative bg-paper-warm py-28 md:py-40">
      <div className="shell mx-auto max-w-shell">
        <div className="grid gap-16 md:grid-cols-12 md:gap-8">
          <div className="md:col-span-5">
            <p className="eyebrow">{t("cap.eyebrow")}</p>
            <h2 className="mt-10 font-display text-giant font-light leading-[0.9] text-ink">
              <SplitLine text={t("cap.title1")} className="block" />
              <SplitLine text={t("cap.title2")} className="block text-sand-deep" delay={90} />
            </h2>

            <Reveal delay={160}>
              <p className="mt-8 max-w-[38ch] font-sans text-base leading-relaxed text-ink/65">
                {t("cap.lede")}
              </p>
            </Reveal>

            <Reveal delay={240}>
              <div className="mt-12 aspect-[16/10] overflow-hidden">
                <img
                  src={VISUALS.capabilities}
                  alt={t("cap.imageAlt")}
                  loading="lazy"
                  className="h-full w-full object-cover"
                />
              </div>
            </Reveal>
          </div>

          <div className="md:col-span-6 md:col-start-7">
            <div className="flex flex-col">
              {CAPABILITIES.map((group, groupIndex) => (
                <Reveal key={group.labelKey} delay={groupIndex * 110}>
                  <div className="border-t border-ink/12 py-10 first:border-t-0 first:pt-0">
                    <p className="eyebrow mb-6 text-sand-deep">{t(group.labelKey)}</p>
                    <ul className="space-y-4">
                      {group.itemKeys.map((itemKey) => (
                        <li key={itemKey} className="flex gap-4">
                          <span aria-hidden className="mt-2.5 h-px w-5 shrink-0 bg-ink/25" />
                          <span className="font-sans text-base leading-relaxed text-ink/80">
                            {t(itemKey)}
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
