import { http } from "./client";
import type { NotificationsResponse } from "./types";

export async function getNotifications(): Promise<NotificationsResponse> {
  const { data } = await http.get<NotificationsResponse>("/notifications");
  return data;
}

export async function markNotificationsRead(): Promise<void> {
  await http.post("/notifications/read");
}
