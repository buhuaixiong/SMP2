export type RegistrationMode = 'draft' | 'final';

export interface SupplierRegistrationPayload {
  companyName: string | null;
  englishName: string | null;
  chineseName: string | null;
  companyType: string | null;
  companyTypeOther: string | null;
  authorizedCapital: string | null;
  issuedCapital: string | null;
  directors: string | null;
  owners: string | null;
  registeredOffice: string | null;
  businessRegistrationNumber: string | null;
  businessAddress: string | null;
  businessPhone: string | null;
  businessFax: string | null;
  contactName: string | null;
  contactEmail: string | null;
  procurementEmail: string | null;
  contactPhone: string | null;
  financeContactName: string | null;
  financeContactEmail: string | null;
  financeContactPhone: string | null;
  businessNature: string | null;
  operatingCurrency: string | null;
  deliveryLocation: string | null;
  shipCode: string | null;
  productOrigin: string | null;
  productTypes: string | null;
  productTypesOther: string | null;
  invoiceType: string | null;
  paymentTermsDays: string | null;
  paymentMethods: string[];
  paymentMethodsOther: string | null;
  bankName: string | null;
  bankAddress: string | null;
  bankAccountNumber: string | null;
  swiftCode: string | null;
  notes: string | null;
  supplierClassification: string | null;
  supplierStage: string | null;
}

export interface ValidationResult {
  valid: boolean;
  errors: Record<string, string>;
  value: SupplierRegistrationPayload;
}

export declare const REQUIRED_FIELDS: readonly string[];
export declare const CLASSIFICATIONS: readonly string[];
export declare const CURRENCIES: readonly string[];
export declare const SHIP_CODES: readonly string[];

export declare function buildNormalizedPayload(payload: unknown): SupplierRegistrationPayload;
export declare function validateRegistration(payload: unknown, options?: { mode?: RegistrationMode }): ValidationResult;
export declare function requiredFieldsForFinal(): string[];
export declare function normalizeCurrency(value: unknown): string | null;
