import { apiFetch, BASE_URL } from "./http";
import type { SupplierDocument } from "@/types";

export interface UploadDocumentPayload {
  file: File;
  docType: string;
  category?: string;
  validFrom?: string;
  expiresAt?: string;
  notes?: string;
}

export async function uploadDocument(supplierId: number, payload: UploadDocumentPayload) {
  const formData = new FormData();
  formData.append("file", payload.file);
  formData.append("docType", payload.docType);
  if (payload.category) formData.append("category", payload.category);
  if (payload.validFrom) formData.append("validFrom", payload.validFrom);
  if (payload.expiresAt) formData.append("expiresAt", payload.expiresAt);
  if (payload.notes) formData.append("notes", payload.notes);

  const response = await apiFetch<{ data: SupplierDocument }>(
    `/suppliers/${supplierId}/documents`,
    {
      method: "POST",
      body: formData,
    },
  );
  return response.data;
}

export async function getSupplierDocuments(supplierId: number) {
  const response = await apiFetch<{ data: SupplierDocument[] }>(
    `/suppliers/${supplierId}/documents`,
  );
  return response.data;
}

export async function downloadDocument(supplierId: number, documentId: number): Promise<Blob> {
  const token = localStorage.getItem("token");
  const headers: HeadersInit = token ? { Authorization: `Bearer ${token}` } : {};
  const response = await fetch(`${BASE_URL}/suppliers/${supplierId}/documents/${documentId}/download`, {
    headers,
  });

  if (!response.ok) {
    throw new Error("Failed to download document");
  }

  return response.blob();
}

export async function deleteDocument(supplierId: number, documentId: number) {
  await apiFetch(`/suppliers/${supplierId}/documents/${documentId}`, {
    method: "DELETE",
  });
}

export async function renewDocument(supplierId: number, documentId: number, file: File) {
  const formData = new FormData();
  formData.append("file", file);

  const response = await apiFetch<{ data: SupplierDocument }>(
    `/suppliers/${supplierId}/documents/${documentId}/renew`,
    {
      method: "POST",
      body: formData,
    },
  );
  return response.data;
}

export async function bulkUploadDocuments(
  supplierId: number,
  documents: Array<{
    file: File;
    docType: string;
    category?: string;
    validFrom?: string;
    expiresAt?: string;
  }>,
) {
  const formData = new FormData();

  documents.forEach((doc, index) => {
    formData.append(`files`, doc.file);
    formData.append(`docTypes`, doc.docType);
    if (doc.category) formData.append(`categories`, doc.category);
    if (doc.validFrom) formData.append(`validFrom`, doc.validFrom);
    if (doc.expiresAt) formData.append(`expiresAt`, doc.expiresAt);
  });

  const response = await apiFetch<{
    data: {
      success: Array<{ id: number; originalName: string; fileType: string; uploadTime: string }>;
      failed: Array<{ filename: string; error: string }>;
    };
  }>(`/temp-suppliers/${supplierId}/bulk-upload-documents`, {
    method: "POST",
    body: formData,
  });
  return response.data;
}
