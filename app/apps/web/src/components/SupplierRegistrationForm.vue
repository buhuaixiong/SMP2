<template>
  <div class="supplier-registration-form">
    <section class="form-section">
      <header class="section-header">
        <h3>{{ t("supplier.form.sections.coreProfile.title") }}</h3>
        <p>{{ t("supplier.form.sections.coreProfile.description") }}</p>
      </header>
      <div class="form-grid">
        <label>
          {{ t("supplier.form.fields.companyName.label") }} *
          <input v-model="form.companyName" type="text" required />
        </label>
        <label>
          {{ t("supplier.form.fields.companyId.label") }} *
          <input v-model="form.companyId" type="text" required />
        </label>
        <fieldset class="radio-group">
          <span>{{ t("supplier.form.fields.stage.label") }} *</span>
          <label v-for="option in stageOptions" :key="option.value" class="radio-item">
            <input v-model="form.stage" type="radio" :value="option.value" />
            {{ t(option.labelKey) }}
          </label>
        </fieldset>
        <label>
          {{ t("supplier.form.fields.englishName.label") }}
          <input v-model="form.englishName" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.chineseName.label") }}
          <input v-model="form.chineseName" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.category.label") }}
          <input
            v-model="form.category"
            type="text"
            :placeholder="t('supplier.form.fields.category.placeholder')"
          />
        </label>
      </div>
      <div class="form-grid">
        <label>
          {{ t("supplier.form.fields.contactPerson.label") }} *
          <input v-model="form.contactPerson" type="text" required />
        </label>
        <label>
          {{ t("supplier.form.fields.contactPhone.label") }} *
          <input v-model="form.contactPhone" type="tel" required />
        </label>
        <label>
          {{ t("supplier.form.fields.contactEmail.label") }} *
          <input v-model="form.contactEmail" type="email" required />
        </label>
        <label>
          {{ t("supplier.form.fields.purchaserEmail.label") }} *
          <input
            v-model="form.purchaserEmail"
            type="email"
            required
            :placeholder="t('supplier.form.fields.purchaserEmail.placeholder')"
          />
          <span class="field-hint">{{ t("supplier.form.fields.purchaserEmail.hint") }}</span>
        </label>
      </div>
    </section>

    <section class="form-section">
      <header class="section-header">
        <h3>{{ t("supplier.form.sections.companyType.title") }}</h3>
        <p>{{ t("supplier.form.sections.companyType.description") }}</p>
      </header>
      <div class="form-grid">
        <fieldset class="radio-group">
          <span>{{ t("supplier.form.fields.companyType.label") }} *</span>
          <label v-for="option in companyTypeOptions" :key="option.value" class="radio-item">
            <input v-model="form.companyType" type="radio" :value="option.value" />
            {{ t(option.labelKey) }}
          </label>
        </fieldset>
        <label v-if="showCompanyTypeOther">
          {{ t("supplier.form.fields.companyTypeOther.label") }}
          <input v-model="form.companyTypeOther" type="text" />
        </label>
        <label v-if="isLimitedCompany">
          {{ t("supplier.form.fields.authorizedCapital.label") }}
          <input v-model="form.authorizedCapital" type="text" />
        </label>
        <label v-if="isLimitedCompany">
          {{ t("supplier.form.fields.issuedCapital.label") }}
          <input v-model="form.issuedCapital" type="text" />
        </label>
        <label v-if="isLimitedCompany" class="full-width">
          {{ t("supplier.form.fields.directors.label") }}
          <textarea
            v-model="form.directors"
            rows="2"
            :placeholder="t('supplier.form.fields.directors.placeholder')"
          ></textarea>
        </label>
        <label v-if="showOwnersField" class="full-width">
          {{ t("supplier.form.fields.owners.label") }}
          <textarea
            v-model="form.owners"
            rows="2"
            :placeholder="t('supplier.form.fields.owners.placeholder')"
          ></textarea>
        </label>
      </div>
    </section>

    <section class="form-section">
      <header class="section-header">
        <h3>{{ t("supplier.form.sections.registration.title") }}</h3>
      </header>
      <div class="form-grid">
        <label class="full-width">
          {{ t("supplier.form.fields.registeredOffice.label") }}
          <input v-model="form.registeredOffice" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.businessRegistrationNumber.label") }}
          <input v-model="form.businessRegistrationNumber" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.businessAddress.label") }}
          <input v-model="form.businessAddress" type="text" @change="syncAddress" />
        </label>
        <label>
          {{ t("supplier.form.fields.mailingAddress.label") }}
          <input v-model="form.address" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.telephone.label") }}
          <input v-model="form.businessPhone" type="tel" />
        </label>
        <label>
          {{ t("supplier.form.fields.fax.label") }}
          <input v-model="form.faxNumber" type="text" />
        </label>
      </div>
    </section>

    <section class="form-section">
      <header class="section-header">
        <h3>{{ t("supplier.form.sections.contacts.title") }}</h3>
      </header>
      <div class="form-grid columns-3">
        <div class="contact-card">
          <h4>{{ t("supplier.form.contacts.sales") }}</h4>
          <label>
            {{ t("supplier.form.contacts.name") }}
            <input v-model="form.salesContactName" type="text" />
          </label>
          <label>
            {{ t("supplier.form.contacts.email") }}
            <input v-model="form.salesContactEmail" type="email" />
          </label>
          <label>
            {{ t("supplier.form.contacts.phone") }}
            <input v-model="form.salesContactPhone" type="tel" />
          </label>
        </div>
        <div class="contact-card">
          <h4>{{ t("supplier.form.contacts.finance") }} *</h4>
          <label>
            {{ t("supplier.form.contacts.name") }} *
            <input v-model="form.financeContactName" type="text" required />
          </label>
          <label>
            {{ t("supplier.form.contacts.email") }}
            <input v-model="form.financeContactEmail" type="email" />
          </label>
          <label>
            {{ t("supplier.form.contacts.phone") }} *
            <input v-model="form.financeContactPhone" type="tel" required />
          </label>
        </div>
        <div class="contact-card">
          <h4>{{ t("supplier.form.contacts.customerService") }}</h4>
          <label>
            {{ t("supplier.form.contacts.name") }}
            <input v-model="form.customerServiceContactName" type="text" />
          </label>
          <label>
            {{ t("supplier.form.contacts.email") }}
            <input v-model="form.customerServiceContactEmail" type="email" />
          </label>
          <label>
            {{ t("supplier.form.contacts.phone") }}
            <input v-model="form.customerServiceContactPhone" type="tel" />
          </label>
        </div>
      </div>
    </section>

    <section class="form-section">
      <header class="section-header">
        <h3>{{ t("supplier.form.sections.business.title") }}</h3>
      </header>
      <div class="form-grid">
        <label>
          {{ t("supplier.form.fields.businessNature.label") }}
          <input v-model="form.businessNature" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.operatingCurrency.label") }}
          <input
            v-model="form.operatingCurrency"
            type="text"
            :placeholder="t('supplier.form.fields.operatingCurrency.placeholder')"
          />
        </label>
        <label>
          {{ t("supplier.form.fields.deliveryLocation.label") }}
          <input v-model="form.deliveryLocation" type="text" required />
        </label>
        <label>
          {{ t("supplier.form.fields.shipCode.label") }}
          <select v-model="form.shipCode" required>
            <option value="">{{ t("common.select") }}</option>
            <option v-for="code in shipCodeOptions" :key="code.value" :value="code.value">
              {{ t(code.labelKey) }}
            </option>
          </select>
        </label>
        <label>
          {{ t("supplier.form.fields.productOrigin.label") }}
          <input v-model="form.productOrigin" type="text" required />
        </label>
        <label class="full-width">
          {{ t("supplier.form.fields.productsForEci.label") }}
          <textarea v-model="form.productsForEci" rows="2"></textarea>
        </label>
        <label>
          {{ t("supplier.form.fields.establishedYear.label") }}
          <input v-model="form.establishedYear" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.employeeCount.label") }}
          <input v-model="form.employeeCount" type="text" />
        </label>
        <label class="full-width">
          {{ t("supplier.form.fields.qualityCertifications.label") }}
          <textarea v-model="form.qualityCertifications" rows="2"></textarea>
        </label>
      </div>
    </section>

    <section class="form-section">
      <header class="section-header">
        <h3>{{ t("supplier.form.sections.payments.title") }}</h3>
      </header>
      <div class="form-grid">
        <label>
          {{ t("supplier.form.fields.invoiceType.label") }}
          <input v-model="form.invoiceType" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.paymentTermsDays.label") }}
          <input v-model="form.paymentTermsDays" type="number" min="0" />
        </label>
        <label>
          {{ t("supplier.form.fields.paymentTermsNotes.label") }}
          <input v-model="form.paymentTerms" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.paymentCurrency.label") }}
          <select v-model="form.paymentCurrency">
            <option value="">{{ t("common.select") }}</option>
            <option
              v-for="currency in currencyOptions"
              :key="currency.value"
              :value="currency.value"
            >
              {{ t(currency.labelKey) }}
            </option>
          </select>
        </label>
      </div>
      <div class="payment-methods">
        <span>{{ t("supplier.form.fields.paymentMethods.label") }}</span>
        <div class="payment-options">
          <label v-for="option in paymentMethodOptions" :key="option.value">
            <input v-model="form.paymentMethods" type="checkbox" :value="option.value" />
            {{ t(option.labelKey) }}
          </label>
        </div>
        <label v-if="form.paymentMethods.includes('other')" class="other-input">
          {{ t("supplier.form.fields.paymentMethodsOther.label") }}
          <input v-model="form.paymentMethodsOther" type="text" />
        </label>
      </div>
    </section>

    <section class="form-section">
      <header class="section-header">
        <h3>{{ t("supplier.form.sections.bank.title") }}</h3>
      </header>
      <div class="form-grid">
        <label>
          {{ t("supplier.form.fields.bankName.label") }}
          <input v-model="form.bankName" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.bankAddress.label") }}
          <input v-model="form.bankAddress" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.bankAccountNumber.label") }}
          <input v-model="form.bankAccount" type="text" />
        </label>
        <label>
          {{ t("supplier.form.fields.swiftCode.label") }}
          <input v-model="form.swiftCode" type="text" />
        </label>
      </div>
    </section>

    <section class="form-section">
      <header class="section-header">
        <h3>{{ t("supplier.form.sections.notes.title") }}</h3>
      </header>
      <textarea
        v-model="form.notes"
        rows="3"
        :placeholder="t('supplier.form.fields.notes.placeholder')"
      ></textarea>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, watch } from "vue";
import { useI18n } from "vue-i18n";
import type { SupplierPayload } from "@/api/suppliers";
import { SupplierStage, SupplierCompanyType } from "@/types";

const { t } = useI18n();

interface SupplierFormModel extends SupplierPayload {
  paymentMethods: string[];
  purchaserEmail?: string;
}

const props = defineProps<{
  modelValue: SupplierFormModel;
  mode?: "create" | "edit";
}>();

const emit = defineEmits<{
  (e: "update:modelValue", value: SupplierFormModel): void;
}>();

const buildDefaultForm = (): SupplierFormModel => ({
  companyName: "",
  companyId: "",
  contactPerson: "",
  contactPhone: "",
  contactEmail: "",
  purchaserEmail: "",
  category: "",
  address: "",
  stage: SupplierStage.TEMPORARY,
  bankAccount: "",
  paymentTerms: "",
  creditRating: "",
  serviceCategory: "",
  region: "",
  importance: "",
  complianceStatus: "",
  complianceNotes: "",
  complianceOwner: "",
  complianceReviewedAt: "",
  financialContact: "",
  paymentCurrency: "",
  englishName: "",
  chineseName: "",
  companyType: SupplierCompanyType.LIMITED,
  companyTypeOther: "",
  authorizedCapital: "",
  issuedCapital: "",
  directors: "",
  owners: "",
  registeredOffice: "",
  businessRegistrationNumber: "",
  businessAddress: "",
  businessPhone: "",
  faxNumber: "",
  salesContactName: "",
  salesContactEmail: "",
  salesContactPhone: "",
  financeContactName: "",
  financeContactEmail: "",
  financeContactPhone: "",
  customerServiceContactName: "",
  customerServiceContactEmail: "",
  customerServiceContactPhone: "",
  businessNature: "",
  operatingCurrency: "",
  deliveryLocation: "",
  shipCode: "",
  productOrigin: "",
  productsForEci: "",
  establishedYear: "",
  employeeCount: "",
  qualityCertifications: "",
  invoiceType: "",
  paymentTermsDays: "",
  paymentMethods: [],
  paymentMethodsOther: "",
  bankName: "",
  bankAddress: "",
  swiftCode: "",
  notes: "",
  tags: [],
  actorId: undefined,
  actorName: undefined,
});

const form = reactive<SupplierFormModel>({
  ...buildDefaultForm(),
  ...props.modelValue,
  paymentMethods: Array.isArray(props.modelValue?.paymentMethods)
    ? [...props.modelValue.paymentMethods]
    : [],
});

if (!form.stage) {
  form.stage = SupplierStage.TEMPORARY;
}

watch(
  () => props.modelValue,
  (value) => {
    if (!value) return;
    const base = buildDefaultForm();
    Object.assign(form, base, value);
    form.paymentMethods = Array.isArray(value.paymentMethods) ? [...value.paymentMethods] : [];
    if (!form.stage) {
      form.stage = SupplierStage.TEMPORARY;
    }
  },
  { deep: true },
);

watch(
  form,
  () => {
    emit("update:modelValue", {
      ...form,
      paymentMethods: [...form.paymentMethods],
    });
  },
  { deep: true },
);

const stageOptions = [
  { value: SupplierStage.TEMPORARY, labelKey: "supplier.form.stage.temporary" },
  { value: SupplierStage.OFFICIAL, labelKey: "supplier.form.stage.official" },
];

const companyTypeOptions = [
  { value: SupplierCompanyType.LIMITED, labelKey: "supplier.form.companyType.limited" },
  {
    value: SupplierCompanyType.SOLE_PROPRIETOR,
    labelKey: "supplier.form.companyType.soleProprietor",
  },
  { value: SupplierCompanyType.PARTNERSHIP, labelKey: "supplier.form.companyType.partnership" },
  { value: SupplierCompanyType.OTHER, labelKey: "supplier.form.companyType.other" },
];

const paymentMethodOptions = [
  { value: "cash", labelKey: "supplier.form.paymentMethod.cash" },
  { value: "cheque", labelKey: "supplier.form.paymentMethod.cheque" },
  { value: "wire", labelKey: "supplier.form.paymentMethod.wire" },
  { value: "other", labelKey: "supplier.form.paymentMethod.other" },
];

const shipCodeOptions = [
  "EXW",
  "FOB",
  "CIF",
  "CFR",
  "DDP",
  "DDU",
  "DAP",
  "DAT",
  "FCA",
  "CPT",
  "CIP",
].map((code) => ({
  value: code,
  labelKey: `supplierRegistration.incoterms.${code}`,
}));

const currencyOptions = [
  { value: "RMB", labelKey: "supplierRegistration.currency.RMB" },
  { value: "USD", labelKey: "supplierRegistration.currency.USD" },
  { value: "KRW", labelKey: "supplierRegistration.currency.KRW" },
  { value: "THB", labelKey: "supplierRegistration.currency.THB" },
  { value: "JPY", labelKey: "supplierRegistration.currency.JPY" },
];

const isLimitedCompany = computed(() => form.companyType === SupplierCompanyType.LIMITED);
const showOwnersField = computed(
  () =>
    form.companyType === SupplierCompanyType.PARTNERSHIP ||
    form.companyType === SupplierCompanyType.SOLE_PROPRIETOR,
);
const showCompanyTypeOther = computed(() => form.companyType === SupplierCompanyType.OTHER);

const syncAddress = () => {
  if (!form.address) {
    form.address = form.businessAddress ?? "";
  }
};
</script>

<style scoped>
.supplier-registration-form {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.form-section {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 8px 20px rgba(33, 41, 72, 0.06);
}

.section-header {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 16px;
}

.section-header h3 {
  margin: 0;
  font-size: 18px;
  color: #1f2937;
}

.section-header p {
  margin: 0;
  color: #6b7280;
  font-size: 13px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
}

.form-grid.columns-3 {
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
}

label,
fieldset {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 13px;
  color: #374151;
}

label input,
label select,
label textarea,
.radio-group input {
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 14px;
  color: #1f2937;
}

label textarea {
  resize: vertical;
}

.full-width {
  grid-column: 1 / -1;
}

.radio-group {
  border: 1px solid #d1d5db;
  border-radius: 8px;
  padding: 12px;
  gap: 8px;
}

.radio-group span {
  font-weight: 600;
}

.radio-item {
  flex-direction: row;
  gap: 8px;
  align-items: center;
  font-size: 13px;
}

.contact-card {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #f9fafb;
}

.contact-card h4 {
  margin: 0;
  font-size: 14px;
  color: #1f2937;
}

.payment-methods {
  margin-top: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.payment-options {
  display: flex;
  flex-wrap: wrap;
  gap: 12px 24px;
}

.payment-options label {
  flex-direction: row;
  align-items: center;
  gap: 8px;
}

.other-input {
  max-width: 320px;
}

textarea {
  min-height: 80px;
}

@media (max-width: 720px) {
  .form-section {
    padding: 16px;
  }

  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
