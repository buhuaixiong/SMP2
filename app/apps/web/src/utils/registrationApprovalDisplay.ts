type TranslateFn = (key: string) => string;

type PaymentMethodsInput = string[] | string | null | undefined;

const translateWithFallback = (
  translate: TranslateFn,
  key: string,
  fallback: string,
) => {
  const translated = translate(key);
  return translated === key ? fallback : translated;
};

const invoiceTypeLabels: Record<string, { key: string; fallback: string }> = {
  general_vat: {
    key: "supplierRegistration.invoice.generalVat",
    fallback: "VAT General Invoice",
  },
  special_vat: {
    key: "supplierRegistration.invoice.specialVat",
    fallback: "VAT Special Invoice",
  },
  ordinary: {
    key: "supplierRegistration.invoice.ordinary",
    fallback: "Ordinary Invoice",
  },
};

const paymentMethodLabels: Record<string, { key: string; fallback: string }> = {
  cash: {
    key: "supplierRegistration.paymentMethods.cash",
    fallback: "Cash",
  },
  cheque: {
    key: "supplierRegistration.paymentMethods.cheque",
    fallback: "Cheque / Banker Draft",
  },
  wire: {
    key: "supplierRegistration.paymentMethods.wire",
    fallback: "Wire Transfer",
  },
  other: {
    key: "supplierRegistration.paymentMethods.other",
    fallback: "Other",
  },
};

const normalizePaymentMethods = (paymentMethods: PaymentMethodsInput): string[] => {
  if (Array.isArray(paymentMethods)) {
    return paymentMethods.map((item) => String(item).trim()).filter(Boolean);
  }

  if (typeof paymentMethods !== "string") {
    return [];
  }

  const trimmed = paymentMethods.trim();
  if (!trimmed) {
    return [];
  }

  if (trimmed.startsWith("[")) {
    try {
      const parsed = JSON.parse(trimmed);
      if (Array.isArray(parsed)) {
        return parsed.map((item) => String(item).trim()).filter(Boolean);
      }
    } catch {
    }
  }

  return trimmed
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
};

export const formatApprovalInvoiceType = (
  invoiceType: string | null | undefined,
  translate: TranslateFn,
) => {
  const normalizedInvoiceType = invoiceType?.trim();
  if (!normalizedInvoiceType) {
    return "";
  }

  const labelConfig = invoiceTypeLabels[normalizedInvoiceType];
  if (!labelConfig) {
    return normalizedInvoiceType;
  }

  return translateWithFallback(translate, labelConfig.key, labelConfig.fallback);
};

export const formatApprovalPaymentMethods = (
  paymentMethods: PaymentMethodsInput,
  paymentMethodsOther: string | null | undefined,
  translate: TranslateFn,
) => {
  const normalizedPaymentMethods = normalizePaymentMethods(paymentMethods);
  if (!normalizedPaymentMethods.length) {
    return "";
  }

  return normalizedPaymentMethods
    .map((paymentMethod) => {
      const labelConfig = paymentMethodLabels[paymentMethod];
      const label = labelConfig
        ? translateWithFallback(translate, labelConfig.key, labelConfig.fallback)
        : paymentMethod;

      if (paymentMethod === "other" && paymentMethodsOther?.trim()) {
        return `${label}\uFF08${paymentMethodsOther.trim()}\uFF09`;
      }

      return label;
    })
    .join("\u3001");
};
