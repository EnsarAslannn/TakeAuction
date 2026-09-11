import { beforeEach, describe, expect, it, vi } from "vitest";
import * as notificationsApi from "@/api/notifications";
import type { UserNotification } from "@/api/types";
import { INBOX_LENGTH, useNotificationStore } from "./notificationStore";

function notification(overrides: Partial<UserNotification> = {}): UserNotification {
  return {
    id: crypto.randomUUID(),
    kind: "AuctionWon",
    auctionId: "auction-1",
    auctionTitle: "Rare stamp collection",
    amount: 250,
    auctionEndsAtUtc: "2026-09-11T12:00:00Z",
    createdAtUtc: "2026-09-11T12:00:05Z",
    readAtUtc: null,
    ...overrides,
  };
}

beforeEach(() => useNotificationStore.getState().reset());

describe("receiving a pushed notification", () => {
  it("puts it at the top and counts it as unread", () => {
    const older = notification();
    const newer = notification();

    useNotificationStore.getState().receive(older);
    useNotificationStore.getState().receive(newer);

    const state = useNotificationStore.getState();
    expect(state.items.map((item) => item.id)).toEqual([newer.id, older.id]);
    expect(state.unreadCount).toBe(2);
  });

  // The hub can deliver the same push twice around a reconnect; the badge must not.
  it("ignores a notification it already holds", () => {
    const pushed = notification();

    expect(useNotificationStore.getState().receive(pushed)).toBe(true);
    expect(useNotificationStore.getState().receive(pushed)).toBe(false);

    expect(useNotificationStore.getState().unreadCount).toBe(1);
    expect(useNotificationStore.getState().items).toHaveLength(1);
  });

  it("keeps the inbox to its display length", () => {
    for (let index = 0; index < INBOX_LENGTH + 5; index++) {
      useNotificationStore.getState().receive(notification());
    }

    expect(useNotificationStore.getState().items).toHaveLength(INBOX_LENGTH);
  });
});

describe("marking everything read", () => {
  it("clears the badge at once and tells the server", async () => {
    const mark = vi.spyOn(notificationsApi, "markNotificationsRead").mockResolvedValue();
    useNotificationStore.getState().receive(notification());

    await useNotificationStore.getState().markAllRead();

    const state = useNotificationStore.getState();
    expect(state.unreadCount).toBe(0);
    expect(state.items.every((item) => item.readAtUtc !== null)).toBe(true);
    expect(mark).toHaveBeenCalledOnce();
  });

  it("does not call the server when nothing is unread", async () => {
    const mark = vi.spyOn(notificationsApi, "markNotificationsRead").mockResolvedValue();

    await useNotificationStore.getState().markAllRead();

    expect(mark).not.toHaveBeenCalled();
  });

  it("reloads the server's view when the call fails", async () => {
    vi.spyOn(notificationsApi, "markNotificationsRead").mockRejectedValue(new Error("offline"));
    const unread = notification();
    vi.spyOn(notificationsApi, "getNotifications").mockResolvedValue({ items: [unread], unreadCount: 1 });
    useNotificationStore.getState().receive(unread);

    await useNotificationStore.getState().markAllRead();

    expect(useNotificationStore.getState().unreadCount).toBe(1);
  });
});
