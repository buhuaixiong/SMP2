import dayjs from "dayjs";
import { SupplierStatus } from "@/types";

export const formatDate = (date: string | Date) => {
  return dayjs(date).format("YYYY-MM-DD HH:mm:ss");
};

export const getStatusText = (status?: SupplierStatus | string | null) => {
  // 增加安全检查
  if (!status) {
    return "Unknown Status"; // 如果 status 无效，返回一个默认文本
  }

  const statusMap: Record<string, string> = {
    [SupplierStatus.PENDING_INFO]: "Awaiting profile details",
    [SupplierStatus.PENDING_PURCHASER]: "Waiting for purchaser review",
    "pending_code_binding": "Pending code binding",
    "pending_cashier": "Cashier review",
    // ... 其他状态
    [SupplierStatus.REJECTED]: "Rejected",
  };
  return statusMap[status] || status;
};

export const getStatusType = (status?: SupplierStatus | string | null) => {
  if (!status) {
    return "info"; // 如果 status 无效，返回默认的 'info' 类型
  }

  const typeMap: Record<string, string> = {
    [SupplierStatus.PENDING_INFO]: "info",
    [SupplierStatus.PENDING_PURCHASER]: "warning",
    [SupplierStatus.PENDING_QUALITY_REVIEW]: "warning",
    [SupplierStatus.PENDING_PURCHASE_MANAGER]: "warning",
    [SupplierStatus.PENDING_PURCHASE_DIRECTOR]: "warning",
    "pending_code_binding": "warning",
    "pending_cashier": "warning",
    [SupplierStatus.PENDING_FINANCE_MANAGER]: "warning",
    [SupplierStatus.APPROVED]: "success",
    [SupplierStatus.REJECTED]: "danger",
  };
  return typeMap[status] || "info";
};
