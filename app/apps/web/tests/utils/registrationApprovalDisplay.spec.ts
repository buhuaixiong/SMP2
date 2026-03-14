import { describe, expect, it } from "vitest";

import {
  formatApprovalInvoiceType,
  formatApprovalPaymentMethods,
} from "@/utils/registrationApprovalDisplay";

const translations: Record<string, string> = {
  "supplierRegistration.invoice.generalVat": "VAT General Invoice",
  "supplierRegistration.invoice.specialVat": "VAT Special Invoice",
  "supplierRegistration.invoice.ordinary": "Ordinary Invoice",
  "supplierRegistration.paymentMethods.cash": "Cash",
  "supplierRegistration.paymentMethods.cheque": "Cheque / Banker Draft",
  "supplierRegistration.paymentMethods.wire": "Wire Transfer",
  "supplierRegistration.paymentMethods.other": "Other",
};

const translate = (key: string) => translations[key] ?? key;

describe("registration approval display helpers", () => {
  it("formats invoice type codes into translated labels", () => {
    expect(formatApprovalInvoiceType("general_vat", translate)).toBe("VAT General Invoice");
    expect(formatApprovalInvoiceType("ordinary", translate)).toBe("Ordinary Invoice");
  });

  it("falls back to the raw invoice type when code is unknown", () => {
    expect(formatApprovalInvoiceType("custom_invoice", translate)).toBe("custom_invoice");
  });

  it("formats payment method arrays into translated labels", () => {
    expect(formatApprovalPaymentMethods(["wire"], null, translate)).toBe("Wire Transfer");
    expect(formatApprovalPaymentMethods(["cash", "cheque"], null, translate)).toBe(
      "Cash\u3001Cheque / Banker Draft",
    );
  });

  it("formats payment methods when the API returns a JSON string", () => {
    expect(formatApprovalPaymentMethods('["wire"]', null, translate)).toBe("Wire Transfer");
    expect(formatApprovalPaymentMethods('["cash","wire"]', null, translate)).toBe(
      "Cash\u3001Wire Transfer",
    );
  });

  it("formats payment methods when the API returns a plain string", () => {
    expect(formatApprovalPaymentMethods("wire", null, translate)).toBe("Wire Transfer");
  });

  it("includes the other payment method detail when provided", () => {
    expect(formatApprovalPaymentMethods(["other"], "Bank Guarantee", translate)).toBe(
      "Other\uFF08Bank Guarantee\uFF09",
    );
  });

  it("returns an empty string when payment methods are missing", () => {
    expect(formatApprovalPaymentMethods(null, null, translate)).toBe("");
    expect(formatApprovalPaymentMethods([], null, translate)).toBe("");
    expect(formatApprovalPaymentMethods("", null, translate)).toBe("");
  });
});
