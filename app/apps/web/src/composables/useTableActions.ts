import { ref } from "vue";
import { useNotification } from "./useNotification";

export interface TableActionOptions<T> {
  rows?: T[];
  requireSelection?: boolean;
  confirmMessage?: string;
  confirmTitle?: string;
  successMessage?: string;
  errorMessage?: string;
  emptySelectionMessage?: string;
}

export function useTableActions<T>() {
  const selectedRows = ref<T[]>([]);
  const processing = ref(false);
  const notification = useNotification();

  const setSelection = (rows: T[]) => {
    selectedRows.value = rows;
  };

  const clearSelection = () => {
    selectedRows.value = [];
  };

  const runAction = async (
    handler: (rows: T[]) => Promise<void>,
    options: TableActionOptions<T> = {},
  ) => {
    const rows = options.rows ?? selectedRows.value;
    if (options.requireSelection && rows.length === 0) {
      notification.warning(options.emptySelectionMessage ?? "请选择需要操作的记录");
      return;
    }

    if (options.confirmMessage) {
      await notification.confirm(options.confirmMessage, options.confirmTitle ?? "确认操作");
    }

    processing.value = true;
    try {
      await handler(rows as T[]);
      if (options.successMessage) {
        notification.success(options.successMessage);
      }
    } catch (error: any) {
      const message = error?.message || options.errorMessage || "操作失败";
      notification.error(message);
      throw error;
    } finally {
      processing.value = false;
    }
  };

  return {
    selectedRows,
    processing,
    setSelection,
    clearSelection,
    runAction,
  };
}
