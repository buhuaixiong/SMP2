import { SupplierStatus, type Supplier } from "@/types";
import { sanitizeInput } from "./security";

export interface ApprovalOptions {
  approver: string;
  decision: "approved" | "rejected";
  comments?: string;
  timestamp?: string;
}

export const getNextStatus = (
  current: SupplierStatus,
  decision: "approved" | "rejected",
): SupplierStatus => {
  if (decision === "rejected") {
    return SupplierStatus.REJECTED;
  }
  switch (current) {
    case SupplierStatus.PENDING_PURCHASER:
      return SupplierStatus.PENDING_PURCHASE_MANAGER;
    case SupplierStatus.PENDING_PURCHASE_MANAGER:
      return SupplierStatus.PENDING_FINANCE_MANAGER;
    case SupplierStatus.PENDING_FINANCE_MANAGER:
      return SupplierStatus.APPROVED;
    default:
      return current;
  }
};

export const handleApproval = (supplier: Supplier, options: ApprovalOptions): Supplier => {
  const sanitizedComment = sanitizeInput(options.comments ?? "");
  const historyRecord = {
    step: supplier.status,
    approver: options.approver,
    result: options.decision,
    date: options.timestamp ?? new Date().toISOString(),
    comments: sanitizedComment || undefined,
  };

  return {
    ...supplier,
    status: getNextStatus(supplier.status, options.decision),
    approvalHistory: [historyRecord, ...supplier.approvalHistory],
    notes:
      options.decision === "rejected"
        ? sanitizedComment || supplier.notes || null
        : (supplier.notes ?? null),
  };
};
