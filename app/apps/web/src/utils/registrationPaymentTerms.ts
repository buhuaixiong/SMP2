export const normalizeRegistrationPaymentTermsDays = (value: string | null | undefined) =>
  String(value ?? "")
    .trim()
    .toUpperCase();

export const isRegistrationPaymentTermsValid = (
  value: string | null | undefined,
  allowedCodes: ReadonlySet<string>,
) => {
  const normalized = normalizeRegistrationPaymentTermsDays(value);
  if (!normalized) {
    return false;
  }

  if (allowedCodes.has(normalized)) {
    return true;
  }

  const numericValue = Number(normalized);
  return Number.isFinite(numericValue) && numericValue >= 0 && numericValue <= 365;
};
