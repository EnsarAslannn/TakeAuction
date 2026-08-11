const currency = new Intl.NumberFormat("tr-TR", {
  style: "currency",
  currency: "TRY",
  maximumFractionDigits: 0,
});

const currencyPrecise = new Intl.NumberFormat("tr-TR", {
  style: "currency",
  currency: "TRY",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const dateTime = new Intl.DateTimeFormat("tr-TR", {
  dateStyle: "medium",
  timeStyle: "short",
});

export const formatMoney = (value: number) => currency.format(value);
export const formatMoneyPrecise = (value: number) => currencyPrecise.format(value);
export const formatDateTime = (iso: string) => dateTime.format(new Date(iso));

export function formatCountdown(msRemaining: number): string {
  if (msRemaining <= 0) return "00:00:00";

  const total = Math.floor(msRemaining / 1000);
  const days = Math.floor(total / 86_400);
  const hours = Math.floor((total % 86_400) / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = total % 60;

  const pad = (n: number) => n.toString().padStart(2, "0");

  return days > 0
    ? `${days}g ${pad(hours)}:${pad(minutes)}:${pad(seconds)}`
    : `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}

export const STATUS_LABEL: Record<string, string> = {
  Scheduled: "Planlandı",
  Active: "Canlı",
  Ended: "Sona erdi",
  Cancelled: "İptal edildi",
};
