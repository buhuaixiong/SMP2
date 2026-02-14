import { defineStore } from "pinia";
import {
  fetchNotifications,
  markNotificationAsRead,
  type NotificationQueryParams,
} from "@/api/notifications";
import type { NotificationItem } from "@/types";

interface NotificationState {
  items: NotificationItem[];
  loading: boolean;
  unreadCount: number;
  lastLoadedAt: string | null;
  error: string | null;
}

export const useNotificationStore = defineStore("notifications", {
  state: (): NotificationState => ({
    items: [],
    loading: false,
    unreadCount: 0,
    lastLoadedAt: null,
    error: null,
  }),
  getters: {
    rfqPendingNotifications: (state) =>
      state.items.filter(
        (item) => item.type === "rfq_pending_processing" && item.status === "unread",
      ),
  },
  actions: {
    async loadNotifications(params?: NotificationQueryParams) {
      this.loading = true;
      this.error = null;
      try {
        const response = await fetchNotifications(params);
        this.items = response.data;
        this.unreadCount =
          response.meta?.unreadCount ??
          response.data.filter((item) => item.status === "unread").length;
        this.lastLoadedAt = new Date().toISOString();
      } catch (error: any) {
        console.error("Failed to load notifications", error);
        this.error = error?.message ?? "Failed to load notifications";
      } finally {
        this.loading = false;
      }
    },
    async markNotificationsAsRead(ids: number[]) {
      const uniqueIds = Array.from(new Set(ids));
      if (!uniqueIds.length) {
        return;
      }

      await Promise.all(
        uniqueIds.map(async (id) => {
          try {
            const updated = await markNotificationAsRead(id);
            const index = this.items.findIndex((item) => item.id === id);
            if (index !== -1) {
              this.items[index] = updated;
            }
          } catch (error) {
            console.error(`Failed to mark notification ${id} as read`, error);
          }
        }),
      );

      const unreadAfterUpdate = this.items.filter((item) => item.status === "unread").length;
      this.unreadCount = unreadAfterUpdate;
    },
    clear() {
      this.items = [];
      this.unreadCount = 0;
      this.error = null;
      this.lastLoadedAt = null;
    },
  },
});
