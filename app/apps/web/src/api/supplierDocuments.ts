import { apiFetch } from "./http";
import type { SupplierDocument } from "@/types";

export const listSupplierDocuments = (supplierId: number) =>
  apiFetch<SupplierDocument[]>(`/suppliers/${supplierId}/documents`);

export interface UploadDocumentPayload {
  supplierId: number;
  file: File;
  docType?: string;
  expiresAt?: string;
  status?: string;
  notes?: string;
  uploadedBy?: string;
  actorId?: string;
  actorName?: string;
}

export const uploadSupplierDocument = async (payload: UploadDocumentPayload) => {
  const form = new FormData();
  form.append("file", payload.file);
  if (payload.docType) form.append("docType", payload.docType);
  if (payload.expiresAt) form.append("expiresAt", payload.expiresAt);
  if (payload.status) form.append("status", payload.status);
  if (payload.notes) form.append("notes", payload.notes);
  if (payload.uploadedBy) form.append("uploadedBy", payload.uploadedBy);
  if (payload.actorId) form.append("actorId", payload.actorId);
  if (payload.actorName) form.append("actorName", payload.actorName);

  return apiFetch<SupplierDocument[]>(`/suppliers/${payload.supplierId}/documents`, {
    method: "POST",
    body: form,
    parseData: true,
  });
};

export interface UpdateDocumentPayload {
  docType?: string | null;
  expiresAt?: string | null;
  status?: string | null;
  notes?: string | null;
  uploadedBy?: string | null;
  actorId?: string;
  actorName?: string;
  file?: File;
}

export const updateSupplierDocument = (
  supplierId: number,
  documentId: number,
  payload: UpdateDocumentPayload,
) => {
  const form = new FormData();
  if (payload.file) {
    form.append("file", payload.file);
  }
  if (payload.docType !== undefined) form.append("docType", payload.docType ?? "");
  if (payload.expiresAt !== undefined) form.append("expiresAt", payload.expiresAt ?? "");
  if (payload.status !== undefined) form.append("status", payload.status ?? "");
  if (payload.notes !== undefined) form.append("notes", payload.notes ?? "");
  if (payload.uploadedBy !== undefined) form.append("uploadedBy", payload.uploadedBy ?? "");
  if (payload.actorId) form.append("actorId", payload.actorId);
  if (payload.actorName) form.append("actorName", payload.actorName);

  return apiFetch<SupplierDocument[]>(`/suppliers/${supplierId}/documents/${documentId}`, {
    method: "PUT",
    body: form,
    parseData: true,
  });
};

export const deleteSupplierDocument = (supplierId: number, documentId: number) =>
  apiFetch<void>(`/suppliers/${supplierId}/documents/${documentId}`, {
    method: "DELETE",
    parseData: false,
  });
