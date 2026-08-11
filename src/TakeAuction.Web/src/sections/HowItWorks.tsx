import { VISUALS } from "@/content/catalog";
import { Reveal, SplitLine } from "@/motion/Reveal";

const STEPS = [
  {
    number: "01",
    title: "Kaydol ve salona gir",
    body: "Alıcı ya da satıcı olarak hesap açarsın. Oturum, JavaScript'in okuyamadığı HttpOnly bir çerezde taşınır; her yazma isteği ayrıca CSRF çift-gönderim doğrulamasından geçer.",
  },
  {
    number: "02",
    title: "Teklifini bırak",
    body: "Teklifin sunucuya gider ve açık artırma satırının sürüm numarasıyla birlikte yazılır. Aynı anda başkası kazandıysa senin isteğin reddedilir, sistem otomatik yeniden dener.",
  },
  {
    number: "03",
    title: "Herkes aynı anda görür",
    body: "Kabul edilen teklif bir domain olayı yayar. SignalR bunu o açık artırmayı izleyen herkese anında iletir — sayfa yenilemek yok, sorgulama (polling) yok.",
  },
  {
    number: "04",
    title: "Süre dolunca otomatik kapanır",
    body: "Arka planda çalışan Hangfire işi süresi biten açık artırmaları kapatır, kazananı belirler ve sonucu RabbitMQ üzerinden dış sistemlere duyurur. Kimsenin butona basması gerekmez.",
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
