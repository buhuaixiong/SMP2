import { describe, expect, it } from "vitest";

import {
  isRegistrationPaymentTermsValid,
  normalizeRegistrationPaymentTermsDays,
} from "@/utils/registrationPaymentTerms";

const allowedCodes = new Set(["30", "45", "60", "6A"]);

describe("registration payment terms helpers", () => {
  it("normalizes the submitted value", () => {
    expect(normalizeRegistrationPaymentTermsDays(" 6a ")).toBe("6A");
    expect(normalizeRegistrationPaymentTermsDays(null)).toBe("");
  });

  it("rejects blank payment terms", () => {
    expect(isRegistrationPaymentTermsValid("", allowedCodes)).toBe(false);
    expect(isRegistrationPaymentTermsValid(undefined, allowedCodes)).toBe(false);
  });

  it("accepts configured payment term codes", () => {
    expect(isRegistrationPaymentTermsValid("30", allowedCodes)).toBe(true);
    expect(isRegistrationPaymentTermsValid("6a", allowedCodes)).toBe(true);
  });

  it("accepts numeric values within range", () => {
    expect(isRegistrationPaymentTermsValid("120", allowedCodes)).toBe(true);
  });

  it("rejects invalid values", () => {
    expect(isRegistrationPaymentTermsValid("999", allowedCodes)).toBe(false);
    expect(isRegistrationPaymentTermsValid("bad-code", allowedCodes)).toBe(false);
  });
});
