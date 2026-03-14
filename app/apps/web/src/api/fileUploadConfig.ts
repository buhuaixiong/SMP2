/**
 * File Upload Configuration API
 */

import { apiFetch } from "./http";
import type { FileUploadConfig, FileScanRecord, ScanStatistics } from "../types/fileUpload";

/**
 * Get all file upload configurations
 */
export async function fetchFileUploadConfigs(): Promise<FileUploadConfig[]> {
  const res = await apiFetch<{ success: boolean; data: FileUploadConfig[] }>(
    "/file-upload-configs",
  );
  return res.data;
}

/**
 * Get configuration for a specific scenario
 */
export async function fetchFileUploadConfig(scenario: string): Promise<FileUploadConfig> {
  const res = await apiFetch<{ success: boolean; data: FileUploadConfig }>(
    `/file-upload-configs/${scenario}`,
  );
  return res.data;
}

/**
 * Update configuration for a specific scenario
 */
export async function updateFileUploadConfig(
  scenario: string,
  config: Partial<FileUploadConfig>,
): Promise<FileUploadConfig> {
  const res = await apiFetch<{ success: boolean; data: FileUploadConfig }>(
    `/file-upload-configs/${scenario}`,
    {
      method: "PUT",
      body: config,
    },
  );
  return res.data;
}

/**
 * Create a new file upload configuration
 */
export async function createFileUploadConfig(
  config: Partial<FileUploadConfig>,
): Promise<FileUploadConfig> {
  const res = await apiFetch<{ success: boolean; data: FileUploadConfig }>("/file-upload-configs", {
    method: "POST",
    body: config,
  });
  return res.data;
}

/**
 * Delete a file upload configuration
 */
export async function deleteFileUploadConfig(scenario: string): Promise<void> {
  await apiFetch(`/file-upload-configs/${scenario}`, {
    method: "DELETE",
  });
}

/**
 * Get scan statistics
 */
export async function fetchScanStatistics(params?: {
  scenario?: string;
  uploadedBy?: number;
  dateFrom?: string;
  dateTo?: string;
}): Promise<ScanStatistics> {
  const res = await apiFetch<{ success: boolean; data: ScanStatistics }>(
    "/file-upload-configs/scan-records/statistics",
    { params },
  );
  return res.data;
}

/**
 * Get scan records with pagination
 */
export async function fetchScanRecords(params?: {
  page?: number;
  limit?: number;
  scenario?: string;
  uploadedBy?: number;
  scanStatus?: string;
}): Promise<{
  data: FileScanRecord[];
  pagination: {
    page: number;
    limit: number;
    total: number;
    totalPages: number;
  };
}> {
  const res = await apiFetch<{
    success: boolean;
    data: FileScanRecord[];
    pagination: {
      page: number;
      limit: number;
      total: number;
      totalPages: number;
    };
  }>("/file-upload-configs/scan-records/list", { params });

  return {
    data: res.data,
    pagination: res.pagination,
  };
}

/**
 * Cleanup old scan records
 */
export async function cleanupScanRecords(
  daysToKeep: number = 90,
): Promise<{ deletedCount: number }> {
  const res = await apiFetch<{ success: boolean; data: { deletedCount: number } }>(
    "/file-upload-configs/scan-records/cleanup",
    {
      method: "POST",
      body: { daysToKeep },
    },
  );
  return res.data;
}
