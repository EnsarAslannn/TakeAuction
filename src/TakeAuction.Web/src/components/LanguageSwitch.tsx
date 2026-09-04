import { useLanguageStore, useT, type Language } from "@/i18n";

const OPTIONS: { code: Language; short: string; labelKey: "lang.tr" | "lang.en" }[] = [
  { code: "tr", short: "TR", labelKey: "lang.tr" },
  { code: "en", short: "EN", labelKey: "lang.en" },
];

interface LanguageSwitchProps {
  dark?: boolean;
  className?: string;
}

export function LanguageSwitch({ dark = false, className = "" }: LanguageSwitchProps) {
  const language = useLanguageStore((state) => state.language);
  const setLanguage = useLanguageStore((state) => state.setLanguage);
  const t = useT();

  return (
    <div
      role="group"
      aria-label={t("lang.switch")}
      className={`flex shrink-0 items-center gap-1.5 ${className}`}
    >
      {OPTIONS.map((option, index) => (
        <span key={option.code} className="flex items-center gap-1.5">
          {index > 0 && (
            <span aria-hidden className={dark ? "text-paper/25" : "text-ink/20"}>
              |
            </span>
          )}
          <button
            type="button"
            lang={option.code}
            onClick={() => setLanguage(option.code)}
            aria-pressed={language === option.code}
            aria-label={t(option.labelKey)}
            className={`font-mono text-eyebrow uppercase transition-colors duration-500 ${
              language === option.code
                ? dark
                  ? "text-sand"
                  : "text-sand-deep"
                : dark
                  ? "text-paper/45 hover:text-paper"
                  : "text-stone hover:text-ink"
            }`}
          >
            {option.short}
          </button>
        </span>
      ))}
    </div>
  );
}
