import { useLanguageStore, type Language } from "./store";
import { translateIn } from "./translate";
import type { TranslationKey } from "./tr";

const LOCALES: Record<Language, string> = { tr: "tr-TR", en: "en-US" };

const STATUS_KEYS: Record<string, TranslationKey> = {
  Scheduled: "status.Scheduled",
  Active: "status.Active",
  Ended: "status.Ended",
  Cancelled: "status.Cancelled",
};

export interface Formatters {
  money: (value: number) => string;
  moneyPrecise: (value: number) => string;
  dateTime: (iso: string) => string;
  time: (iso: string) => string;
  countdown: (msRemaining: number) => string;
  status: (status: string) => string;
}

function build(language: Language): Formatters {
  const locale = LOCALES[language];

  const currency = new Intl.NumberFormat(locale, {
    style: "currency",
    currency: "TRY",
    currencyDisplay: "narrowSymbol",
    maximumFractionDigits: 0,
  });

  const currencyPrecise = new Intl.NumberFormat(locale, {
    style: "currency",
    currency: "TRY",
    currencyDisplay: "narrowSymbol",
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

  const dateTime = new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "short",
  });

  const time = new Intl.DateTimeFormat(locale, { timeStyle: "medium" });

  const dayShort = translateIn(language, "format.dayShort");

  return {
    money: (value) => currency.format(value),
    moneyPrecise: (value) => currencyPrecise.format(value),
    dateTime: (iso) => dateTime.format(new Date(iso)),
    time: (iso) => time.format(new Date(iso)),
    countdown: (msRemaining) => {
      if (msRemaining <= 0) return "00:00:00";

      const total = Math.floor(msRemaining / 1000);
      const days = Math.floor(total / 86_400);
      const hours = Math.floor((total % 86_400) / 3600);
      const minutes = Math.floor((total % 3600) / 60);
      const seconds = total % 60;

      const pad = (n: number) => n.toString().padStart(2, "0");

      return days > 0
        ? `${days}${dayShort} ${pad(hours)}:${pad(minutes)}:${pad(seconds)}`
        : `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
    },
    status: (status) => {
      const key = STATUS_KEYS[status];
      return key ? translateIn(language, key) : status;
    },
  };
}

const cache = new Map<Language, Formatters>();

export function formattersFor(language: Language): Formatters {
  let formatters = cache.get(language);

  if (!formatters) {
    formatters = build(language);
    cache.set(language, formatters);
  }

  return formatters;
}

export function useFormat(): Formatters {
  return formattersFor(useLanguageStore((state) => state.language));
}
