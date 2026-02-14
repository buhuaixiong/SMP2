import { apiFetch, BASE_URL } from "@/api/http";
import type {
  Rfq,
  Quote,
  QuoteVersion,
  RfqReview,
  RfqCategoryMetadata,
  CreateRfqPayload,
  SubmitQuotePayload,
  ModifyQuotePayload,
  ReviewRfqPayload,
  SupplierRfqInvitationSummary,
  QuotePriceComparison,
  CreatePriceComparisonPayload,
  QuoteExportData,
} from "@/types";

export interface RfqListParams {
  materialType?: string;
  distributionCategory?: string;
  distributionSubcategory?: string;
  rfqType?: string;
  status?: string;
  createdBy?: string;
  page?: number;
  limit?: number;
}

export interface RfqListResponse {
  data: Rfq[];
  pagination: {
    page: number;
    limit: number;
    total: number;
    totalPages: number;
  };
}

const API_ORIGIN = BASE_URL.replace(/\/api\/?$/, "");

type UnknownRecord = Record<string, unknown>;

const toRecord = (value: unknown): UnknownRecord | null =>
  value && typeof value === "object" ? (value as UnknownRecord) : null;

const toRecordArray = (value: unknown): UnknownRecord[] => {
  if (!Array.isArray(value)) return [];
  return value.filter((entry): entry is UnknownRecord => !!entry && typeof entry === "object");
};

const toOptionalString = (value: unknown): string | null => {
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  return null;
};

const toOptionalNumber = (value: unknown): number | null => {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
};

const toOptionalNumberOrString = (value: unknown): string | number | null => {
  if (typeof value === "string") return value;
  const numeric = toOptionalNumber(value);
  return numeric !== null ? numeric : null;
};

function normalizeRfqListParams(params?: RfqListParams) {
  if (!params) {
    return params;
  }
  const normalized: Record<string, unknown> = { ...params };
  if (params.limit != null) {
    normalized.pageSize = params.limit;
    delete normalized.limit;
  }
  return normalized;
}

function mapPlatformToApi(platform: string): string {
  if (platform === "zhenkunxing") {
    return "zkh";
  }
  return platform;
}

function mapPlatformFromApi(platform: string): string {
  if (platform === "zkh") {
    return "zhenkunxing";
  }
  return platform;
}

function normalizePriceComparisonFormData(formData: FormData): FormData {
  const normalized = new FormData();
  const platformRaw = formData.get("platform") ?? formData.get("online_platform");
  const priceRaw = formData.get("platformPrice") ?? formData.get("online_price");
  const productUrl = formData.get("productUrl") ?? formData.get("product_url");
  const notes = formData.get("notes") ?? formData.get("comparison_notes");
  const lineItemId = formData.get("lineItemId");
  const screenshot = formData.get("screenshot");

  if (platformRaw) {
    normalized.append("platform", mapPlatformToApi(String(platformRaw)));
  }
  if (priceRaw != null) {
    normalized.append("platformPrice", String(priceRaw));
  }
  if (productUrl) {
    normalized.append("productUrl", String(productUrl));
  }
  if (notes) {
    normalized.append("notes", String(notes));
  }
  if (lineItemId) {
    normalized.append("lineItemId", String(lineItemId));
  }
  if (screenshot instanceof Blob) {
    normalized.append("screenshot", screenshot);
  }

  return normalized;
}

/**
 * Get RFQ category metadata (equipment, auxiliary materials, etc.)
 */
export async function fetchRfqCategories(): Promise<RfqCategoryMetadata> {
  const response = await apiFetch<{ data: RfqCategoryMetadata }>("/rfq-workflow/categories");
  return response.data;
}

function normalizeRfqPagination(
  pagination: unknown,
  fallback: RfqListResponse["pagination"],
): RfqListResponse["pagination"] {
  const paginationRecord =
    pagination && typeof pagination === "object" ? (pagination as Record<string, unknown>) : {};
  const pageRaw = paginationRecord.page ?? fallback.page;
  const limitRaw = paginationRecord.limit ?? paginationRecord.pageSize ?? fallback.limit;
  const totalRaw = paginationRecord.total ?? fallback.total;
  const totalPagesRaw = paginationRecord.totalPages ?? paginationRecord.pageCount ?? fallback.totalPages;

  const page = Number.isFinite(Number(pageRaw)) && Number(pageRaw) > 0 ? Number(pageRaw) : fallback.page;
  const limit =
    Number.isFinite(Number(limitRaw)) && Number(limitRaw) > 0 ? Number(limitRaw) : fallback.limit;
  const total =
    Number.isFinite(Number(totalRaw)) && Number(totalRaw) >= 0 ? Number(totalRaw) : fallback.total;

  let totalPages =
    Number.isFinite(Number(totalPagesRaw)) && Number(totalPagesRaw) >= 0
      ? Number(totalPagesRaw)
      : fallback.totalPages;

  if (!totalPages && total && limit) {
    totalPages = Math.ceil(total / limit);
  }

  return { page, limit, total, totalPages };
}


/**
 * Get list of RFQs with optional filters
 */
export async function fetchRfqs(params?: RfqListParams): Promise<RfqListResponse> {
  const payload = await apiFetch<UnknownRecord>("/rfq-workflow", {
    params: normalizeRfqListParams(params),
  });
  const fallbackPagination = {
    page: params?.page ?? 1,
    limit: params?.limit ?? 20,
    total: 0,
    totalPages: 0,
  };

  if (!payload) {
    return { data: [], pagination: fallbackPagination };
  }

  const dataRecord = toRecord(payload.data);
  const directList = Array.isArray(payload.data) ? (payload.data as Rfq[]) : null;
  const nestedList = dataRecord && Array.isArray(dataRecord.data) ? (dataRecord.data as Rfq[]) : null;
  const list = directList ?? nestedList;

  if (list) {
    const paginationSource = directList
      ? payload.pagination
      : dataRecord?.pagination ?? payload.pagination;
    return {
      data: list,
      pagination: normalizeRfqPagination(paginationSource, fallbackPagination),
    };
  }

  return {
    data: [],
    pagination: normalizeRfqPagination(payload.pagination, fallbackPagination),
  };
}

/**
 * Get a single RFQ by ID with all details
 */
export async function fetchRfqById(id: number): Promise<Rfq> {
  const response = await apiFetch<{ data: Rfq }>(`/rfq-workflow/${id}`);
  return response.data;
}
export async function fetchSupplierRfq(id: number): Promise<Rfq> {
  const response = await apiFetch<{ data: Rfq }>(`/rfq-workflow/supplier/${id}`);
  return response.data;
}


/**
 * Create a new RFQ
 */
export async function createRfq(payload: CreateRfqPayload): Promise<Rfq> {
  const response = await apiFetch<{ data: Rfq }>("/rfq-workflow/create", {
    method: "POST",
    body: payload,
  });
  return response.data;
}

/**
 * Update an existing RFQ (draft only)
 */
export async function updateRfq(id: number, payload: Partial<CreateRfqPayload>): Promise<Rfq> {
  const response = await apiFetch<{ data: Rfq }>(`/rfq-workflow/${id}`, {
    method: "PUT",
    body: payload,
  });
  return response.data;
}

/**
 * Publish an RFQ (change status from draft to published)
 */
export async function publishRfq(id: number): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${id}/publish`, {
    method: "POST",
  });
}

/**
 * Cancel an RFQ
 */
export async function cancelRfq(id: number, reason?: string): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${id}/close`, {
    method: "POST",
    body: { reason },
  });
}

/**
 * Get RFQ invitations for suppliers (supplier portal only)
 */
export async function listSupplierRfqInvitations(): Promise<SupplierRfqInvitationSummary[]> {
  const payload = await apiFetch<{ data: SupplierRfqInvitationSummary[] }>(
    "/rfq-workflow/supplier/invitations",
  );
  return payload.data ?? [];
}

/**
 * Submit a quote for an RFQ (suppliers only)
 */
export async function submitQuote(rfqId: number, payload: SubmitQuotePayload): Promise<Quote> {
  const response = await apiFetch<{ data: Quote }>(`/rfq-workflow/${rfqId}/quotes`, {
    method: "POST",
    body: payload,
  });
  return response.data;
}

/**
 * Withdraw a submitted quote
 */
export async function withdrawQuote(
  rfqId: number,
  quoteId: number,
  reason: string,
): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/quotes/${quoteId}/withdraw`, {
    method: "PUT",
    body: { reason },
  });
}

/**
 * Review and close an RFQ by selecting a winning quote
 */
export async function reviewRfq(
  rfqId: number,
  payload: ReviewRfqPayload,
): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/review`, {
    method: "POST",
    body: payload,
  });
}

/**
 * Get all quotes for a specific RFQ
 */
export async function fetchRfqQuotes(rfqId: number): Promise<Quote[]> {
  const response = await apiFetch<{ data: Rfq }>(`/rfq-workflow/${rfqId}`);
  return response.data?.quotes ?? [];
}

/**
 * Get review details for a closed RFQ
 */
export async function fetchRfqReview(rfqId: number): Promise<RfqReview | null> {
  try {
    const response = await apiFetch<{ data: { review?: RfqReview } }>(`/rfq-workflow/${rfqId}`);
    return response.data?.review ?? null;
  } catch (error) {
    return null;
  }
}

/**
 * Modify an existing quote (within 3-day window)
 */
export async function modifyQuote(
  rfqId: number,
  quoteId: number,
  payload: ModifyQuotePayload,
): Promise<Quote> {
  const response = await apiFetch<{ data: Quote }>(`/rfq-workflow/${rfqId}/quotes/${quoteId}`, {
    method: "PUT",
    body: payload,
  });
  return response.data;
}

/**
 * Get version history for a quote
 */
export async function fetchQuoteVersions(rfqId: number, quoteId: number): Promise<QuoteVersion[]> {
  return [];
}

/**
 * Validate an invitation token
 */
export async function validateInvitationToken(token: string): Promise<{
  valid: boolean;
  data?: {
    email: string;
    companyName: string | null;
    contactPerson: string | null;
    rfqId: number;
    rfqTitle: string;
    rfqValidUntil: string;
    expiresAt: string;
  };
}> {
  return await apiFetch<{
    valid: boolean;
    data?: {
      email: string;
      companyName: string | null;
      contactPerson: string | null;
      rfqId: number;
      rfqTitle: string;
      rfqValidUntil: string;
      expiresAt: string;
    };
  }>(`/auth/invitation/${token}`);
}

// ============================================================
// Price Comparison Functions (IDM Process Enhancement)
// ============================================================

/**
 * Add a price comparison for a quote
 */
export async function createPriceComparison(
  rfqId: number,
  quoteId: number,
  formData: FormData,
): Promise<QuotePriceComparison> {
  const response = await apiFetch<{ data: QuotePriceComparison }>(
    `/rfq-workflow/${rfqId}/price-comparison`,
    {
      method: "POST",
      body: normalizePriceComparisonFormData(formData),
      headers: {}, // Let browser set Content-Type for FormData
    },
  );
  return response.data;
}

/**
 * Get all price comparisons for a quote
 */
export async function fetchPriceComparisons(
  rfqId: number,
  quoteId: number,
): Promise<QuotePriceComparison[]> {
  const response = await apiFetch<{ data: UnknownRecord }>(`/rfq-workflow/${rfqId}`);
  const rawList = response.data?.priceComparisons;
  const list = toRecordArray(rawList);
  return list.map((item) => ({
    id: Number(item.id ?? 0),
    quote_id: quoteId,
    rfq_id: Number(item.rfq_id ?? rfqId),
    online_platform: mapPlatformFromApi(String(item.platform ?? item.online_platform ?? "")),
    online_price: Number(item.platform_price ?? item.online_price ?? 0),
    online_currency: String(item.online_currency ?? item.platform_currency ?? "CNY"),
    screenshot_file: (item.stored_file_name ?? item.screenshot_file ?? null) as string | null,
    file_path: (item.file_path ?? item.filePath ?? null) as string | null,
    price_variance_percent: Number(item.price_variance_percent ?? 0),
    product_url: (item.product_url ?? null) as string | null,
    comparison_notes: (item.notes ?? item.comparison_notes ?? null) as string | null,
    compared_by_id: (item.uploaded_by_id ?? item.compared_by_id ?? null) as string | null,
    compared_by_name: (item.uploaded_by_name ??
      item.compared_by_name ??
      item.uploaded_by ??
      null) as string | null,
    compared_at: String(item.uploaded_at ?? item.compared_at ?? ""),
  }));
}

/**
 * Get screenshot download URL for a price comparison
 */
export function getPriceComparisonScreenshotUrl(
  storedFileName?: string | null,
): string {
  if (!storedFileName) {
    return "";
  }
  if (/^https?:\/\//i.test(storedFileName)) {
    return storedFileName;
  }
  const normalized = storedFileName.startsWith("/")
    ? storedFileName
    : `/uploads/rfq-attachments/${storedFileName}`;
  return `${API_ORIGIN}${normalized}`;
}

// ============================================================
// Quote Export Functions (IDM Process Enhancement)
// ============================================================

/**
 * Export selected quote data (JSON format)
 */
export async function exportQuote(
  rfqId: number,
  quoteId: number,
  format: "json" | "excel" | "pdf" = "json",
): Promise<QuoteExportData> {
  const response = await apiFetch<{ data: UnknownRecord }>(`/rfq-workflow/${rfqId}`, {
    params: { format },
  });
  const rfq = response.data;
  const quotes = Array.isArray(rfq?.quotes) ? rfq.quotes : [];
  const quote = toRecordArray(quotes).find((entry) => Number(entry.id) === quoteId);
  if (!quote) {
    throw new Error("Quote not found");
  }

  let exportedBy: string | null = null;
  try {
    const rawUser = localStorage.getItem("user");
    if (rawUser) {
      const parsed = JSON.parse(rawUser);
      exportedBy = parsed?.name ?? parsed?.id ?? null;
    }
  } catch {}

  const rfqExport: QuoteExportData["rfq"] = {
    id: Number(rfq?.id ?? 0),
    rfq_number: toOptionalNumberOrString(
      rfq?.rfqNumber ?? rfq?.rfq_number ?? rfq?.id,
    ),
    title: toOptionalString(rfq?.title),
    material_type: toOptionalString(
      rfq?.materialType ??
        rfq?.material_type ??
        rfq?.materialCategoryType ??
        rfq?.material_category_type ??
        rfq?.rfqType,
    ),
    status: toOptionalString(rfq?.status),
  };

  const quoteExport: QuoteExportData["quote"] = {
    id: Number(quote.id ?? 0),
    supplier_name: toOptionalString(
      quote.supplierName ?? quote.supplier_name ?? quote.companyName,
    ),
    supplier_id: toOptionalNumberOrString(quote.supplierId ?? quote.supplier_id),
    contactPerson: toOptionalString(quote.contactPerson ?? quote.contact_person),
    contactEmail: toOptionalString(quote.contactEmail ?? quote.contact_email),
    contactPhone: toOptionalString(quote.contactPhone ?? quote.contact_phone),
    address: toOptionalString(quote.address),
    bankAccount: toOptionalString(quote.bankAccount ?? quote.bank_account),
    supplierPaymentTerms: toOptionalString(quote.supplierPaymentTerms),
    unit_price: toOptionalNumberOrString(quote.unitPrice ?? quote.unit_price),
    total_amount: toOptionalNumberOrString(
      quote.totalAmount ?? quote.total_amount ?? quote.total_price,
    ),
    delivery_time: toOptionalNumberOrString(
      quote.deliveryDate ?? quote.delivery_time ?? quote.delivery_period,
    ),
    payment_terms: toOptionalString(quote.paymentTerms ?? quote.payment_terms),
    currency: toOptionalString(quote.currency),
    status: toOptionalString(quote.status),
    created_at: toOptionalString(quote.createdAt ?? quote.created_at ?? quote.submitted_at),
  };

  const priceComparisonsRaw = rfq?.priceComparisons ?? rfq?.price_comparisons ?? [];
  const priceComparisons = Array.isArray(priceComparisonsRaw)
    ? (priceComparisonsRaw as QuoteExportData["priceComparisons"])
    : [];

  return {
    rfq: rfqExport,
    quote: quoteExport,
    priceComparisons,
    review: (rfq?.review ?? null) as QuoteExportData["review"],
    exportedAt: new Date().toISOString(),
    exportedBy: exportedBy ?? "system",
  };
}

// ============================================================
// RFQ Workflow Functions (New Line-Item Based Workflow)
// ============================================================

export interface RfqWorkflowCreatePayload {
  materialCategoryType?: "IDM" | "DM";
  title: string;
  description?: string;
  rfqType: string;
  deliveryPeriod: number;
  budgetAmount?: number;
  currency?: string;
  validUntil: string;
  requestingParty?: string;
  requestingDepartment?: string;
  requirementDate?: string;
  lineItems: Array<{
    materialCategory: string;
    brand?: string;
    itemName: string;
    specifications?: string;
    quantity: number;
    unit: string;
    estimatedUnitPrice?: number;
    currency?: string;
    parameters?: string;
    notes?: string;
  }>;
  supplierIds?: number[];
  externalEmails?: string[];
  minSupplierCount?: number;
  supplierExceptionNote?: string;
  requiredDocuments?: string[];
  evaluationCriteria?: Record<string, number | string | undefined>;
}

/**
 * Create RFQ with line items (new workflow)
 */
export async function createRfqWorkflow(payload: RfqWorkflowCreatePayload): Promise<Rfq> {
  const response = await apiFetch<{ data: Rfq }>("/rfq-workflow/create", {
    method: "POST",
    body: payload,
  });
  return response.data;
}

/**
 * Get RFQ with line items (new workflow)
 */
export async function fetchRfqWorkflow(id: number): Promise<Rfq> {
  const response = await apiFetch<{ data: Rfq }>(`/rfq-workflow/${id}`);
  return response.data;
}

/**
 * Publish RFQ (new workflow)
 */
export async function publishRfqWorkflow(id: number): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${id}/publish`, {
    method: "POST",
  });
}

/**
 * Select winning quote (new workflow)
 */
export async function selectQuoteWorkflow(
  rfqId: number,
  quoteId: number
): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/select-quote`, {
    method: "POST",
    body: { selectedQuoteId: quoteId },
  });
}

/**
 * Submit review and initiate approval workflow (new workflow)
 */
export async function submitReviewWorkflow(rfqId: number): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/submit-review`, {
    method: "POST",
  });
}

/**
 * Get approval workflow status
 */
export interface RfqApprovalComment {
  id: number;
  approval_id?: number;
  approvalId?: number;
  author_id?: number;
  authorId?: number;
  author_name?: string;
  authorName?: string;
  content: string;
  created_at?: string;
  createdAt?: string;
}

export interface RfqApprovalStep {
  id: number;
  rfq_id?: number;
  rfqId?: number;
  step_order?: number;
  stepOrder?: number;
  step_name?: string;
  stepName?: string;
  approver_role?: string;
  approverRole?: string;
  approver_name?: string;
  approverName?: string;
  status: string;
  comments?: RfqApprovalComment[] | string;
  rejection_reason?: string;
  rejectionReason?: string;
  decided_at?: string;
  decidedAt?: string;
}

export async function fetchApprovalWorkflow(rfqId: number): Promise<RfqApprovalStep[]> {
  const response = await apiFetch<{ data: RfqApprovalStep[] }>(`/rfq-workflow/${rfqId}/approvals`);
  return response.data;
}

/**
 * Approve RFQ (manager or director)
 */
export async function approveRfqWorkflow(
  rfqId: number,
  approvalId: number,
  payload: { comments?: string }
): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/approvals/${approvalId}/approve`, {
    method: "POST",
    body: payload,
  });
}

/**
 * Reject RFQ (manager or director)
 */
export async function rejectRfqWorkflow(
  rfqId: number,
  approvalId: number,
  payload: { reason: string }
): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/approvals/${approvalId}/reject`, {
    method: "POST",
    body: payload,
  });
}

/**
 * Add comment to RFQ approval step
 */
export async function addRfqApprovalComment(
  rfqId: number,
  approvalId: number,
  payload: { content: string }
): Promise<{ message: string; data: Record<string, unknown> }> {
  return await apiFetch<{ message: string; data: Record<string, unknown> }>(
    `/rfq-workflow/${rfqId}/approvals/${approvalId}/comments`,
    {
      method: "POST",
      body: payload,
    }
  );
}

/**
 * Invite purchasers to comment on RFQ approval
 */
export async function invitePurchaserToComment(
  rfqId: number,
  approvalId: number,
  payload: { purchaserIds: (string | number)[]; message?: string }
): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(
    `/rfq-workflow/${rfqId}/approvals/${approvalId}/invite-purchasers`,
    {
      method: "POST",
      body: payload,
    }
  );
}

/**
 * Upload price comparison attachment
 */
export async function uploadPriceComparison(
  rfqId: number,
  platform: string,
  file: File,
  price: number,
  url: string,
  lineItemId?: number
): Promise<{ message: string }> {
  const formData = new FormData();
  formData.append("screenshot", file);
  formData.append("platform", mapPlatformToApi(platform));
  formData.append("platformPrice", String(price)); // Backend expects platformPrice, not price
  formData.append("productUrl", url); // Backend expects productUrl, not url
  if (lineItemId) {
    formData.append("lineItemId", String(lineItemId)); // Include lineItemId for multi-line RFQs
  }

  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/price-comparison`, {
    method: "POST",
    body: formData,
  });
}

/**
 * Get price comparison attachments for workflow
 */
export async function fetchWorkflowPriceComparisons(
  rfqId: number,
): Promise<Record<string, unknown>[]> {
  const response = await apiFetch<{ data: UnknownRecord }>(`/rfq-workflow/${rfqId}`);
  return toRecordArray(response.data?.priceComparisons);
}

// ============================================================
// PR Fill Functions (Post-Approval Phase)
// ============================================================

export interface FillPrPayload {
  prNumber: string;
  prDate: string;
  notes?: string;
}

export interface ConfirmPrPayload {
  confirmationStatus: "confirmed" | "rejected";
  confirmationNotes?: string;
}

/**
 * Fill PR number after approval (purchaser only)
 */
export async function fillPr(rfqId: number, payload: FillPrPayload): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/fill-pr`, {
    method: "POST",
    body: payload,
  });
}

/**
 * Confirm or reject PR (department confirmation)
 */
export async function confirmPr(
  rfqId: number,
  payload: ConfirmPrPayload,
): Promise<{ message: string }> {
  return await apiFetch<{ message: string }>(`/rfq-workflow/${rfqId}/confirm-pr`, {
    method: "POST",
    body: payload,
  });
}

/**
 * Excel导入相关接口
 */

export interface RfqRequirement {
  itemName: string;
  itemCode?: string;
  quantity: number;
  unit: string;
  estimatedUnitPrice?: number;
  estimatedAmount?: number;
  notes?: string;
}

export interface ImportExcelResult {
  title: string;
  requestingDepartment: string;
  requirementDate: string;
  currency: string;
  budgetAmount: number;
  description: string;
  requirements: RfqRequirement[];
  recommendedSuppliers: string[];
  prNumber: string;
  metadata: {
    importedAt: string;
    rowCount: number;
    sheetName: string;
  };
}

/**
 * 从Excel导入RFQ数据
 * @param formData - 包含Excel文件的FormData对象
 * @returns 解析后的RFQ数据
 */
export async function importRfqFromExcel(formData: FormData): Promise<ImportExcelResult> {
  const response = await apiFetch<{ data: ImportExcelResult }>("/rfq-workflow/import-excel", {
    method: "POST",
    body: formData,
  });
  return response.data;
}

/**
 * 生成PR Excel文件
 * @param rfqId - RFQ ID
 * @param lineItemIds - 选中的line item IDs
 * @param options - 生成选项
 * @returns Excel文件Blob对象和调试信息
 */
export async function generatePrExcel(
  rfqId: number,
  lineItemIds: number[],
  options?: {
    prNumber?: string
    department?: string
    accountNo?: string
  }
): Promise<Blob> {
  const token = localStorage.getItem('token')

  const response = await fetch(`${BASE_URL}/rfq-workflow/${rfqId}/generate-pr-excel`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token && { 'Authorization': `Bearer ${token}` })
    },
    body: JSON.stringify({
      lineItemIds,
      ...options
    })
  })

  if (!response.ok) {
    // Parse structured error response
    let errorData: Record<string, unknown> = {}
    try {
      const parsed = await response.json()
      if (parsed && typeof parsed === "object") {
        errorData = parsed as Record<string, unknown>
      } else {
        errorData = { message: 'Failed to generate PR Excel file' }
      }
    } catch {
      errorData = { message: 'Failed to generate PR Excel file' }
    }

    const received = errorData.received ?? 'N/A'
    const fileSizeKB = errorData.fileSizeKB ?? 'N/A'
    const maxSizeKB = errorData.maxSizeKB ?? 'N/A'

    // Map backend error codes to user-friendly messages
    const errorMessages: Record<string, string> = {
      'INVALID_RFQ_ID': '无效的RFQ ID',
      'INVALID_LINE_ITEMS': 'No line items selected',
      'INVALID_LINE_ITEM_IDS': '选择的明细项ID无效',
      'TOO_MANY_LINE_ITEMS': 'Too many line items (max 1000). Selected: ' + String(received),
      'RESOURCE_NOT_FOUND': '找不到指定的RFQ或明细项',
      'NO_VALID_LINE_ITEMS': '所选明细项中没有已批准的报价，无法生成PR',
      'INVALID_LINE_ITEM_STATUS': '存在状态不允许导出的行项目',
      'MISSING_SELECTED_QUOTE': 'Missing selected quote for some line items',
      'TEMPLATE_ERROR': 'Excel模板文件缺失或损坏，请联系系统管理员',
      'INVALID_BUFFER': '生成的Excel文件无效',
      'FILE_TOO_SMALL': 'Generated file is too small (' + String(fileSizeKB) + ' KB).',
      'FILE_TOO_LARGE': 'Generated file is too large (' + String(fileSizeKB) + ' KB). Max ' + String(maxSizeKB) + ' KB',
      'GENERATION_FAILED': 'Failed to generate PR Excel file'
    }

    const errorCode = typeof errorData.error === 'string' ? errorData.error : 'GENERATION_FAILED'
    const messageFromApi = typeof errorData.message === 'string' ? errorData.message : undefined
    const userMessage = errorMessages[errorCode] || messageFromApi || '生成PR Excel失败'

    // Create enhanced error with both user message and technical details
    const error = new Error(userMessage) as Error & {
      code?: string
      details?: unknown
      status?: number
    }
    error.code = errorCode
    error.details = errorData.details
    error.status = response.status

    throw error
  }

  // Extract debug headers for logging
  const fileSizeKB = response.headers.get('X-File-Size-KB')
  const generationTimeMs = response.headers.get('X-Generation-Time-Ms')
  const lineItemCount = response.headers.get('X-Line-Item-Count')

  if (fileSizeKB && generationTimeMs) {
    console.log('[PR Excel] Generated successfully: ' + fileSizeKB + ' KB in ' + generationTimeMs + 'ms (' + lineItemCount + ' items)')
  }

  return await response.blob()
}

// ============================================================
// RFQ Line Item Pending Approvals
// ============================================================

/**
 * Get pending RFQ line items for current user
 * Different roles see different pending items:
 * - purchaser: pending_po, draft with selected_quote_id (rejected items)
 * - procurement_director: pending_director
 */
export interface PendingRfqLineItem {
  id: number;
  rfqId: number;
  rfqTitle: string;
  lineNumber: number;
  itemName: string;
  status: string;
  currentApproverRole: string | null;
  selectedQuoteId: number | null;
  supplierName: string | null;
  quantity: number;
  unit: string;
  estimatedUnitPrice: number;
  requestingDepartment: string;
  updatedAt: string;
}

export async function fetchPendingRfqLineItems(params?: {
  status?: string
}): Promise<{ data: PendingRfqLineItem[] }> {
  return await apiFetch<{ data: PendingRfqLineItem[] }>(
    "/rfq/line-items/pending-approvals",
    { params }
  );
}





