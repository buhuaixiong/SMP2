import { apiFetch, BASE_URL } from "./http";
import type { Invoice } from "@/types";

/**
 * Invoice API Module
 * Handles invoice management for finance accountants
 */

export interface InvoiceListParams {
  type?: "temporary_supplier" | "formal_supplier";
  status?: "pending" | "verified" | "rejected" | "exception";
  page?: number;
  limit?: number;
}

export interface InvoiceDetailResponse extends Invoice {
  supplier_name?: string;
  supplier_stage?: string;
  rfq_title?: string;
  rfq_amount?: number;
  order_number?: string;
  order_amount?: number;
  file?: {
    id: number;
    original_name: string;
    stored_name: string;
    storage_path: string;
    downloadUrl: string;
  } | null;
}

export interface InvoiceReviewPayload {
  status: "verified" | "rejected" | "exception";
  review_notes?: string;
  rejection_reason?: string;
}

export interface InvoiceUploadFormData {
  supplier_id: number;
  rfq_id?: number;
  order_id?: number;
  invoice_number: string;
  invoice_date: string;
  amount: number;
  type: "temporary_supplier" | "formal_supplier";
  tax_rate?: string;
  invoice_type?: string;
  pre_payment_proof?: string;
  signature_seal?: boolean;
  file: File;
}

/**
 * Fetch list of invoices with optional filters
 */
export async function fetchInvoices(params?: InvoiceListParams): Promise<Invoice[]> {
  return await apiFetch<Invoice[]>("/invoices", {
    method: "GET",
    params,
  });
}

/**
 * Fetch single invoice detail
 */
export async function fetchInvoiceById(id: number): Promise<InvoiceDetailResponse> {
  return await apiFetch<InvoiceDetailResponse>(`/invoices/${id}`, {
    method: "GET",
  });
}

/**
 * Download invoice file
 */
export function getInvoiceDownloadUrl(id: number): string {
  return `${BASE_URL}/invoices/${id}/download`;
}

/**
 * Review invoice (accountant action)
 */
export async function reviewInvoice(
  id: number,
  payload: InvoiceReviewPayload,
): Promise<{ message: string; status: string; validation_errors?: string[] }> {
  return await apiFetch(`/invoices/${id}/review`, {
    method: "POST",
    body: payload,
  });
}

/**
 * Delete invoice
 */
export async function deleteInvoice(id: number): Promise<{ message: string; invoiceId: number }> {
  return await apiFetch(`/invoices/${id}`, {
    method: "DELETE",
  });
}

/**
 * Upload new invoice (with file)
 */
export async function uploadInvoice(
  formData: InvoiceUploadFormData,
): Promise<{ message: string; invoiceId: number }> {
  const body = new FormData();

  // Append all fields to FormData
  body.append("supplier_id", formData.supplier_id.toString());
  body.append("invoice_number", formData.invoice_number);
  body.append("invoice_date", formData.invoice_date);
  body.append("amount", formData.amount.toString());
  body.append("type", formData.type);
  body.append("file", formData.file);

  if (formData.rfq_id) body.append("rfq_id", formData.rfq_id.toString());
  if (formData.order_id) body.append("order_id", formData.order_id.toString());
  if (formData.tax_rate) body.append("tax_rate", formData.tax_rate);
  if (formData.invoice_type) body.append("invoice_type", formData.invoice_type);
  if (formData.pre_payment_proof) body.append("pre_payment_proof", formData.pre_payment_proof);
  if (formData.signature_seal !== undefined)
    body.append("signature_seal", formData.signature_seal.toString());

  const token = localStorage.getItem("token");

  const response = await fetch(`${BASE_URL}/invoices/upload`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
    },
    body,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: "Upload failed" }));
    throw new Error(error.message || "Upload failed");
  }

  return await response.json();
}

/**
 * Get invoice statistics
 */
export async function fetchInvoiceStats(): Promise<{
  total_invoices: number;
  verified_invoices: number;
  pending_invoices: number;
  rejected_invoices: number;
  exception_invoices: number;
  large_amount_invoices: number;
  avg_review_days: number;
}> {
  return await apiFetch("/invoices/stats/overview", {
    method: "GET",
  });
}
