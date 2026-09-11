import type { Formatters, Translate, TranslationKey } from "@/i18n";
import type { NotificationKind, UserNotification } from "@/api/types";

const COPY: Record<NotificationKind, { title: TranslationKey; body: TranslationKey }> = {
  AuctionWon: { title: "notify.won.title", body: "notify.won.body" },
  AuctionSold: { title: "notify.sold.title", body: "notify.sold.body" },
  AuctionUnsold: { title: "notify.unsold.title", body: "notify.unsold.body" },
  AuctionClosingSoon: { title: "notify.closingSoon.title", body: "notify.closingSoon.body" },
};

export interface NotificationCopy {
  title: string;
  body: string;
}

export function describeNotification(
  notification: UserNotification,
  t: Translate,
  format: Pick<Formatters, "money" | "clock">
): NotificationCopy {
  const copy = COPY[notification.kind];

  return {
    title: t(copy.title),
    body: t(copy.body, {
      title: notification.auctionTitle,
      price: notification.amount === null ? "" : format.money(notification.amount),
      time: format.clock(notification.auctionEndsAtUtc),
    }),
  };
}
