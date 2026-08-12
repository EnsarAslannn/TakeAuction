import { VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";

const STEPS = [
  {
    number: "01",
    title: "Hesabını aç",
    body: "Bir e-posta ve parola yeter. Alıcı olarak girersen salondaki her parçaya teklif verebilirsin; satıcı olarak girersen kendi parçanı sergilemeye hemen başlarsın.",
  },
  {
    number: "02",
    title: "Parçayı incele",
    body: "Her ilanda güncel fiyatı, o ana kadar kaç teklif geldiğini ve kapanışa ne kadar kaldığını görürsün. Bazı parçaları üç boyutlu olarak çevirip her açıdan inceleyebilirsin.",
  },
  {
    number: "03",
    title: "Teklifini ver",
    body: "Teklifin, güncel fiyatın belirli bir tutar üzerinde olmalı — bu alt sınır ilanda yazar. Teklifin kabul edildiği anda salondaki herkesin ekranında fiyat değişir; sayfayı yenilemene gerek kalmaz.",
  },
  {
    number: "04",
    title: "Sayaç sıfırlanır, kazanan belli olur",
    body: "Süre dolduğu anda açık artırma kendiliğinden kapanır ve en yüksek teklif kazanır. Son saniyede gelen teklifler de sıraya doğru girer; kimsenin bir butona basması gerekmez.",
  },
];

export function HowItWorks() {
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
          <p className="eyebrow text-paper/40">02 — Nasıl çalışır</p>
          <p className="eyebrow hidden text-paper/40 md:block">Dört adım</p>
        </div>

        <h2 className="mt-10 max-w-[16ch] font-display text-giant font-light leading-[0.9]">
          <SplitLine text="teklif ver." className="block" />
          <SplitLine text="anında yayılır." className="block text-sand" delay={100} />
        </h2>

        <ol className="mt-20 grid gap-px border border-paper/10 bg-paper/10 md:grid-cols-2">
          {STEPS.map((step, index) => (
            <Reveal as="li" key={step.number} delay={index * 90} className="bg-ink">
              <div className="group h-full p-8 transition-colors duration-700 hover:bg-ink-soft md:p-12">
                <div className="flex items-start gap-6">
                  <span className="font-mono text-eyebrow text-sand">{step.number}</span>
                  <div>
                    <h3 className="font-display text-2xl font-light leading-tight text-paper md:text-3xl">
                      {step.title}
                    </h3>
                    <p className="mt-4 max-w-[42ch] font-sans text-sm leading-relaxed text-paper/55">
                      {step.body}
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
