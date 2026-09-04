import { VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";
import { useT, type TranslationKey } from "@/i18n";

const STEPS: { number: string; titleKey: TranslationKey; bodyKey: TranslationKey }[] = [
  { number: "01", titleKey: "how.step1.title", bodyKey: "how.step1.body" },
  { number: "02", titleKey: "how.step2.title", bodyKey: "how.step2.body" },
  { number: "03", titleKey: "how.step3.title", bodyKey: "how.step3.body" },
  { number: "04", titleKey: "how.step4.title", bodyKey: "how.step4.body" },
];

export function HowItWorks() {
  const t = useT();

  return (
    <section
      id="how-it-works"
      data-nav-theme="dark"
      className="relative overflow-hidden bg-ink py-28 text-paper md:py-40"
    >
      <div className="absolute inset-0">
        <img
          src={VISUALS.realtime}
          alt=""
          aria-hidden
          loading="lazy"
          className="h-full w-full object-cover opacity-[0.22]"
        />
        <div className="absolute inset-0 bg-gradient-to-b from-ink via-ink/85 to-ink" />
      </div>

      <div className="shell relative z-10 mx-auto max-w-shell">
        <div className="flex items-baseline justify-between gap-8">
          <p className="eyebrow text-paper/40">{t("how.eyebrow")}</p>
          <p className="eyebrow hidden text-paper/40 md:block">{t("how.eyebrowRight")}</p>
        </div>

        <h2 className="mt-10 max-w-[16ch] font-display text-giant font-light leading-[0.9]">
          <SplitLine text={t("how.title1")} className="block" />
          <SplitLine text={t("how.title2")} className="block text-sand" delay={100} />
        </h2>

        <ol className="mt-20 grid gap-px border border-paper/10 bg-paper/10 md:grid-cols-2">
          {STEPS.map((step, index) => (
            <Reveal as="li" key={step.number} delay={index * 90} className="bg-ink">
              <div className="group h-full p-8 transition-colors duration-700 hover:bg-ink-soft md:p-12">
                <div className="flex items-start gap-6">
                  <span className="font-mono text-eyebrow text-sand">{step.number}</span>
                  <div>
                    <h3 className="font-display text-2xl font-light leading-tight text-paper md:text-3xl">
                      {t(step.titleKey)}
                    </h3>
                    <p className="mt-4 max-w-[42ch] font-sans text-sm leading-relaxed text-paper/55">
                      {t(step.bodyKey)}
                    </p>
                  </div>
                </div>
                <div className="mt-8 h-px w-0 bg-sand transition-[width] duration-[900ms] ease-editorial group-hover:w-full" />
              </div>
            </Reveal>
          ))}
        </ol>
      </div>
    </section>
  );
}
