import { SHOWCASE, VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";
import { useT, type TranslationKey } from "@/i18n";

const STATS: { valueKey?: TranslationKey; value?: string; labelKey: TranslationKey }[] = [
  { value: String(SHOWCASE.length), labelKey: "manifesto.stat.lots" },
  { valueKey: "manifesto.stat.updatesValue", labelKey: "manifesto.stat.updates" },
  { value: "0", labelKey: "manifesto.stat.lostBids" },
  { valueKey: "manifesto.stat.openValue", labelKey: "manifesto.stat.open" },
];

export function Manifesto() {
  const t = useT();

  return (
    <section id="about" className="relative overflow-hidden bg-paper-warm py-28 md:py-40">
      <div className="shell mx-auto max-w-shell">
        <div className="flex items-baseline justify-between gap-8">
          <p className="eyebrow">{t("manifesto.eyebrow")}</p>
          <p className="eyebrow hidden md:block">{t("manifesto.eyebrowRight")}</p>
        </div>

        <h2 className="mt-10 max-w-[20ch] font-display text-giant font-light leading-[0.9] text-ink">
          <SplitLine text={t("manifesto.title1")} className="block" />
          <SplitLine text={t("manifesto.title2")} className="block text-stone" delay={90} />
          <SplitLine text={t("manifesto.title3")} className="block text-sand-deep" delay={180} />
        </h2>

        <div className="mt-16 grid gap-12 md:grid-cols-12 md:gap-8">
          <Reveal className="md:col-span-5">
            <div className="relative aspect-[4/5] overflow-hidden">
              <img
                src={VISUALS.vault}
                alt={t("manifesto.vaultAlt")}
                loading="lazy"
                className="h-full w-full object-cover transition-transform duration-[1400ms] ease-editorial hover:scale-[1.04]"
              />
            </div>
          </Reveal>

          <div className="md:col-span-6 md:col-start-7">
            <Reveal delay={80}>
              <p className="font-sans text-2xl font-light leading-[1.5] text-ink md:text-[1.75rem]">
                {t("manifesto.lede")}
              </p>
            </Reveal>

            <Reveal delay={160}>
              <p className="mt-8 font-sans text-base leading-relaxed text-ink/65">
                {t("manifesto.body")}
              </p>
            </Reveal>

            <Reveal delay={240}>
              <dl className="mt-12 grid grid-cols-2 gap-x-8 gap-y-10 border-t border-ink/12 pt-10">
                {STATS.map((stat) => (
                  <div key={stat.labelKey}>
                    <dt className="font-display text-4xl font-light tabular-nums text-ink">
                      {stat.valueKey ? t(stat.valueKey) : stat.value}
                    </dt>
                    <dd className="mt-2 font-mono text-eyebrow uppercase text-stone">
                      {t(stat.labelKey)}
                    </dd>
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
