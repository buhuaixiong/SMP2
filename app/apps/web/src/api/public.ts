// src/api/public.ts - Public API client for registration and activation
import { apiFetch } from "./http";

// =============================================================================
// Types - Supplier Registration
// =============================================================================
export interface SupplierRegistrationDocumentUpload { name: string; type: string; size: number; content: string; }

export interface SupplierRegistrationPayload {
  companyName: string; englishName?: string; chineseName?: string;
  companyType: "limited" | "partnership" | "sole_proprietor" | "other"; companyTypeOther?: string;
  supplierClassification: "DM" | "IDM"; authorizedCapital?: string; issuedCapital?: string;
  directors?: string; owners?: string; registeredOffice: string; businessRegistrationNumber: string;
  businessAddress: string; businessPhone?: string; businessFax?: string; contactName: string;
  contactEmail: string; procurementEmail: string; contactPhone: string; financeContactName: string;
  financeContactEmail?: string; financeContactPhone: string; businessNature?: string;
  operatingCurrency: string; deliveryLocation: string; shipCode: string; productOrigin?: string;
  productTypes?: string; invoiceType?: string; paymentTermsDays?: string; paymentMethods?: string[];
  paymentMethodsOther?: string; bankName: string; bankAddress: string; bankAccountNumber: string;
  swiftCode?: string; notes?: string; businessLicenseFile?: SupplierRegistrationDocumentUpload | null;
  bankAccountFile?: SupplierRegistrationDocumentUpload | null;
}

export interface SupplierRegistrationResponse {
  message: string; applicationId: number; supplierCode: string; trackingToken: string; trackingUrl: string;
  draftToken: string | null; assignedPurchaserId: string | null; assignedPurchaserName: string | null;
  assignedPurchaserEmail: string | null; status: string; nextStep: string; supplierId?: number | null;
  assignedBuyerId?: number | null; assignedBuyerName?: string | null; assignmentCreated?: boolean | null;
  defaultPassword?: string | null; trackingUsername?: string | null; trackingPassword?: string | null;
  trackingMessage?: string | null;
}

export interface SupplierRegistrationDraftSaveResponse { draftToken: string; status: "active" | "submitted" | "expired"; expiresAt: string | null; lastStep: string | null; validation: { valid: boolean; errors: Record<string, string> }; normalized: Record<string, unknown>; }
export interface SupplierRegistrationDraftGetResponse extends SupplierRegistrationDraftSaveResponse { expiresAt: string | null; createdAt: string; updatedAt: string; form: Record<string, unknown>; submittedApplicationId: number | null; }
export interface SupplierRegistrationTempAccountInfo { id: number | null; username: string | null; status: string | null; expiresAt: string | null; lastLoginAt: string | null; createdAt: string | null; }
export interface SupplierRegistrationStatusHistoryEntry { type: "registration" | "approval"; step: string | null; status: string | null; result?: string | null; approver?: string | null; occurredAt: string | null; comments?: string | null; }
export interface SupplierRegistrationStatusResponse { applicationId: number; supplierId: number | null; supplierCode: string | null; status: string; supplierStatus: string | null; supplierStage: string | null; currentApprover: string | null; submittedAt: string | null; updatedAt: string | null; tempAccount: SupplierRegistrationTempAccountInfo | null; history: SupplierRegistrationStatusHistoryEntry[]; }
export interface SupplierRegistrationApplicationDetail { id: number; status: string; submittedAt: string | null; updatedAt: string | null; createdAt?: string | null; supplierId: number | null; supplierCode: string | null; companyName: string; companyNameEnglish?: string | null; englishName: string | null; chineseName: string | null; companyType: SupplierRegistrationPayload["companyType"]; companyTypeOther: string | null; authorizedCapital: string | null; issuedCapital: string | null; directors: string | null; owners: string | null; registeredOffice: string; businessRegistrationNumber: string; businessAddress: string; businessPhone: string | null; companyPhone?: string | null; businessFax: string | null; companyFax?: string | null; contactName: string; contactEmail: string; procurementEmail: string | null; contactPhone: string; financeContactName: string | null; financeContactEmail: string | null; financeContactPhone: string | null; businessNature: string | null; operatingCurrency: string; deliveryLocation: string; shipCode: string; productOrigin: string | null; productTypes: string | null; invoiceType: string | null; paymentTermsDays: string | null; paymentMethods: string[]; paymentMethodsOther: string | null; bankName: string; bankAddress: string; bankAccountNumber: string; swiftCode: string | null; notes: string | null; supplierClassification: SupplierRegistrationPayload["supplierClassification"]; businessLicenseFileName: string | null; businessLicenseFilePath: string | null; businessLicenseFileMime: string | null; businessLicenseFileSize: number | null; bankAccountFileName: string | null; bankAccountFilePath: string | null; bankAccountFileMime: string | null; bankAccountFileSize: number | null; tempAccount: SupplierRegistrationTempAccountInfo | null; supplier: { status: string | null; stage: string | null; currentApprover: string | null; updatedAt: string | null; }; }

// =============================================================================
// Types - Account Activation
// =============================================================================
export interface AccountActivationPayload { token: string; password: string; }
export interface AccountActivationResponse { success: boolean; message: string; supplierCode: string; supplierId: number; username: string; role: string; }

// =============================================================================
// Types - Registration Approval
// =============================================================================
export interface ApprovalApplicationListItem { id: number; companyName: string; supplierCode: string; status: string; createdAt: string; updatedAt: string; contactEmail: string; procurementEmail: string | null; }
export interface ApproveApplicationPayload { comment?: string; }
export interface ApproveApplicationResponse { success: boolean; message: string; nextStatus?: string; activationToken?: string; }
export interface RejectApplicationPayload { reason: string; }
export interface RequestInfoPayload { message: string; }
export interface BindSupplierCodeResponse { success: boolean; message: string; supplierCode: string; supplierId: number; loginMethods?: Array<{ type: string; value: string }>; }

// =============================================================================
// API Functions - Supplier Registration
// =============================================================================
export const submitSupplierRegistration = (payload: SupplierRegistrationPayload) =>
  apiFetch<SupplierRegistrationResponse>("/public/supplier-registrations", { method: "POST", body: payload });

export const saveSupplierRegistrationDraft = (payload: { draftToken?: string; form: Record<string, unknown>; lastStep?: string | null; }) =>
  apiFetch<SupplierRegistrationDraftSaveResponse>("/public/supplier-registrations/drafts", { method: "POST", body: payload });

export const fetchSupplierRegistrationDraft = (draftToken: string) =>
  apiFetch<SupplierRegistrationDraftGetResponse>(`/public/supplier-registrations/drafts/${encodeURIComponent(draftToken)}`);

export const fetchSupplierRegistrationStatus = (applicationId: number) =>
  apiFetch<{ data: SupplierRegistrationStatusResponse }>(`/supplier-registrations/${applicationId}/status`).then((r) => r.data);

export const fetchSupplierRegistrationStatusBySupplier = (supplierId: number) =>
  apiFetch<{ data: SupplierRegistrationStatusResponse }>(`/supplier-registrations/current/status?supplierId=${supplierId}`).then((r) => r.data);

export const fetchSupplierRegistrationStatusForCurrentUser = () =>
  apiFetch<{ data: SupplierRegistrationStatusResponse }>("/supplier-registrations/current/status").then((r) => r.data);

export const fetchSupplierRegistrationApplication = (applicationId: number) =>
  apiFetch<{ data: SupplierRegistrationApplicationDetail }>(`/supplier-registrations/applications/${applicationId}`).then((r) => r.data);

// =============================================================================
// API Functions - Account Activation
// =============================================================================
export const activateSupplierAccount = (payload: AccountActivationPayload) =>
  apiFetch<AccountActivationResponse>("/public/activate-account", { method: "POST", body: payload });

// =============================================================================
// API Functions - Registration Approval
// =============================================================================
export const fetchPendingRegistrations = () =>
  apiFetch<{ data: ApprovalApplicationListItem[] }>("/supplier-registrations/pending").then((r) => r.data);

export const fetchApprovedRegistrations = () =>
  apiFetch<{ data: ApprovalApplicationListItem[] }>("/supplier-registrations/approved").then((r) => r.data);

export const fetchRegistrationDetail = (id: number) =>
  apiFetch<{ data: SupplierRegistrationApplicationDetail }>(`/supplier-registrations/${id}`).then((r) => r.data);

export const approveRegistration = (id: number, payload: ApproveApplicationPayload) =>
  apiFetch<ApproveApplicationResponse>(`/supplier-registrations/${id}/approve`, { method: "POST", body: payload });

export const rejectRegistration = (id: number, payload: RejectApplicationPayload) =>
  apiFetch<ApproveApplicationResponse>(`/supplier-registrations/${id}/reject`, { method: "POST", body: payload });

export const requestRegistrationInfo = (id: number, payload: RequestInfoPayload) =>
  apiFetch<ApproveApplicationResponse>(`/supplier-registrations/${id}/request-info`, { method: "POST", body: payload });

export const bindSupplierCode = (id: number, supplierCode?: string | null) =>
  apiFetch<BindSupplierCodeResponse>(`/supplier-registrations/${id}/bind-code`, { method: "POST", body: { supplierCode } });
