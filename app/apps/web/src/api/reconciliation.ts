import { apiFetch } from "./http";
import type {
  Reconciliation,
  ReconciliationDashboardData,
  SupplierReconciliationData,
  WarehouseReceiptSummary,
  ReconciliationReport,
  InvoiceUploadPayload,
  InvoiceMatchPayload,
  ReconciliationConfirmPayload,
} from "../types";

/**
 * Get accountant dashboard with reconciliation stats and trends
 */
export async function fetchReconciliationDashboard(params?: {
  startDate?: string;
  endDate?: string;
  status?: string;
}): Promise<ReconciliationDashboardData> {
  const res = await apiFetch<{ success: boolean; data: ReconciliationDashboardData }>(
    "/reconciliation/dashboard",
    { params },
  );
  return res.data;
}

/**
 * Get supplier's own reconciliation data
 */
export async function fetchSupplierReconciliations(params?: {
  startDate?: string;
  endDate?: string;
  status?: string;
}): Promise<SupplierReconciliationData> {
  const res = await apiFetch<{ success: boolean; data: SupplierReconciliationData }>(
    "/reconciliation/supplier",
    { params },
  );
  return res.data;
}

/**
 * Get warehouse receipt data
 */
export async function fetchWarehouseReceipts(params?: {
  reconciliationId?: number;
  supplierId?: number;
  startDate?: string;
  endDate?: string;
}): Promise<WarehouseReceiptSummary> {
  const res = await apiFetch<{ success: boolean; data: WarehouseReceiptSummary }>(
    "/reconciliation/warehouse-receipts",
    { params },
  );
  return res.data;
}

/**
 * Upload invoice for reconciliation
 */
export async function uploadInvoice(payload: InvoiceUploadPayload): Promise<{
  invoiceId: number;
  reconciliationId: number;
  fileName: string;
  fileSize: number;
}> {
  const formData = new FormData();
  formData.append("reconciliationId", payload.reconciliationId.toString());
  formData.append("invoiceAmount", payload.invoiceAmount.toString());
  formData.append("invoiceDate", payload.invoiceDate);
  formData.append("invoiceNumber", payload.invoiceNumber);
  formData.append("invoiceFile", payload.invoiceFile);

  const res = await apiFetch<{
    success: boolean;
    message: string;
    data: {
      invoiceId: number;
      reconciliationId: number;
      fileName: string;
      fileSize: number;
    };
  }>("/reconciliation/upload-invoice", {
    method: "POST",
    body: formData,
  });

  return res.data;
}

/**
 * Manual invoice-reconciliation matching
 */
export async function matchInvoice(payload: InvoiceMatchPayload): Promise<{
  reconciliationId: number;
  invoiceId: number;
  matchedAmount: number;
  varianceAmount: number;
  status: string;
}> {
  const res = await apiFetch<{
    success: boolean;
    message: string;
    data: {
      reconciliationId: number;
      invoiceId: number;
      matchedAmount: number;
      varianceAmount: number;
      status: string;
    };
  }>("/reconciliation/match-invoice", {
    method: "POST",
    body: payload,
  });

  return res.data;
}

/**
 * Accountant confirmation of reconciliation
 */
export async function confirmReconciliation(payload: ReconciliationConfirmPayload): Promise<{
  reconciliationId: number;
  confirmed: boolean;
  confirmedAt: string;
}> {
  const res = await apiFetch<{
    success: boolean;
    message: string;
    data: {
      reconciliationId: number;
      confirmed: boolean;
      confirmedAt: string;
    };
  }>("/reconciliation/confirm", {
    method: "PUT",
    body: payload,
  });

  return res.data;
}

/**
 * Get reconciliation reports
 */
export async function fetchReconciliationReport(params: {
  reportType: "summary" | "variance" | "supplier";
  startDate?: string;
  endDate?: string;
  supplierId?: number;
}): Promise<ReconciliationReport> {
  const res = await apiFetch<{ success: boolean; data: ReconciliationReport }>(
    "/reconciliation/reports",
    { params },
  );
  return res.data;
}

/**
 * Get specific reconciliation details
 */
export async function fetchReconciliationById(id: number): Promise<Reconciliation> {
  const res = await apiFetch<{ success: boolean; data: Reconciliation }>(`/reconciliation/${id}`);
  return res.data;
}
