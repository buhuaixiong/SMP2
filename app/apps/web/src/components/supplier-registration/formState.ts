import type {
  SupplierRegistrationDocumentUpload,
  SupplierRegistrationPayload,
} from "@/api/public";

export type SupplierRegistrationCompanyType = SupplierRegistrationPayload["companyType"];
export type SupplierClassification = SupplierRegistrationPayload["supplierClassification"];
export type SupplierInvoiceType = NonNullable<SupplierRegistrationPayload["invoiceType"]>;
export type RegistrationDocumentUpload = SupplierRegistrationDocumentUpload;

export interface SupplierRegistrationFormState {
  companyName: string;
  englishName: string;
  chineseName: string;
  registeredOffice: string;
  businessRegistrationNumber: string;
  businessAddress: string;
  companyType: SupplierRegistrationCompanyType;
  companyTypeOther: string;
  supplierClassification: SupplierClassification;
  operatingCurrency: string;
  deliveryLocation: string;
  shipCode: string;
  productOrigin: string;
  businessNature: string;
  contactName: string;
  contactEmail: string;
  procurementEmail: string;
  contactPhone: string;
  financeContactName: string;
  financeContactEmail: string;
  financeContactPhone: string;
  invoiceType: SupplierInvoiceType | "";
  paymentTermsDays: string;
  paymentMethods: string[];
  paymentMethodsOther: string;
  bankName: string;
  bankAddress: string;
  bankAccountNumber: string;
  swiftCode: string;
  notes: string;
  businessLicenseFile: RegistrationDocumentUpload | null;
  bankAccountFile: RegistrationDocumentUpload | null;
}

export const createDefaultSupplierRegistrationForm = (): SupplierRegistrationFormState => ({
  companyName: "",
  englishName: "",
  chineseName: "",
  registeredOffice: "",
  businessRegistrationNumber: "",
  businessAddress: "",
  companyType: "limited",
  companyTypeOther: "",
  supplierClassification: "DM",
  operatingCurrency: "RMB",
  deliveryLocation: "",
  shipCode: "",
  productOrigin: "",
  businessNature: "",
  contactName: "",
  contactEmail: "",
  procurementEmail: "",
  contactPhone: "",
  financeContactName: "",
  financeContactEmail: "",
  financeContactPhone: "",
  invoiceType: "general_vat",
  paymentTermsDays: "",
  paymentMethods: [],
  paymentMethodsOther: "",
  bankName: "",
  bankAddress: "",
  bankAccountNumber: "",
  swiftCode: "",
  notes: "",
  businessLicenseFile: null,
  bankAccountFile: null,
});
