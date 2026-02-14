export enum SupplierStage {
  TEMPORARY = "temporary",

  OFFICIAL = "official",
}

export enum SupplierStatus {
  POTENTIAL = "potential",

  UNDER_REVIEW = "under_review",

  PENDING_INFO = "pending_info",

  PENDING_PURCHASER = "pending_purchaser",

  PENDING_QUALITY_REVIEW = "pending_quality_review",

  PENDING_PURCHASE_MANAGER = "pending_purchase_manager",

  PENDING_PURCHASE_DIRECTOR = "pending_purchase_director",

  PENDING_FINANCE_MANAGER = "pending_finance_manager",

  APPROVED = "approved",

  QUALIFIED = "qualified",

  DISQUALIFIED = "disqualified",

  SUSPENDED = "suspended",

  TERMINATED = "terminated",

  REJECTED = "rejected",
}

export enum SupplierCompanyType {
  LIMITED = "limited",

  SOLE_PROPRIETOR = "sole_proprietor",

  PARTNERSHIP = "partnership",

  OTHER = "other",
}

export enum UserRole {
  ADMIN = "admin",

  PURCHASER = "purchaser",

  PURCHASE_MANAGER = "purchase_manager",

  FINANCE_MANAGER = "finance_manager",

  SQE = "sqe",

  SUPPLIER = "supplier",

  TEMP_SUPPLIER = "temp_supplier",

  FORMAL_SUPPLIER = "formal_supplier",

  PROCUREMENT_MANAGER = "procurement_manager",

  PROCUREMENT_DIRECTOR = "procurement_director",

  FINANCE_ACCOUNTANT = "finance_accountant",

  FINANCE_CASHIER = "finance_cashier",

  FINANCE_DIRECTOR = "finance_director",
}

export interface ApprovalRecord {
  step: string;

  approver: string;

  result: "approved" | "rejected";

  date: string;

  comments?: string | null;
}

export interface SupplierFile {
  id: number;

  agreementNumber: string | null;

  fileType: string | null;

  validFrom: string | null;

  validTo: string | null;

  supplierId: number;

  status: string | null;

  uploadTime: string;

  uploaderName: string | null;

  originalName: string | null;

  storedName: string | null;
}

export interface SupplierTag {
  id: number;

  name: string;

  description?: string | null;

  color?: string | null;
}

export interface SupplierDocument {
  id: number;

  supplierId: number;

  docType: string | null;

  storedName: string;

  originalName: string;

  uploadedAt: string;

  uploadedBy: string | null;

  validFrom?: string | null;

  expiresAt: string | null;

  status: string | null;

  notes: string | null;

  fileSize?: number | null;

  category?: string | null;

  isRequired?: boolean | number;

  // Alias for compatibility (some views use 'filename' instead of 'originalName')
  filename?: string;
}

export interface SupplierComplianceFieldStatus {
  key: string;

  label: string;

  value: unknown;

  complete: boolean;
}

export interface SupplierComplianceDocumentStatus {
  type: string;

  label: string;

  uploaded: boolean;
}

export type SupplierCompletionStatus = "complete" | "mostly_complete" | "needs_attention";

export interface SupplierComplianceMissingItem {
  type: "profile" | "document";
  key: string;
  label: string;
}

export interface SupplierComplianceSummary {
  requiredProfileFields: SupplierComplianceFieldStatus[];

  missingProfileFields: Array<{ key: string; label: string }>;

  requiredDocumentTypes: SupplierComplianceDocumentStatus[];

  missingDocumentTypes: Array<{ type: string; label: string }>;

  isProfileComplete: boolean;

  isDocumentComplete: boolean;

  isComplete: boolean;

  profileScore: number;

  documentScore: number;

  overallScore: number;

  completionCategory: SupplierCompletionStatus;

  missingItems: SupplierComplianceMissingItem[];
}

// Alias for compatibility
export type ComplianceSummary = SupplierComplianceSummary;

export interface SupplierRfqInvitationSummary {
  id: number;

  title: string;

  description: string | null;

  rfqType: string | null;

  deliveryPeriod: number | string | null;

  budgetAmount: number | null;

  currency: string | null;

  validUntil: string | null;

  createdAt: string | null;

  rfqStatus: string | null;

  quoteId: number | null;

  quoteStatus: string | null;

  quoteSubmittedAt: string | null;

  daysRemaining: number | null;
  denialReason?: string | null;

  needsResponse: boolean;
}

export interface ContractVersion {
  id: number;

  contractId: number;

  versionNumber: number;

  storedName: string;

  originalName: string;

  changeLog: string | null;

  createdAt: string;

  createdBy: string | null;

  fileSize?: number | null;
}

export interface Contract {
  id: number;

  supplierId: number;

  title: string;

  agreementNumber: string;

  amount: number | null;

  currency: string | null;

  status: string | null;

  paymentCycle: string | null;

  effectiveFrom: string | null;

  effectiveTo: string | null;

  autoRenew: boolean;

  isMandatory: boolean;

  createdBy: string | null;

  createdAt: string;

  updatedAt: string | null;

  notes: string | null;

  versions?: ContractVersion[];
}

export interface ContractReminderSettings {
  leadDays: number[];

  channels: string[];

  autoNotify: boolean;

  remindExpired: boolean;
}

export interface ContractReminderBucketSummary {
  key: string;

  leadDays?: number;

  contractCount: number;

  supplierCount: number;
}

export interface ContractReminderSummary {
  settings: ContractReminderSettings;

  expired: {
    contractCount: number;

    supplierCount: number;
  };

  buckets: ContractReminderBucketSummary[];
}

export interface ContractReminderItem {
  id: number;

  supplierId: number;

  supplierName: string;

  title: string | null;

  agreementNumber: string | null;

  amount: number | null;

  currency: string | null;

  status: string | null;

  effectiveTo: string | null;

  autoRenew: boolean;

  isMandatory?: boolean;

  bucket: string;

  daysRemaining: number | null;
}

export interface SupplierRating {
  id: number;

  supplierId: number;

  periodStart: string | null;

  periodEnd: string | null;

  onTimeDelivery: number | null;

  qualityScore: number | null;

  serviceScore: number | null;

  costScore: number | null;

  overallScore: number | null;

  notes: string | null;

  createdAt: string;

  createdBy: string | null;
}

export interface RatingsSummary {
  totalEvaluations: number;

  overallAverage: number | null;

  avgOnTimeDelivery: number | null;

  avgQualityScore: number | null;

  avgServiceScore: number | null;

  avgCostScore: number | null;
}

export type SupplierChangeRiskLevel = "low" | "high";

export interface SupplierChangeField {
  field: string;

  label: string;

  previousValue: string | null;

  newValue: string | null;
}

export interface SupplierChangeStep {
  id: string;

  name: string;

  role: string;

  status: "pending" | "approved" | "rejected";

  completedAt?: string | null;
}

export interface SupplierChangeRequest {
  id: string;

  supplierId: number;

  riskLevel: SupplierChangeRiskLevel;

  includeQualityReview: boolean;

  status: "pending" | "approved" | "rejected";

  submittedAt: string | null;

  submittedBy?: string | null;

  previousStatus: SupplierStatus | string;

  fields: SupplierChangeField[];

  steps: SupplierChangeStep[];

  currentStepIndex: number;

  payload: Record<string, unknown>;

  // Enriched fields from supplier data
  companyName?: string;
  companyId?: string;
  currentStep?: string;
}

export interface Supplier {
  id: number;

  companyName: string;

  companyId: string;

  contactPerson: string;

  contactPhone: string;

  contactEmail: string;

  purchaserEmail?: string | null;

  category: string;

  address: string;

  status: SupplierStatus;

  stage?: SupplierStage | null;

  currentApprover: string | null;

  createdBy?: string | null;

  createdAt: string;

  updatedAt?: string | null;

  notes?: string | null;

  bankAccount?: string | null;

  paymentTerms?: string | null;

  creditRating?: string | null;

  serviceCategory?: string | null;

  region?: string | null;

  importance?: string | null;

  complianceStatus?: string | null;

  complianceNotes?: string | null;

  complianceOwner?: string | null;

  complianceReviewedAt?: string | null;

  financialContact?: string | null;

  paymentCurrency?: string | null;

  englishName?: string | null;

  chineseName?: string | null;

  companyType?: SupplierCompanyType | null;

  companyTypeOther?: string | null;

  authorizedCapital?: string | null;

  issuedCapital?: string | null;

  directors?: string | null;

  owners?: string | null;

  registeredOffice?: string | null;

  businessRegistrationNumber?: string | null;

  businessAddress?: string | null;

  businessPhone?: string | null;

  faxNumber?: string | null;

  salesContactName?: string | null;

  salesContactEmail?: string | null;

  salesContactPhone?: string | null;

  financeContactName?: string | null;

  financeContactEmail?: string | null;

  financeContactPhone?: string | null;

  customerServiceContactName?: string | null;

  customerServiceContactEmail?: string | null;

  customerServiceContactPhone?: string | null;

  businessNature?: string | null;

  operatingCurrency?: string | null;

  deliveryLocation?: string | null;

  shipCode?: string | null;

  productsForEci?: string | null;

  establishedYear?: string | null;

  employeeCount?: string | null;

  qualityCertifications?: string | null;

  invoiceType?: string | null;

  paymentTermsDays?: string | null;

  paymentMethods?: string[] | null;

  paymentMethodsOther?: string | null;

  bankName?: string | null;

  bankAddress?: string | null;

  swiftCode?: string | null;

  pendingChangeRequest?: SupplierChangeRequest | null;

  tags: SupplierTag[];

  documents: SupplierDocument[];

  ratingsSummary: RatingsSummary;

  latestRating: SupplierRating | null;

  contracts: Contract[];

  approvalHistory: ApprovalRecord[];

  fileApprovals: FileApprovalReview[];

  files: SupplierFile[];

  complianceSummary: SupplierComplianceSummary;

  profileCompletion: number;

  documentCompletion: number;

  completionScore: number;

  completionStatus: SupplierCompletionStatus;

  missingRequirements: SupplierComplianceMissingItem[];

  relatedApplicationId?: number | null;

  registrationCompleted?: boolean | number | string | null;

  productOrigin?: string | null;
}

export interface SupplierImportError {
  row: number;

  message: string;
}

export interface SupplierImportSummary {
  sheetName?: string | null;

  scannedRows: number;

  importedRows: number;

  created: number;

  updated: number;

  skipped: number;

  passwordResets: number;

  errors: SupplierImportError[];
}

export interface SupplierImportResult {
  rowNumber: number;

  action: "created" | "updated";

  supplierId: number;

  companyId: string;

  companyName: string;

  defaultPassword?: string | null;
}

export interface SupplierImportResponse {
  summary: SupplierImportSummary;

  results: SupplierImportResult[];
}

export interface OrgUnitMembership {
  unitId: number;
  unitName: string;
  unitCode: string;
  memberRole: "member" | "lead" | "admin";
  function?: string | null;
}

export interface AdminUnit {
  id: number;
  name: string;
  code: string;
  function?: string | null;
}

export interface User {
  id: string;

  name: string;

  role: UserRole;

  supplierId?: string | number | null;
  tempAccountId?: number | null;
  relatedApplicationId?: number | null;
  accountType?: string | null;

  email?: string | null;

  permissions?: string[];

  // User lifecycle management
  status?: "active" | "frozen" | "deleted";
  lastLoginAt?: string;
  freezeReason?: string;

  // Enhanced organizational unit fields
  orgUnits?: OrgUnitMembership[];
  adminUnits?: AdminUnit[];
  isOrgUnitAdmin?: boolean;
  functions?: string[]; // ['procurement', 'finance', 'quality']
}

export interface LoginCredentials {
  username: string;

  password: string;

  role?: UserRole | string;
}

export interface ApiResponse<T> {
  data: T;

  message?: string;
}

export interface GlobalSearchResult {
  suppliers: Array<
    Pick<
      Supplier,
      | "id"
      | "companyName"
      | "companyId"
      | "contactPerson"
      | "category"
      | "status"
      | "region"
      | "importance"
    >
  >;

  contracts: Array<
    Pick<
      Contract,
      | "id"
      | "supplierId"
      | "title"
      | "agreementNumber"
      | "status"
      | "effectiveTo"
      | "amount"
      | "currency"
    >
  >;

  documents: Array<
    Pick<
      SupplierDocument,
      "id" | "supplierId" | "docType" | "originalName" | "uploadedAt" | "expiresAt" | "status"
    >
  >;
}

export interface UpgradeRequirement {
  code: string;
  name: string;
  description: string;
  required: boolean;
  template?: TemplateFileRecord | null;
  fulfilled?: boolean;
  documentId?: number | null;
}

// Requirement row for upload form in upgrade management
export interface RequirementRow {
  code: string;
  name: string;
  description: string;
  required: boolean;
  documentType: string;
  validFrom: Date | null;
  validTo: Date | null;
  file: File | null;
  status?: string;
  uploadProgress?: number;
}

export interface UpgradeDocumentFile {
  id: number;
  originalName: string | null;
  storedName: string | null;
  fileType: string | null;
  uploadTime: string | null;
  validFrom?: string | null;
  validTo?: string | null;
}

export interface UpgradeApplicationDocument {
  id: number;
  requirementCode: string;
  requirementName: string;
  status: string;
  notes: string | null;
  uploadedAt: string;
  uploadedBy: string | null;
  file: UpgradeDocumentFile | null;
}

export interface UpgradeWorkflowStep {
  id: number;
  order: number;
  name: string;
  key: string;
  permission: string | null;
  status: string;
  dueAt: string | null;
  completedAt: string | null;
  notes: string | null;
}

// Approval source identifier for different approval types
export type ApprovalSource =
  | 'file_upload'           // File upload approval workflow
  | 'upgrade_application'   // Upgrade application review workflow
  | 'contract_review'       // Contract review (future)
  | 'qualification_review'  // Qualification review (future)

// File upload approval record
export interface FileApprovalReview {
  id: number;
  uploadId: number;
  source: 'file_upload';
  stepKey: string;
  stepName: string;
  decision: "approved" | "rejected";
  comments: string | null;
  decidedById: string | null;
  decidedByName: string | null;
  decidedAt: string;  // ISO 8601 format
  fileName?: string;
  fileDescription?: string;
  riskLevel?: string;
}

// Upgrade application review record
export interface UpgradeReview {
  id: number;
  source: 'upgrade_application';
  stepKey: string;
  stepName: string;
  decision: "approved" | "rejected";
  comments: string | null;
  decidedById: string | null;
  decidedByName: string | null;
  decidedAt: string;  // ISO 8601 format
}

export interface UpgradeWorkflowSummary {
  id: number;
  status: string;
  currentStep: string | null;
  createdBy: string | null;
  createdAt: string;
  updatedAt: string;
  steps: UpgradeWorkflowStep[];
}

export interface UpgradeApplicationInfo {
  id: number;
  supplierId: number;
  status: string;
  currentStep: string | null;
  submittedAt: string;
  submittedBy: string | null;
  dueAt: string | null;
  workflowId: number | null;
  rejectionReason: string | null;
  resubmittedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpgradeApplicationSummary {
  application: UpgradeApplicationInfo;
  workflow: UpgradeWorkflowSummary | null;
  documents: UpgradeApplicationDocument[];
  reviews: UpgradeReview[];
  requirements: UpgradeRequirement[];
}

export interface UpgradeStatus {
  supplierId: number;
  status: string;
  application: UpgradeApplicationInfo | null;
  workflow: UpgradeWorkflowSummary | null;
  documents: UpgradeApplicationDocument[];
  reviews: UpgradeReview[];
  fileApprovals: FileApprovalReview[];  // File upload approval history
  requirements: UpgradeRequirement[];
}

export interface WorkflowStep {
  id: number;

  workflowId: number;

  stepOrder: number;

  name: string;

  assignee: string | null;

  status: string | null;

  dueAt: string | null;

  completedAt: string | null;

  notes: string | null;
}

export interface WorkflowInstance {
  id: number;

  workflowType: string;

  entityType: string;

  entityId: string;

  status: string | null;

  currentStep: string | null;

  createdBy: string | null;

  createdAt: string;

  updatedAt: string | null;

  steps: WorkflowStep[];
}

export interface TemplateFileRecord {
  id: number;

  templateCode: string;

  templateName: string;

  description: string | null;

  originalName: string;

  storedName: string;

  fileType: string | null;

  fileSize: number | null;

  uploadedBy: string | null;

  uploadedAt: string;

  isActive: number | boolean;

  downloadUrl: string;
}

export interface TemplateDefinition {
  code: string;

  name: string;

  description: string;

  file: TemplateFileRecord | null;
}

export interface TemplateHistory {
  definition: {
    code: string;

    name: string;

    description: string;
  };

  history: TemplateFileRecord[];
}

export interface CreateUserPayload {
  id: string;

  username: string;

  name: string;

  role: string;

  password: string;

  supplierId?: string | number | null;

  email?: string | null;
}

export interface BuyerSummary {
  id: string;

  name: string;

  role: string;

  assignmentCount: number;

  contractAlertCount: number;

  profileAccessCount: number;
}

export interface BuyerAssignment {
  id?: number;
  buyerId?: string;
  buyerName?: string | null;
  buyerRole?: string | null;
  supplierId: number;
  companyName: string;
  companyId: string;
  contractAlert: boolean;
  profileAccess: boolean;
  category?: string | null;
  region?: string | null;
  groupIsPrimary?: number | null;
  groupNotes?: string | null;
  createdAt?: string | null;
  createdBy?: string | null;
}

export interface SupplierSummary {
  id: number;

  companyName: string;

  companyId: string;

  supplierCode?: string | null;
}

// Purchasing Groups

export enum PurchasingGroupMemberRole {
  MEMBER = "member",
  LEAD = "lead",
}

export interface PurchasingGroup {
  id: number;
  code: string;
  name: string;
  description?: string | null;
  category?: string | null;
  region?: string | null;
  isActive: number;
  memberCount?: number;
  supplierCount?: number;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  deletedAt?: string | null;
  deletedBy?: string | null;
}

export interface PurchasingGroupMember {
  id: number;
  groupId: number;
  buyerId: string;
  buyerName: string;
  buyerRole: string;
  role: PurchasingGroupMemberRole | string;
  joinedAt: string;
  assignedBy: string;
  notes?: string | null;
}

export interface PurchasingGroupSupplier {
  id: number;
  groupId: number;
  supplierId: number;
  companyName: string;
  companyId: string;
  category?: string | null;
  region?: string | null;
  status?: string | null;
  isPrimary: number;
  assignedAt: string;
  assignedBy: string;
  notes?: string | null;
}

export interface PurchasingGroupDetail extends PurchasingGroup {
  members: PurchasingGroupMember[];
  suppliers: PurchasingGroupSupplier[];
}

export interface BuyerWorkload {
  buyerId: string;
  buyerName: string;
  directCount: number;
  groupCount: number;
  totalCount: number;
  avgSupplierScore: number | null;
  workloadRatio: number;
}

export interface WorkloadSummary {
  totalBuyers: number;
  totalSuppliers: number;
  avgWorkload: number;
  maxWorkload: number;
  minWorkload: number;
  imbalanceRatio: number;
}

export interface WorkloadAnalysis {
  buyers: BuyerWorkload[];
  summary: WorkloadSummary;
}

export interface CreatePurchasingGroupPayload {
  code: string;
  name: string;
  description?: string;
  category?: string;
  region?: string;
}

export interface UpdatePurchasingGroupPayload {
  name?: string;
  description?: string;
  category?: string;
  region?: string;
  isActive?: boolean;
}

export interface AddGroupMembersPayload {
  buyerIds: string[];
  role?: PurchasingGroupMemberRole | string;
  notes?: string;
}

export interface AddGroupSuppliersPayload {
  supplierIds: number[];
  isPrimary?: boolean;
  notes?: string;
}

// RFQ (Request for Quotation) Types

export enum RfqMaterialType {
  IDM = "IDM",
  DM = "DM",
}

export enum RfqDistributionCategory {
  EQUIPMENT = "equipment",
  AUXILIARY_MATERIALS = "auxiliary_materials",
}

export enum RfqDistributionSubcategory {
  // Equipment subcategories
  STANDARD = "standard",
  NON_STANDARD = "non_standard",
  FIXTURES = "fixtures",
  MOLDS = "molds",
  BLADES = "blades",
  // Auxiliary materials subcategories
  LABOR_PROTECTION = "labor_protection",
  OFFICE_SUPPLIES = "office_supplies",
  PRODUCTION_SUPPLIES = "production_supplies",
  ACCESSORIES = "accessories",
  HARDWARE = "hardware",
  OTHERS = "others",
}

export enum RfqType {
  SHORT_TERM = "short_term",
  LONG_TERM = "long_term",
}

export enum RfqStatus {
  DRAFT = "draft",
  PUBLISHED = "published",
  IN_PROGRESS = "in_progress",
  CLOSED = "closed",
  CANCELLED = "cancelled",
}

export enum QuoteStatus {
  DRAFT = "draft",
  SUBMITTED = "submitted",
  WITHDRAWN = "withdrawn",
  UNDER_REVIEW = "under_review",
  SELECTED = "selected",
  REJECTED = "rejected",
}

// Frontend default evaluation dimensions (backend may extend)
export interface EvaluationCriteria {
  price?: number;
  quality?: number;
  delivery?: number;
  service?: number;
  [key: string]: number | string | undefined;
}

export interface ReviewScores {
  price?: number;
  quality?: number;
  delivery?: number;
  service?: number;
  [key: string]: number | undefined;
}

export interface Rfq {
  id: number;
  title: string;
  description: string | null;
  materialType: RfqMaterialType | string;
  materialCategoryType?: RfqMaterialType | string;
  distributionCategory: RfqDistributionCategory | string | null;
  distributionSubcategory: RfqDistributionSubcategory | string | null;
  rfqType: RfqType | string;
  deliveryPeriod: number;
  budgetAmount: number | null;
  currency: string;
  requiredDocuments: string[] | null;
  evaluationCriteria: EvaluationCriteria | null;
  validUntil: string;
  status: RfqStatus | string;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  // IDM Equipment Standard Form enhancements
  requestingParty?: string | null;
  requestingDepartment?: string | null;
  requirementDate?: string | null;
  detailedParameters?: string | null;
  minSupplierCount?: number;
  supplierExceptionNote?: string | null;
  invitedSuppliers?: RfqInvitedSupplier[];
  quotes?: Quote[];
  invitation?: (SupplierRfqInvitationSummary & { denialReason?: string | null });
  // Quote visibility control
  quotesVisible?: boolean;
  visibilityReason?: {
    totalInvited: number;
    submittedCount: number;
    deadline: string;
    message: string;
  } | null;
  // Multi-item RFQ support
  isMultiItem?: boolean;
  isLineItemMode?: boolean;
  items?: RfqItem[];
  lineItems?: RfqItem[];
  supplierInvitation?: (SupplierRfqInvitationSummary & { denialReason?: string | null }) | null;
}

export interface RfqInvitedSupplier {
  id: number;
  companyId: string | null;
  supplierCode?: string | null;
  vendorCode?: string | null;
  companyName: string;
  stage: string | null;
  invitationStatus?: string | null;
}

export interface QuoteTariffSummary {
  currency: string;
  totalOriginalAmount: number;
  totalOriginalAmountUsd: number | null;
  totalStandardCostLocal: number;
  totalStandardCostUsd: number | null;
  totalTariffAmountLocal: number;
  totalTariffAmountUsd: number | null;
  hasSpecialTariff: boolean;
  standardCostCurrency: string;
}

export interface TariffCalculationResult {
  originalPrice: number;
  originalCurrency: string;
  exchangeRate: number | null;
  originalPriceUsd: number | null;
  tariffRate: number;
  tariffRateMissing: boolean;
  tariffRatePercent: string | null;
  freightRate: number;
  freightRatePercent: string | null;
  specialTariffRate: number;
  specialTariffRatePercent: string | null;
  tariffAmount: number;
  freightAmount: number;
  specialTariffAmount: number;
  tariffAmountUsd: number | null;
  freightAmountUsd: number | null;
  specialTariffAmountUsd: number | null;
  totalTariffAmount: number;
  totalTariffAmountUsd: number | null;
  standardCostLocal: number;
  standardCostUsd: number | null;
  standardCost: number;
  standardCostCurrency: string | null;
  shippingCountry: string | null;
  productGroup: string | null;
  productOrigin: string | null;
  projectLocation: string | null;
  deliveryTerms: string | null;
  isDdp: boolean;
  hasSpecialTariff: boolean;
  warnings: Array<{
    code: string | null;
    message: string | null;
    severity: string | null;
  }> | null;
  error: string | null;
}

export interface QuoteTariffCalculation {
  originalPrice: number;
  originalCurrency: string;
  exchangeRate: number | null;
  originalPriceUsd: number | null;
  tariffRate: number;
  tariffRatePercent: string;
  specialTariffRate: number;
  specialTariffRatePercent: string | null;
  tariffAmount: number;
  specialTariffAmount: number;
  tariffAmountUsd: number | null;
  specialTariffAmountUsd: number | null;
  totalTariffAmount: number;
  totalTariffAmountUsd: number | null;
  standardCostLocal: number;
  standardCostUsd: number | null;
  standardCost: number;
  standardCostCurrency: string;
  shippingCountry: string | null;
  productGroup: string | null;
  productOrigin?: string | null;
  hasSpecialTariff: boolean;
}

export interface Quote {
  id: number;
  rfqId: number;
  supplierId: number;
  companyName?: string;
  supplierName?: string;
  unitPrice: number;
  totalAmount: number;
  currency: string;
  deliveryDate: string;
  paymentTerms: string | null;
  notes: string | null;
  deliveryTerms?: string | null;
  shippingCountry?: string | null;
  shippingLocation?: string | null;
  status: QuoteStatus | string;
  submittedAt: string | null;
  withdrawalReason: string | null;
  withdrawnAt: string | null;
  createdAt: string;
  updatedAt: string;
  // IDM Equipment Standard Form enhancements
  brand?: string | null;
  taxStatus?: "inclusive" | "exclusive" | string;
  parameters?: string | null;
  optionalConfig?: string | null;
  version?: number;
  isLatest?: number | boolean;
  modifiedCount?: number;
  ipAddress?: string | null;
  canModifyUntil?: string | null;
  // Supplier profile fields for risk assessment
  companyId?: string;
  stage?: string;
  category?: string;
  registeredCapital?: number | null;
  registrationDate?: string | null;
  businessRegistrationNumber?: string | null;
  legalRepresentative?: string | null;
  contactPerson?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  address?: string | null;
  region?: string | null;
  paymentCurrency?: string | null;
  taxRegistrationNumber?: string | null;
  bankAccount?: string | null;
  bankName?: string | null;
  isItemized?: boolean;
  quoteItems?: RfqQuoteItem[];
  supplierDocuments?: SupplierDocument[];
  tariffSummary?: QuoteTariffSummary | null;
  standardCostLocalTotal?: number | null;
  standardCostUsdTotal?: number | null;
  attachments?: QuoteAttachment[];
}

export interface QuoteVersion {
  id: number;
  quoteId: number;
  version: number;
  unitPrice: number;
  totalAmount: number;
  brand: string | null;
  taxStatus: string;
  parameters: string | null;
  optionalConfig: string | null;
  deliveryDate: string;
  paymentTerms: string | null;
  notes: string | null;
  modifiedAt: string;
  ipAddress: string | null;
  changeSummary: string | null;
}

export interface ExternalSupplierInvitation {
  id: number;
  rfqId: number;
  email: string;
  companyName: string | null;
  contactPerson: string | null;
  invitationToken: string;
  registrationCompleted: number | boolean;
  registeredSupplierId: number | null;
  invitedAt: string;
  registeredAt: string | null;
  expiresAt: string;
  status: "pending" | "registered" | "expired" | "email_failed" | string;
}

export interface ExternalEmailInvitation {
  email: string;
  companyName?: string;
  contactPerson?: string;
}

export interface RfqReview {
  id: number;
  rfqId: number;
  selectedQuoteId: number | null;
  reviewScores: ReviewScores;
  comments: string | null;
  reviewedBy: string;
  reviewedAt: string;
}

export interface RfqCategoryMetadata {
  equipment: {
    label: string;
    subcategories: {
      standard: string;
      non_standard: string;
    };
  };
  auxiliary_materials: {
    label: string;
    subcategories: {
      labor_protection: string;
      office_supplies: string;
      production_supplies: string;
      accessories: string;
      others: string;
    };
  };
}

// Multi-item RFQ: RFQ Item (閻椻晜鏋＄悰宀勩€嶉惄?
export interface RfqItem {
  id?: number;
  rfqId?: number;
  lineNumber: number;
  materialType: RfqMaterialType | string;
  distributionCategory: RfqDistributionCategory | string;
  distributionSubcategory: RfqDistributionSubcategory | string;
  itemName: string;
  itemCode?: string;
  specifications?: string;
  quantity?: number;
  unit?: string;
  estimatedUnitPrice?: number;
  estimatedTotalAmount?: number;
  currency?: string;
  detailedParameters?: string;
  notes?: string;
  createdAt?: string;
}

// Multi-item RFQ: Quote Item (閹躲儰鐜悰宀勩€嶉惄?
export interface RfqQuoteItem {
  id?: number;
  quoteId?: number;
  rfqItemId?: number;
  rfqLineItemId?: number;
  unitPrice?: number;
  unit_price?: number | string | null;
  quantity?: number;
  totalAmount?: number;
  totalPrice?: number;
  currency?: string;
  brand?: string;
  deliveryDate?: string;
  deliveryPeriod?: number | string | null;
  delivery_period?: number | string | null;
  delivery_time?: number | string | null;
  deliveryTerms?: string | null;
  taxStatus?: "inclusive" | "exclusive";
  parameters?: string;
  notes?: string;
  createdAt?: string;
  updatedAt?: string;
  productGroup?: string | null;
  product_group?: string | null;
  productOrigin?: string | null;
  product_origin?: string | null;
  tariffCalculation?: QuoteTariffCalculation | null;
  standardLineCostLocal?: number | null;
  standardLineCostUsd?: number | null;
  standardUnitCostLocal?: number | null;
  standardUnitCostUsd?: number | null;
  standardUnitCost?: number | string | null;
  standardCostLocal?: number | string | null;
  standardCostUsd?: number | string | null;
  exchangeRate?: number | string | null;
  originalLineTotal?: number | null;
  originalLineTotalUsd?: number | null;
  tariffLineTotal?: number | null;
  tariffLineTotalUsd?: number | null;
  lineQuantity?: number | null;
  minimumOrderQuantity?: number | null;
  minimum_order_quantity?: number | string | null;
  unit?: string | null;
  moq?: number | null;
  standardPackageQuantity?: number | null;
  standard_package_quantity?: number | string | null;
  standardPackQuantity?: number | string | null;
  spq?: number | null;
  freightAmount?: number | string | null;
  shippingFee?: number | string | null;
  shipping_fee?: number | string | null;
  freightRate?: number | string | null;
  freight_rate?: number | string | null;
  specialTariffRate?: number | string | null;
  special_tariff_rate?: number | string | null;
}

export interface QuoteAttachment {
  id: number;
  quoteId: number;
  originalName: string;
  storedName: string;
  fileType?: string | null;
  fileSize?: number | null;
  uploadedAt?: string | null;
  uploadedBy?: number | null;
  downloadUrl?: string | null;
}

export interface CreateRfqPayload {
  title: string;
  description?: string;
  // Single-item mode (backward compatible)
  materialType?: RfqMaterialType | string;
  distributionCategory?: RfqDistributionCategory | string;
  distributionSubcategory?: RfqDistributionSubcategory | string;
  // Multi-item mode
  isMultiItem?: boolean;
  items?: RfqItem[];
  rfqType: RfqType | string;
  deliveryPeriod: number;
  budgetAmount?: number;
  currency?: string;
  requiredDocuments?: string[];
  evaluationCriteria?: EvaluationCriteria;
  validUntil: string;
  supplierIds: number[];
  // IDM Equipment Standard Form enhancements
  requestingParty?: string;
  requestingDepartment?: string;
  requirementDate?: string;
  detailedParameters?: string;
  minSupplierCount?: number;
  supplierExceptionNote?: string;
  projectLocation?: string;
  externalEmails?: ExternalEmailInvitation[];
}

export interface SubmitQuotePayload {
  // Single-item mode (backward compatible)
  unitPrice?: number;
  totalAmount?: number;
  currency?: string;
  deliveryDate?: string;
  paymentTerms?: string;
  notes?: string;
  // IDM Equipment Standard Form enhancements
  brand?: string;
  taxStatus?: "inclusive" | "exclusive";
  parameters?: string;
  optionalConfig?: string;
  // Multi-item mode
  isItemized?: boolean;
  quoteItems?: RfqQuoteItem[];
}

export interface ModifyQuotePayload extends SubmitQuotePayload {
  changeSummary?: string;
}

export interface ReviewRfqPayload {
  selectedQuoteId: number;
  reviewScores: ReviewScores;
  comments?: string;
}

// ============================================================
// Notifications
// ============================================================

export interface NotificationItem {
  id: number;
  userId?: number | null;
  supplierId?: number | null;
  type: string;
  title: string;
  message: string;
  priority: "low" | "normal" | "high" | "urgent" | string;
  status: "unread" | "read" | string;
  relatedEntityType?: string | null;
  relatedEntityId?: number | null;
  createdAt: string;
  readAt?: string | null;
  expiresAt?: string | null;
  metadata: Record<string, unknown>;
}

export interface PaginatedNotificationsResponse {
  data: NotificationItem[];
  pagination: {
    total: number;
    limit: number;
    offset: number;
    hasMore: boolean;
  };
  meta: {
    unreadCount: number;
  };
} // ============================================================
// Price Comparisons (IDM Process Enhancement)
// ============================================================

export enum OnlinePlatform {
  ZHENKUNXING = "zhenkunxing",
  ALIBABA_1688 = "1688",
  JD = "jd",
  TAOBAO = "taobao",
  OTHER = "other",
}

export interface QuotePriceComparison {
  id: number;
  quote_id: number;
  rfq_id: number;
  online_platform: OnlinePlatform | string;
  online_price: number;
  online_currency: string;
  screenshot_file: string | null;
  file_path?: string | null;
  price_variance_percent: number;
  product_url: string | null;
  comparison_notes: string | null;
  compared_by_id: string | null;
  compared_by_name: string | null;
  compared_at: string;
}

export interface CreatePriceComparisonPayload {
  online_platform: OnlinePlatform | string;
  online_price: number;
  online_currency?: string;
  product_url?: string;
  comparison_notes?: string;
  screenshot?: File;
}

// ============================================================
// Quote Export (IDM Process Enhancement)
// ============================================================

export interface QuoteExportRfq {
  id: number;
  rfq_number?: string | number | null;
  title?: string | null;
  material_type?: string | null;
  status?: string | null;
}

export interface QuoteExportQuote {
  id: number;
  supplier_name?: string | null;
  supplier_id?: number | string | null;
  contactPerson?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  address?: string | null;
  bankAccount?: string | null;
  supplierPaymentTerms?: string | null;
  unit_price?: number | string | null;
  total_amount?: number | string | null;
  delivery_time?: string | number | null;
  payment_terms?: string | null;
  currency?: string | null;
  status?: string | null;
  created_at?: string | null;
}

export interface QuoteExportData {
  rfq: QuoteExportRfq;
  quote: QuoteExportQuote;
  priceComparisons: QuotePriceComparison[];
  review: RfqReview | null;
  exportedAt: string;
  exportedBy: string;
}

// ============================================================
// Reconciliation (Supplier Invoice & Warehouse Receipt)
// ============================================================

export enum ReconciliationStatus {
  MATCHED = "matched",
  VARIANCE = "variance",
  UNMATCHED = "unmatched",
  PENDING = "pending",
}

export interface Invoice {
  id: number;
  reconciliation_id: number;
  invoice_number: string;
  invoiceNumber?: string;
  invoice_date: string;
  invoiceDate?: string;
  invoice_amount: number;
  invoiceAmount?: number;
  supplier_id?: number;
  status?: string;
  file_url?: string | null;
  fileUrl?: string | null;
  file_name?: string | null;
  fileName?: string | null;
  created_at: string;
  updated_at: string;
}

export interface Reconciliation {
  id: number;
  warehouseOrderNo: string;
  supplier_id: number;
  rfq_id?: number | null;
  invoice_id?: number | null;
  received_date: string;
  totalAmount: number;
  total_quantity: number;
  status: ReconciliationStatus | string;
  variance_amount: number;
  accountant_confirmed: boolean | number;
  accountant_id?: number | null;
  accountant_notes?: string | null;
  confirmed_at?: string | null;
  created_at: string;
  updated_at: string;
  // Enriched data
  invoice?: Invoice | null;
  warehouseDetails?: WarehouseReceiptDetail | null;
  varianceAnalysis?: ReconciliationVarianceAnalysis | null;
  statusHistory?: ReconciliationStatusHistory[];
  invoiceMatches?: InvoiceReconciliationMatch[];
  matchedAmount?: number;
  matchedQuantity?: number;
  supplierName?: string;
  invoiceNumber?: string;
  invoiceDate?: string;
  invoiceAmount?: number;
}

export interface WarehouseReceiptDetail {
  id: number;
  reconciliation_id: number;
  receipt_number: string;
  receipt_date: string;
  warehouse_location?: string | null;
  received_by?: string | null;
  item_details?: string | null;
  total_items: number;
  external_system_id?: string | null;
  sync_status?: string | null;
  last_sync_at?: string | null;
  sync_error?: string | null;
  reconciliation_number?: string;
  supplierName?: string;
  invoiceNumber?: string;
}

export interface InvoiceReconciliationMatch {
  id: number;
  invoice_id: number;
  reconciliation_id: number;
  matched_amount: number;
  matched_quantity?: number;
  variance_amount: number;
  variance_percentage?: number;
  variance_reason?: string | null;
  matching_status: string;
  requires_review?: boolean | number;
  auto_matched?: boolean | number;
  created_at: string;
  updated_at?: string | null;
  invoiceNumber?: string;
  invoiceDate?: string;
  totalAmount?: number;
}

export interface ReconciliationVarianceAnalysis {
  id: number;
  reconciliation_id: number;
  invoice_id: number;
  variance_amount: number;
  variance_percentage: number;
  variance_reason?: string | null;
  analysis_details?: string | Record<string, unknown> | null;
  auto_generated?: boolean | number;
  created_at: string;
  updated_at: string;
}

export interface ReconciliationStatusHistory {
  id: number;
  reconciliation_id: number;
  status: string;
  changed_by: number;
  changed_by_name?: string | null;
  change_reason?: string | null;
  changed_at: string;
  created_at?: string;
}

export interface ReconciliationThreshold {
  id: number;
  acceptable_variance_percentage: number;
  warning_variance_percentage: number;
  critical_variance_percentage?: number;
  is_active: boolean | number;
  created_at: string;
  updated_at: string;
}

export interface ReconciliationStats {
  total_reconciliations: number;
  matched_count: number;
  variance_count: number;
  unmatched_count: number;
  confirmed_count: number;
  total_variance_amount: number;
  avg_variance_amount: number;
}

export interface ReconciliationDashboardData {
  stats: ReconciliationStats;
  recentReconciliations: Reconciliation[];
  supplierStats: SupplierReconciliationStats[];
  varianceTrends: VarianceTrendData[];
}

export interface SupplierReconciliationStats {
  id: number;
  companyName: string;
  reconciliation_count: number;
  matched_count: number;
  variance_count: number;
  total_reconciliation_amount: number;
  avg_variance: number;
}

export interface VarianceTrendData {
  date: string;
  count: number;
  avg_variance: number;
  positive_variance_count: number;
  negative_variance_count: number;
}

export interface SupplierReconciliationData {
  stats: ReconciliationStats;
  reconciliations: Reconciliation[];
  pendingActions: {
    pending_confirmations: number;
    pending_variances: number;
  };
}

export interface WarehouseReceiptSummary {
  warehouseReceipts: WarehouseReceiptDetail[];
  summary: {
    total_receipts: number;
    total_quantity: number;
    matched_receipts: number;
    variance_receipts: number;
  };
}

export interface ReconciliationReport {
  reportType: "summary" | "variance" | "supplier";
  summary?: ReconciliationStats & {
    monthlyTrends: MonthlyReconciliationTrend[];
    statusBreakdown: StatusBreakdown[];
  };
  varianceDistribution?: VarianceDistribution[];
  topVariances?: (Reconciliation & { absolute_variance: number })[];
  supplierPerformance?: SupplierPerformanceData[];
}

export interface MonthlyReconciliationTrend {
  month: string;
  count: number;
  total_amount: number;
  avg_variance: number;
  matched_count: number;
}

export interface StatusBreakdown {
  status: string;
  count: number;
  total_amount: number;
  avg_variance: number;
}

export interface VarianceDistribution {
  variance_category: string;
  count: number;
  total_variance: number;
  avg_variance: number;
}

export interface SupplierPerformanceData {
  id: number;
  companyName: string;
  total_reconciliations: number;
  matched_count: number;
  variance_count: number;
  total_amount: number;
  avg_variance: number;
  confirmed_count: number;
}

export interface InvoiceUploadPayload {
  reconciliationId: number;
  invoiceAmount: number;
  invoiceDate: string;
  invoiceNumber: string;
  invoiceFile: File;
}

export interface InvoiceMatchPayload {
  invoiceId: number;
  reconciliationId: number;
  matchedAmount: number;
  matchedQuantity?: number;
  varianceReason?: string;
}

export interface ReconciliationConfirmPayload {
  reconciliationId: number;
  confirm: boolean;
  notes?: string;
}
