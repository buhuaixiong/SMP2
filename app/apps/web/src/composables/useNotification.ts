import { useService } from "@/core/hooks";
import type { NotificationService } from "@/services";

export function useNotification() {
  return useService<NotificationService>("notification");
}
