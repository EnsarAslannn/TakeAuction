import { create } from "zustand";
import * as notificationsApi from "@/api/notifications";
import type { UserNotification } from "@/api/types";

export const INBOX_LENGTH = 20;

interface NotificationState {
  items: UserNotification[];
  unreadCount: number;
  status: "idle" | "loading" | "ready" | "error";
  load: () => Promise<void>;
  receive: (notification: UserNotification) => boolean;
  markAllRead: () => Promise<void>;
  reset: () => void;
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  items: [],
  unreadCount: 0,
  status: "idle",

  load: async () => {
    set({ status: "loading" });
    try {
      const inbox = await notificationsApi.getNotifications();
      set({ items: inbox.items, unreadCount: inbox.unreadCount, status: "ready" });
    } catch {
      set({ status: "error" });
    }
  },

  receive: (notification) => {
    if (get().items.some((item) => item.id === notification.id)) {
      return false;
    }

    set((state) => ({
      items: [notification, ...state.items].slice(0, INBOX_LENGTH),
      unreadCount: state.unreadCount + (notification.readAtUtc === null ? 1 : 0),
    }));

    return true;
  },

  markAllRead: async () => {
    if (get().unreadCount === 0) return;

    const readAt = new Date().toISOString();
    set((state) => ({
      items: state.items.map((item) => (item.readAtUtc ? item : { ...item, readAtUtc: readAt })),
      unreadCount: 0,
    }));

    try {
      await notificationsApi.markNotificationsRead();
    } catch {
      await get().load();
    }
  },

  reset: () => set({ items: [], unreadCount: 0, status: "idle" }),
}));
