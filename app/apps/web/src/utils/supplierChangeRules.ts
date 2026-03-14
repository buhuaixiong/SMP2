import { SupplierStatus } from "@/types";
import type {
  Supplier,
  SupplierChangeField,
  SupplierChangeRiskLevel,
  SupplierChangeRequest,
  SupplierChangeStep,
} from "@/types";

const CRITICAL_FIELDS = new Set([
  "bankAccount",
  "paymentTerms",
  "paymentMethods",
  "businessRegistrationNumber",
  "qualityCertifications",
  "invoiceType",
]);

const QUALITY_REVIEW_FIELDS = new Set(["qualityCertifications"]);

const FIELD_LABELS: Record<string, string> = {
  companyName: "Primary display name",
  companyId: "Registration ID",
  englishName: "English name",
  chineseName: "Chinese name",
  stage: "Supplier stage",
  category: "Supplier category",
  contactPerson: "Primary contact",
  contactPhone: "Primary phone",
  contactEmail: "Primary email",
  serviceCategory: "Service category",
  region: "Region",
  importance: "Importance",
  address: "Mailing address",
  bankAccount: "Bank account",
  paymentTerms: "Payment terms",
  paymentTermsDays: "Payment terms (days)",
  paymentCurrency: "Payment currency",
  paymentMethods: "Payment methods",
  paymentMethodsOther: "Other payment method detail",
  creditRating: "Credit rating",
  financialContact: "Finance contact",
  complianceStatus: "Compliance status",
  complianceNotes: "Compliance notes",
  complianceOwner: "Compliance owner",
  complianceReviewedAt: "Compliance reviewed at",
  englishContact: "English contact",
  companyType: "Company type",
  companyTypeOther: "Other company type detail",
  authorizedCapital: "Authorized capital",
  issuedCapital: "Issued capital",
  directors: "Directors / legal representatives",
  owners: "Owners / partners",
  registeredOffice: "Registered office",
  businessRegistrationNumber: "Business registration number",
  businessAddress: "Operational address",
  businessPhone: "Business phone",
  faxNumber: "Fax number",
  salesContactName: "Sales contact name",
  salesContactEmail: "Sales contact email",
  salesContactPhone: "Sales contact phone",
  financeContactName: "Finance contact name",
  financeContactEmail: "Finance contact email",
  financeContactPhone: "Finance contact phone",
  customerServiceContactName: "Customer service contact name",
  customerServiceContactEmail: "Customer service contact email",
  customerServiceContactPhone: "Customer service contact phone",
  businessNature: "Nature of business",
  operatingCurrency: "Operating currency",
  deliveryLocation: "Delivery location",
  shipCode: "Ship code",
  productsForEci: "Products sold to ECI",
  establishedYear: "Year established",
  employeeCount: "Number of employees",
  qualityCertifications: "Quality certifications",
  invoiceType: "Invoice type",
  bankName: "Bank name",
  bankAddress: "Bank address",
  swiftCode: "Swift code",
  notes: "Additional notes",
};

const TRACKED_FIELDS = Object.keys(FIELD_LABELS);

const normalizeForCompare = (value: unknown) => {
  if (Array.isArray(value)) {
    return value
      .map((entry) => (entry === null || entry === undefined ? "" : String(entry).trim()))
      .filter((entry) => entry.length > 0)
      .sort()
      .join("||");
  }
  if (value === null || value === undefined) return "";
  return String(value).trim();
};

const formatValue = (value: unknown): string | null => {
  if (Array.isArray(value)) {
    const normalized = value
      .map((entry) => (entry === null || entry === undefined ? "" : String(entry).trim()))
      .filter((entry) => entry.length > 0);
    return normalized.length ? normalized.join(", ") : null;
  }
  if (typeof value === "boolean") {
    return value ? "Yes" : "No";
  }
  if (value === null || value === undefined || value === "") {
    return null;
  }
  return String(value);
};

export interface SupplierChangeAnalysis {
  fields: SupplierChangeField[];
  riskLevel: SupplierChangeRiskLevel;
  includeQualityReview: boolean;
  payload: Record<string, unknown>;
}

export const analyseSupplierChanges = (
  original: Supplier,
  updated: Record<string, unknown>,
): SupplierChangeAnalysis => {
  const payload: Record<string, unknown> = {};
  const fields: SupplierChangeField[] = [];

  const originalRecord = original as unknown as Record<string, unknown>;

  TRACKED_FIELDS.forEach((field) => {
    const previous = originalRecord[field];
    const next = updated[field];
    if (next === undefined) return;

    const previousComparable = normalizeForCompare(previous);
    const nextComparable = normalizeForCompare(next);

    if (previousComparable === nextComparable) return;

    payload[field] = next;
    fields.push({
      field,
      label: FIELD_LABELS[field] ?? field,
      previousValue: formatValue(previous),
      newValue: formatValue(next),
    });
  });

  const riskLevel: SupplierChangeRiskLevel = fields.some((item) => CRITICAL_FIELDS.has(item.field))
    ? "high"
    : "low";
  const includeQualityReview = fields.some((item) => QUALITY_REVIEW_FIELDS.has(item.field));

  return {
    fields,
    riskLevel,
    includeQualityReview,
    payload,
  };
};
export const stepRoleLabels: Record<string, string> = {
  purchaser: "Purchaser approval",
  quality: "Quality review",
  purchase_manager: "Purchasing manager approval",
  purchase_director: "Purchasing director approval",
  finance_director: "Finance director approval",
};

export const stepRoleStatusMap: Record<string, SupplierStatus> = {
  purchaser: SupplierStatus.PENDING_PURCHASER,
  quality: SupplierStatus.PENDING_QUALITY_REVIEW,
  purchase_manager: SupplierStatus.PENDING_PURCHASE_MANAGER,
  purchase_director: SupplierStatus.PENDING_PURCHASE_DIRECTOR,
  finance_director: SupplierStatus.PENDING_FINANCE_MANAGER,
};

const createStep = (role: keyof typeof stepRoleLabels): SupplierChangeStep => ({
  id: `${role}-${Math.random().toString(36).slice(2, 10)}`,
  name: stepRoleLabels[role],
  role,
  status: "pending",
});

export const buildApprovalSteps = (
  riskLevel: SupplierChangeRiskLevel,
  includeQualityReview: boolean,
): SupplierChangeStep[] => {
  const steps: SupplierChangeStep[] = [createStep("purchaser")];

  if (riskLevel === "high") {
    if (includeQualityReview) {
      steps.push(createStep("quality"));
    }
    steps.push(createStep("purchase_manager"));
    steps.push(createStep("purchase_director"));
    steps.push(createStep("finance_director"));
  }

  return steps;
};

export const cloneChangeRequest = (request: SupplierChangeRequest): SupplierChangeRequest => ({
  ...request,
  fields: request.fields.map((field) => ({ ...field })),
  steps: request.steps.map((step) => ({ ...step })),
  payload: { ...request.payload },
});
