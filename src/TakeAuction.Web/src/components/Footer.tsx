import { Link } from "react-router-dom";
import { VISUALS } from "@/content/catalog";
import { useT, type TranslationKey } from "@/i18n";

const LINKS: { to: string; labelKey: TranslationKey }[] = [
  { to: "/auctions", labelKey: "footer.link.lots" },
  { to: "/#how-it-works", labelKey: "footer.link.how" },
  { to: "/#capabilities", labelKey: "footer.link.parties" },
  { to: "/register", labelKey: "footer.link.register" },
];

const RULES: TranslationKey[] = ["footer.rule1", "footer.rule2", "footer.rule3", "footer.rule4"];

export function Footer() {
  const t = useT();

  return (
    <footer data-nav-theme="dark" className="relative overflow-hidden bg-ink text-paper">
      <div className="absolute inset-0">
        <img
          src={VISUALS.plaster}
          alt=""
          aria-hidden
          loading="lazy"
          className="h-full w-full object-cover opacity-[0.12]"
        />
      </div>

      <div className="shell relative z-10 mx-auto max-w-shell py-24 md:py-32">
        <div className="flex flex-col gap-14 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="eyebrow mb-8 text-paper/40">{t("footer.enterHall")}</p>
            <h2 className="font-display text-giant font-light leading-[0.88]">
              take<span className="text-sand">auction</span>
            </h2>
          </div>

          <div className="flex flex-wrap gap-3">
            <Link
              to="/auctions"
              className="btn border border-paper/25 text-paper hover:border-sand hover:bg-sand hover:text-ink"
            >
              {t("footer.auctions")}
            </Link>
            <Link to="/register" className="btn bg-paper text-ink hover:bg-sand">
              {t("footer.openAccount")}
            </Link>
          </div>
        </div>

        <div className="mt-20 grid gap-12 border-t border-paper/12 pt-14 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)] md:gap-20">
          <div>
            <p className="eyebrow mb-6 text-paper/40">{t("footer.navigate")}</p>
            <ul className="space-y-3">
              {LINKS.map((link) => (
                <li key={link.to}>
                  <Link
                    to={link.to}
                    className="font-sans text-base text-paper/70 transition-colors hover:text-sand"
                  >
                    {t(link.labelKey)}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <p className="eyebrow mb-6 text-paper/40">{t("footer.beforeBidding")}</p>
            <ul className="space-y-4">
              {RULES.map((ruleKey) => (
                <li key={ruleKey} className="flex gap-4">
                  <span aria-hidden className="mt-2.5 h-px w-5 shrink-0 bg-sand/50" />
                  <span className="max-w-[52ch] font-sans text-sm leading-relaxed text-paper/60">
                    {t(ruleKey)}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </div>

        <p className="mt-16 border-t border-paper/12 pt-8 font-mono text-eyebrow uppercase text-paper/35">
          © {new Date().getFullYear()} TakeAuction
        </p>
      </div>
    </footer>
  );
}
