/**
 * Shared supplier registration validation helpers consumed by both
 * frontend (Vite/TypeScript) and backend (Node.js/Express).
 *
 * The module stays dependency-free to simplify reuse across build targets.
 */

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/i;
const PHONE_REGEX = /^[+0-9()\-\s]{6,20}$/;

const CLASSIFICATIONS = ['DM', 'IDM'];
const CURRENCIES = ['RMB', 'USD', 'KRW', 'THB', 'JPY'];

const SHIP_CODES = ['EXW', 'FOB', 'CIF', 'CFR', 'DDP', 'DDU', 'DAP', 'DAT', 'FCA', 'CPT', 'CIP'];

const REQUIRED_FIELDS = [
  'companyName',
  'registeredOffice',
  'businessRegistrationNumber',
  'businessAddress',
  'contactName',
  'contactEmail',
  'procurementEmail',
  'contactPhone',
  'operatingCurrency',
  'deliveryLocation',
  'shipCode',
  'productOrigin',
  'bankName',
  'bankAddress',
  'bankAccountNumber',
  'companyType',
  'supplierClassification',
  'financeContactName',
  'financeContactPhone',
];

const CURRENCY_ALIASES = {
  RMB: 'RMB',
  CNY: 'RMB',
  USD: 'USD',
  KRW: 'KRW',
  THB: 'THB',
  JPY: 'JPY',
};

const normalizeString = (value) => {
  if (value == null) return null;
  const text = String(value).trim();
  return text.length > 0 ? text : null;
};

const normalizeEmail = (value) => {
  const email = normalizeString(value);
  return email ? email.toLowerCase() : null;
};

const normalizeCurrency = (value) => {
  const currency = normalizeString(value);
  if (!currency) return null;
  const upper = currency.toUpperCase();
  return CURRENCY_ALIASES[upper] || null;
};

const normalizeShipCode = (value) => {
  const code = normalizeString(value);
  if (!code) return null;
  return code.toUpperCase();
};

const normalizePaymentMethods = (value) => {
  if (Array.isArray(value)) {
    return value
      .map((item) => normalizeString(item))
      .filter((item) => Boolean(item));
  }
  const normalized = normalizeString(value);
  if (!normalized) return [];
  return normalized.split(',').map((item) => item.trim()).filter(Boolean);
};

const normalizePaymentTerms = (value) => {
  const normalized = normalizeString(value);
  if (!normalized) return null;
  return normalized;
};

const buildNormalizedPayload = (payload) => {
  const base = payload && typeof payload === 'object' ? payload : {};
  const normalizedCurrency = normalizeCurrency(base.operatingCurrency);

  const normalizeDocumentUpload = (value) => {
    if (!value || typeof value !== 'object') {
      return null;
    }
    const name = normalizeString(value.name);
    const type = normalizeString(value.type);
    const size = Number.isFinite(Number(value.size)) ? Number(value.size) : null;
    const content = normalizeString(value.content);
    if (!name || !type || !content) {
      return null;
    }
    return {
      name,
      type,
      size: size && size > 0 ? size : null,
      content,
    };
  };

  return {
    companyName: normalizeString(base.companyName),
    englishName: normalizeString(base.englishName),
    chineseName: normalizeString(base.chineseName),
    companyType: normalizeString(base.companyType),
    companyTypeOther: normalizeString(base.companyTypeOther),
    authorizedCapital: normalizeString(base.authorizedCapital),
    issuedCapital: normalizeString(base.issuedCapital),
    directors: normalizeString(base.directors),
    owners: normalizeString(base.owners),
    registeredOffice: normalizeString(base.registeredOffice),
    businessRegistrationNumber: normalizeString(base.businessRegistrationNumber),
    businessAddress: normalizeString(base.businessAddress),
    businessPhone: normalizeString(base.businessPhone),
    businessFax: normalizeString(base.businessFax),
    contactName: normalizeString(base.contactName),
    contactEmail: normalizeEmail(base.contactEmail),
    procurementEmail: normalizeEmail(base.procurementEmail),
    contactPhone: normalizeString(base.contactPhone),
    financeContactName: normalizeString(base.financeContactName),
    financeContactEmail: normalizeEmail(base.financeContactEmail),
    financeContactPhone: normalizeString(base.financeContactPhone),
    businessNature: normalizeString(base.businessNature),
    operatingCurrency: normalizedCurrency,
    deliveryLocation: normalizeString(base.deliveryLocation),
    shipCode: normalizeShipCode(base.shipCode),
    productOrigin: normalizeString(base.productOrigin),
    productTypes: Array.isArray(base.productTypes)
      ? base.productTypes.map((item) => normalizeString(item)).filter(Boolean).join(', ')
      : normalizeString(base.productTypes),
    productTypesOther: normalizeString(base.productTypesOther),
    invoiceType: normalizeString(base.invoiceType),
    paymentTermsDays: normalizePaymentTerms(base.paymentTermsDays),
    paymentMethods: normalizePaymentMethods(base.paymentMethods),
    paymentMethodsOther: normalizeString(base.paymentMethodsOther),
    bankName: normalizeString(base.bankName),
    bankAddress: normalizeString(base.bankAddress),
    bankAccountNumber: normalizeString(base.bankAccountNumber),
    swiftCode: normalizeString(base.swiftCode),
    notes: normalizeString(base.notes),
    supplierClassification: normalizeString(base.supplierClassification)
      ? normalizeString(base.supplierClassification).toUpperCase()
      : null,
    supplierStage: normalizeString(base.supplierStage),
    businessLicenseFile: normalizeDocumentUpload(base.businessLicenseFile),
    bankAccountFile: normalizeDocumentUpload(base.bankAccountFile),
  };
};

const validateRegistration = (payload, options = {}) => {
  const mode = options.mode === 'draft' ? 'draft' : 'final';
  const normalized = buildNormalizedPayload(payload);
  const errors = {};

  if (mode === 'final') {
    for (const field of REQUIRED_FIELDS) {
      if (normalized[field] == null || normalized[field] === '' || (Array.isArray(normalized[field]) && normalized[field].length === 0)) {
        errors[field] = 'REQUIRED';
      }
    }
  }

  if (normalized.contactEmail && !EMAIL_REGEX.test(normalized.contactEmail)) {
    errors.contactEmail = 'INVALID_EMAIL';
  }
  if (normalized.procurementEmail && !EMAIL_REGEX.test(normalized.procurementEmail)) {
    errors.procurementEmail = 'INVALID_EMAIL';
  }
  if (normalized.financeContactEmail && !EMAIL_REGEX.test(normalized.financeContactEmail)) {
    errors.financeContactEmail = 'INVALID_EMAIL';
  }

  if (normalized.contactPhone && !PHONE_REGEX.test(normalized.contactPhone)) {
    errors.contactPhone = 'INVALID_PHONE';
  }
  if (normalized.financeContactPhone && !PHONE_REGEX.test(normalized.financeContactPhone)) {
    errors.financeContactPhone = 'INVALID_PHONE';
  }

  if (normalized.operatingCurrency && !CURRENCIES.includes(normalized.operatingCurrency)) {
    errors.operatingCurrency = 'UNSUPPORTED_CURRENCY';
  } else if (mode === 'final' && !normalized.operatingCurrency) {
    errors.operatingCurrency = 'REQUIRED';
  }

  if (normalized.supplierClassification && !CLASSIFICATIONS.includes(normalized.supplierClassification)) {
    errors.supplierClassification = 'UNSUPPORTED_CLASSIFICATION';
  } else if (mode === 'final' && !normalized.supplierClassification) {
    errors.supplierClassification = 'REQUIRED';
  }

  if (normalized.shipCode && !SHIP_CODES.includes(normalized.shipCode)) {
    errors.shipCode = 'UNSUPPORTED_SHIP_CODE';
  } else if (mode === 'final' && !normalized.shipCode) {
    errors.shipCode = 'REQUIRED';
  }

  if (normalized.paymentTermsDays != null) {
    const num = Number(normalized.paymentTermsDays);
    if (!Number.isFinite(num) || num < 0 || num > 365) {
      errors.paymentTermsDays = 'INVALID_PAYMENT_TERMS';
    }
  } else if (mode === 'final') {
    errors.paymentTermsDays = 'REQUIRED';
  }

  if (mode === 'final') {
    if (!normalized.bankAccountNumber) {
      errors.bankAccountNumber = 'REQUIRED';
    }
    if (!normalized.bankName) {
      errors.bankName = 'REQUIRED';
    }
    if (!normalized.bankAddress) {
      errors.bankAddress = 'REQUIRED';
    }
    if (!normalized.businessLicenseFile) {
      errors.businessLicenseFile = 'REQUIRED';
    }
    if (!normalized.bankAccountFile) {
      errors.bankAccountFile = 'REQUIRED';
    }
  }

  return {
    valid: Object.keys(errors).length === 0,
    errors,
    value: normalized,
  };
};

const requiredFieldsForFinal = () => [...REQUIRED_FIELDS, 'businessLicenseFile', 'bankAccountFile'];

module.exports = {
  REQUIRED_FIELDS,
  CLASSIFICATIONS,
  CURRENCIES,
  SHIP_CODES,
  normalizeCurrency,
  buildNormalizedPayload,
  validateRegistration,
  requiredFieldsForFinal,
};

module.exports.default = module.exports;
