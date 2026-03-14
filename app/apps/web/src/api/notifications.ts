import { apiFetch } from "./http";
import type { NotificationItem, PaginatedNotificationsResponse } from "@/types";

export interface NotificationQueryParams {
  status?: "unread" | "read";
  limit?: number;
  offset?: number;
}

export async function fetchNotifications(
  params?: NotificationQueryParams,
): Promise<PaginatedNotificationsResponse> {
  return await apiFetch<PaginatedNotificationsResponse>("/notifications", { params });
}

export async function markNotificationAsRead(id: number): Promise<NotificationItem> {
  const response = await apiFetch<{ data: NotificationItem }>(`/notifications/${id}/read`, {
    method: "PUT",
  });
  return response.data;
}
