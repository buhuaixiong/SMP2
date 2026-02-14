/**
 * File Upload Configuration Types
 */

export interface FileUploadConfig {
  id: number;
  scenario: string;
  scenario_name: string;
  scenario_description: string | null;
  allowed_formats: string;
  max_file_size: number;
  max_file_count: number;
  enable_virus_scan: number;
  created_at: string;
  updated_at: string;
  updated_by: number | null;
}

export interface FileScanRecord {
  id: number;
  file_path: string;
  original_name: string;
  file_size: number | null;
  mime_type: string | null;
  scan_status: "clean" | "infected" | "error" | "disabled" | "skipped";
  scan_result: string | null;
  scan_engine: string;
  scan_duration: number | null;
  is_clean: number;
  threat_name: string | null;
  scanned_at: string;
  uploaded_by: number | null;
  scenario: string | null;
  quarantined: number;
  quarantine_path: string | null;
}

export interface ScanStatistics {
  total: number;
  clean: number;
  infected: number;
}

export interface FileValidationError {
  message: string;
  code:
    | "FILE_TOO_LARGE"
    | "TOO_MANY_FILES"
    | "INVALID_FILE_TYPE"
    | "VIRUS_DETECTED"
    | "SCAN_ERROR"
    | "UNEXPECTED_FILE";
  filename?: string;
  scanStatus?: string;
}

export interface FileUploadProgress {
  file: File;
  progress: number;
  status: "pending" | "uploading" | "scanning" | "success" | "error";
  error?: string;
  url?: string;
}

export interface UploadOptions {
  scenario: string;
  onProgress?: (progress: FileUploadProgress[]) => void;
  onSuccess?: (files: any[]) => void;
  onError?: (error: FileValidationError) => void;
}
