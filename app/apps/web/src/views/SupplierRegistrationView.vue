<template>
  <div class="registration-page">
    <PageHeader>
      <template #title>
        <div class="registration-header">
          <h1>{{ t("common.appName") }}</h1>
        </div>
      </template>
    </PageHeader>

    <main class="registration-content">
      <el-alert
        v-if="draftError"
        type="warning"
        :closable="false"
        class="draft-alert"
      >
        {{ draftError }}
      </el-alert>
      <el-alert
        v-if="submitError"
        type="error"
        :closable="true"
        class="submit-alert"
        @close="() => { submitError = null; serverFieldErrors = {}; }"
      >
        {{ submitError }}
      </el-alert>

      <SupplierRegistrationInvitationBanner
        v-if="isFromInvitation"
        :translate-or="translateOr"
      />

      <section v-if="!submission" class="wizard-card">
        <div class="wizard-header">
          <el-steps :active="currentStep" finish-status="success" align-center>
            <el-step
              v-for="step in steps"
              :key="step.key"
              :title="step.title"
              :description="step.description"
            />
          </el-steps>
        </div>

        <div class="wizard-body">
          <SupplierRegistrationStepProfile
            v-if="currentStep === 0"
            :title="steps[0].title"
            :description="steps[0].description"
            :form="form"
            @update:form="applyFormUpdate"
            :translate-or="translateOr"
            :is-category-a="isCategoryA"
            :category-badge-label="categoryBadgeLabel"
            :category-badge-tooltip="categoryBadgeTooltip"
          />
          <SupplierRegistrationStepContacts
            v-else-if="currentStep === 1"
            :title="steps[1].title"
            :description="steps[1].description"
            :form="form"
            :translate-or="translateOr"
            :is-category-a="isCategoryA"
            :category-badge-label="categoryBadgeLabel"
            :category-badge-tooltip="categoryBadgeTooltip"
            :credit-code-error="creditCodeError"
            :email-error="emailError"
            :procurement-email-error="procurementEmailError"
            :validate-credit-code="validateCreditCode"
            :validate-contact-email="validateContactEmail"
          />
          <SupplierRegistrationStepBusiness
            v-else-if="currentStep === 2"
            :title="steps[2].title"
            :description="steps[2].description"
            :form="form"
            :translate-or="translateOr"
          />
          <SupplierRegistrationStepFinance
            v-else-if="currentStep === 3"
            :title="steps[3].title"
            :description="steps[3].description"
            :form="form"
            :translate-or="translateOr"
            :is-category-a="isCategoryA"
            :category-badge-label="categoryBadgeLabel"
            :category-badge-tooltip="categoryBadgeTooltip"
            :payment-method-options="paymentMethodOptions"
            :payment-term-options="paymentTermOptions"
          />
          <SupplierRegistrationStepBanking
            v-else-if="currentStep === 4"
            :title="steps[4].title"
            :description="steps[4].description"
            :form="form"
            :translate-or="translateOr"
            :is-category-a="isCategoryA"
            :category-badge-label="categoryBadgeLabel"
            :category-badge-tooltip="categoryBadgeTooltip"
          />
          <SupplierRegistrationStepDocuments
            v-else-if="currentStep === 5"
            :title="steps[5].title"
            :description="steps[5].description"
            :form="form"
            @update:form="applyFormUpdate"
            :translate-or="translateOr"
            :approval-preview="approvalPreview"
            :preview-loading="previewLoading"
            :preview-error="previewError"
            :format-eta="formatEta"
            :request-preview="handlePreview"
          />
        </div>

        <SupplierRegistrationWizardFooter
          :translate-or="translateOr"
          :is-first-step="isFirstStep"
          :is-last-step="isLastStep"
          :loading="loading"
          :preview-loading="previewLoading"
          :draft-saving="draftSaving"
          :draft-loading="draftLoading"
          :draft-expires-at="draftExpiresAt"
          :format-date-time="formatDateTime"
          :handle-save-draft="handleSaveDraft"
          :go-previous-step="goPreviousStep"
          :go-next-step="goNextStep"
          :handle-preview="handlePreview"
          :handle-submit="handleSubmit"
        />
      </section>

      <SupplierRegistrationSubmissionCard
        v-else
        :translate-or="translateOr"
        :submission="submission"
        :temp-account="tempAccount"
        :temp-account-error="tempAccountError"
        :temp-account-qr-url="tempAccountQrUrl"
        :masked-temp-password="maskedTempPassword"
        :show-temp-password="showTempPassword"
        :format-date-time="formatDateTime"
        :copy-to-clipboard="copyToClipboard"
        :toggle-temp-password="toggleTempPassword"
        :view-status="viewRegistrationStatus"
        :return-to-login="returnToLogin"
      />

      <el-dialog
        v-model="showTrackingAccountDialog"
        class="tracking-account-dialog"
        :title="
          translateOr(
            'supplierRegistration.confirmation.trackingDialogTitle',
            'Registration Successful - Tracking Account Created',
          )
        "
        width="520px"
        :close-on-click-modal="false"
        :close-on-press-escape="false"
        :show-close="false"
        @close="() => closeTrackingAccountDialog()"
      >
        <el-alert
          class="tracking-account-alert"
          type="success"
          :closable="false"
          show-icon
          :title="
            trackingAccount.message ||
            translateOr(
              'supplierRegistration.tempAccount.instructions',
              'Use the credentials below to track your approval progress.',
            )
          "
        />

        <el-form label-width="120px" class="tracking-account-form">
          <el-form-item :label="translateOr('auth.usernameLabel', 'Username')">
            <el-input :model-value="trackingAccount.username" readonly>
              <template #append>
                <el-button
                  link
                  :disabled="!trackingAccount.username"
                  @click="
                    copyTrackingCredential(
                      trackingAccount.username,
                      translateOr('auth.usernameLabel', 'Username'),
                    )
                  "
                >
                  {{ translateOr("common.copy", "Copy") }}
                </el-button>
              </template>
            </el-input>
          </el-form-item>

          <el-form-item :label="translateOr('auth.passwordLabel', 'Password')">
            <el-input :model-value="trackingAccount.password || '••••••'" readonly>
              <template #append>
                <el-button
                  link
                  :disabled="!trackingAccount.password"
                  @click="
                    copyTrackingCredential(
                      trackingAccount.password,
                      translateOr('auth.passwordLabel', 'Password'),
                      { markPassword: true },
                    )
                  "
                >
                  {{ translateOr("common.copy", "Copy") }}
                </el-button>
              </template>
            </el-input>
            <span
              v-if="passwordCopied"
              class="tracking-account-password-note tracking-account-password-note--success"
            >
              {{
                translateOr(
                  "supplierRegistration.tempAccount.copied",
                  "Copied to clipboard.",
                )
              }}
            </span>
            <span v-else-if="!trackingAccount.password" class="tracking-account-password-note">
              {{
                translateOr(
                  "supplierRegistration.confirmation.passwordCleared",
                  "For security, the password is cleared a few minutes after it is shown.",
                )
              }}
            </span>
          </el-form-item>
        </el-form>

        <template #footer>
          <div class="tracking-account-actions">
            <el-button @click="() => closeTrackingAccountDialog()">
              {{ translateOr("common.close", "Close") }}
            </el-button>
            <el-button type="primary" @click="handleViewStatusFromDialog">
              {{
                translateOr(
                  "supplierRegistration.confirmation.viewStatus",
                  "View Application Status",
                )
              }}
            </el-button>
          </div>
        </template>
      </el-dialog>
    </main>
  </div>
</template>

<script setup lang="ts">



import {
  h,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
  computed,
  watch,
  nextTick,
  defineAsyncComponent,
} from "vue";
import { useRoute, useRouter } from "vue-router";
import { ElMessageBox } from "element-plus";
import { useI18n } from "vue-i18n";
import { submitSupplierRegistration, saveSupplierRegistrationDraft, fetchSupplierRegistrationDraft, type SupplierRegistrationPayload, type SupplierRegistrationResponse } from "@/api/public";
import { previewApprovalFlow, issueTempAccount, type ApprovalPreview, type TempAccountResponse } from "@/api/suppliers";
import { validateBlacklist } from "@/api/whitelistBlacklist";
import { useAuthStore } from "@/stores/auth";
import PageHeader from "@/components/layout/PageHeader.vue";
import SupplierRegistrationInvitationBanner from "@/components/supplier-registration/SupplierRegistrationInvitationBanner.vue";
import SupplierRegistrationWizardFooter from "@/components/supplier-registration/SupplierRegistrationWizardFooter.vue";
import { useNotification } from "@/composables";
import { createDefaultSupplierRegistrationForm, type SupplierRegistrationFormState } from "@/components/supplier-registration/formState";
import { formatDateTime, optional, isPlainObject, valuesEqual, diffKeys, translateOr as createTranslateOr, copyToClipboard as utilCopyToClipboard } from "@/utils";
const notification = useNotification();

const SupplierRegistrationStepProfile = defineAsyncComponent(
  () => import("@/components/supplier-registration/SupplierRegistrationStepProfile.vue"),
);
const SupplierRegistrationStepContacts = defineAsyncComponent(
  () => import("@/components/supplier-registration/SupplierRegistrationStepContacts.vue"),
);
const SupplierRegistrationStepBusiness = defineAsyncComponent(
  () => import("@/components/supplier-registration/SupplierRegistrationStepBusiness.vue"),
);
const SupplierRegistrationStepFinance = defineAsyncComponent(
  () => import("@/components/supplier-registration/SupplierRegistrationStepFinance.vue"),
);
const SupplierRegistrationStepBanking = defineAsyncComponent(
  () => import("@/components/supplier-registration/SupplierRegistrationStepBanking.vue"),
);
const SupplierRegistrationStepDocuments = defineAsyncComponent(
  () => import("@/components/supplier-registration/SupplierRegistrationStepDocuments.vue"),
);
const SupplierRegistrationSubmissionCard = defineAsyncComponent(
  () => import("@/components/supplier-registration/SupplierRegistrationSubmissionCard.vue"),
);

type FormState = SupplierRegistrationFormState;
type TrackingAccountState = { username: string; password: string; message: string };

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

const DRAFT_STORAGE_KEY = "supplierRegistrationDraftToken";
const AUTO_SAVE_DELAY = 6000;
const translateOr = (key: string, fallback: string) => createTranslateOr(t, key, fallback);

const steps = [
  { key: "profile", title: translateOr("supplierRegistration.stepTitles.profile", "Company Profile"), description: translateOr("supplierRegistration.stepDescriptions.profile", "Provide basic company identifiers.") },
  { key: "contacts", title: translateOr("supplierRegistration.stepTitles.contacts", "Registration & Contacts"), description: translateOr("supplierRegistration.stepDescriptions.contacts", "Share legal addresses and key contacts.") },
  { key: "business", title: translateOr("supplierRegistration.stepTitles.business", "Business Scope"), description: translateOr("supplierRegistration.stepDescriptions.business", "Outline your operating coverage.") },
  { key: "finance", title: translateOr("supplierRegistration.stepTitles.finance", "Finance & Payment"), description: translateOr("supplierRegistration.stepDescriptions.finance", "Define billing and payment preferences.") },
  { key: "banking", title: translateOr("supplierRegistration.stepTitles.banking", "Bank & Submission"), description: translateOr("supplierRegistration.stepDescriptions.banking", "Confirm bank details and preview approvals.") },
  { key: "documents", title: translateOr("supplierRegistration.stepTitles.documents", "Supporting Documents"), description: translateOr("supplierRegistration.stepDescriptions.documents", "Upload business license and bank account proof.") },
];

const currentStep = ref(0);
const missingFields = ref<Set<keyof FormState>>(new Set());
const isFirstStep = computed(() => currentStep.value === 0);
const isLastStep = computed(() => currentStep.value === steps.length - 1);

const form = reactive<FormState>(createDefaultSupplierRegistrationForm());
const applyFormUpdate = (nextForm: FormState) => {
  Object.assign(form, nextForm);
};

const loading = ref(false);
const previewLoading = ref(false);
const previewError = ref<string | null>(null);
const approvalPreview = ref<ApprovalPreview | null>(null);
const submission = ref<SupplierRegistrationResponse | null>(null);

const draftSaving = ref(false);
const draftLoading = ref(false);
const draftToken = ref<string | null>(null);
const draftExpiresAt = ref<string | null>(null);
const draftError = ref<string | null>(null);
const submitError = ref<string | null>(null);
const serverFieldErrors = ref<Record<string, string>>({});

const changedFields = ref<string[]>([]);
const suppressChangeTracking = ref(false);
let autoSaveTimer: number | null = null;

const creditCodeError = ref("");
const emailError = ref("");
const validating = ref(false);
const procurementEmailError = computed(() => serverFieldErrors.value.procurementEmail || "");

const invitationToken = ref("");
const rfqId = ref("");
const isFromInvitation = ref(false);

const tempAccount = ref<TempAccountResponse | null>(null);
const tempAccountError = ref<string | null>(null);
const showTempPassword = ref(false);
const showTrackingAccountDialog = ref(false);
const trackingAccount = reactive<TrackingAccountState>({
  username: "",
  password: "",
  message: "",
});
const passwordCopied = ref(false);
const isMounted = ref(false);
const thirdPartyBaseUrl =
  (import.meta as { env?: Record<string, string | undefined> })?.env?.VITE_THIRD_PARTY_URL || "";
const normalizedThirdPartyBaseUrl = thirdPartyBaseUrl.replace(/\/+$/, "");
const TRACKING_PASSWORD_TIMEOUT_MS = 5 * 60 * 1000;
let trackingPasswordTimer: ReturnType<typeof setTimeout> | null = null;
let passwordCopiedResetTimer: ReturnType<typeof setTimeout> | null = null;

const maskedTempPassword = computed(() => {
  if (!tempAccount.value) {
    return "";
  }
  const password = tempAccount.value.password ?? "";
  return showTempPassword.value ? password : password.replace(/./g, "?");
});

const tempAccountQrUrl = computed(() => {
  if (!tempAccount.value || !normalizedThirdPartyBaseUrl) {
    return "";
  }
  return `${normalizedThirdPartyBaseUrl}/qr?text=${encodeURIComponent(
    `user=${tempAccount.value.username}&pass=${tempAccount.value.password}`,
  )}`;
});

const clearTrackingPasswordTimer = () => {
  if (trackingPasswordTimer) {
    clearTimeout(trackingPasswordTimer);
    trackingPasswordTimer = null;
  }
};

const clearPasswordCopiedResetTimer = () => {
  if (passwordCopiedResetTimer) {
    clearTimeout(passwordCopiedResetTimer);
    passwordCopiedResetTimer = null;
  }
};

const ensureDomReady = async () => {
  if (!isMounted.value || typeof document === "undefined") {
    return false;
  }
  await nextTick();
  return true;
};

const scheduleTrackingPasswordClear = () => {
  if (typeof window === "undefined") {
    return;
  }
  clearTrackingPasswordTimer();
  trackingPasswordTimer = setTimeout(() => {
    trackingAccount.password = "";
  }, TRACKING_PASSWORD_TIMEOUT_MS);
};

const schedulePasswordCopiedReset = () => {
  clearPasswordCopiedResetTimer();
  passwordCopiedResetTimer = setTimeout(() => {
    passwordCopied.value = false;
    passwordCopiedResetTimer = null;
  }, 3000);
};

const openTrackingAccountDialog = (response: SupplierRegistrationResponse) => {
  if (!response.trackingUsername || !response.trackingPassword) {
    return;
  }
  trackingAccount.username = response.trackingUsername;
  trackingAccount.password = response.trackingPassword;
  trackingAccount.message =
    response.trackingMessage ||
    response.message ||
    translateOr(
      "supplierRegistration.tempAccount.instructions",
      "Use the credentials below to track your approval progress.",
    );
  passwordCopied.value = false;
  showTrackingAccountDialog.value = true;
  scheduleTrackingPasswordClear();
};

type CloseTrackingAccountDialogOptions = {
  redirect?: boolean;
};

const closeTrackingAccountDialog = (
  options?: CloseTrackingAccountDialogOptions,
) => {
  showTrackingAccountDialog.value = false;
  trackingAccount.username = "";
  trackingAccount.password = "";
  trackingAccount.message = "";
  passwordCopied.value = false;
  clearPasswordCopiedResetTimer();
  clearTrackingPasswordTimer();
  if (options?.redirect !== false) {
    router.push("/login");
  }
};

const categoryAFieldSet = new Set<keyof FormState>([
  "companyName",
  "englishName",
  "chineseName",
  "bankName",
  "bankAddress",
  "bankAccountNumber",
  "swiftCode",
  "financeContactName",
  "financeContactEmail",
  "financeContactPhone",
  "invoiceType",
  "paymentTermsDays",
  "paymentMethods",
]);

const categoryBadgeLabel = computed(() =>
  translateOr("supplierRegistration.categoryA.label", "Category A"),
);
const categoryBadgeTooltip = computed(() =>
  translateOr(
    "supplierRegistration.categoryA.tooltip",
    "Editing after registration triggers full approval.",
  ),
);
const isCategoryA = (field: keyof FormState) => categoryAFieldSet.has(field);

const paymentTermCodeOptions = [
  { value: "CA", label: "CA - Cash in Advance" },
  { value: "PO", label: "PO - TERM BY PO" },
  { value: "00", label: "00 - COD" },
  { value: "04", label: "04 - 1% 10 Net 30" },
  { value: "05", label: "05 - 1% 15 Net 30" },
  { value: "07", label: "07 - 7 days net" },
  { value: "1A", label: "1A - 120 days AMS" },
  { value: "1B", label: "1B - 150 days AMS" },
  { value: "1C", label: "1C - 180 days AMS" },
  { value: "1D", label: "1D - 210 days AMS" },
  { value: "1E", label: "1E - 240 days AMS" },
  { value: "10", label: "10 - 10 days net" },
  { value: "12", label: "12 - 120 days net" },
  { value: "14", label: "14 - 14 days net" },
  { value: "15", label: "15 - 150 days net" },
  { value: "16", label: "16 - 105 days net" },
  { value: "18", label: "18 - 180 days net" },
  { value: "21", label: "21 - 21 days net" },
  { value: "3A", label: "3A - 30 days AMS" },
  { value: "3B", label: "3B - 30 Days 10th Dy" },
  { value: "30", label: "30 - 30 days net" },
  { value: "32", label: "32 - Net 15 Days" },
  { value: "33", label: "33 - Net 135 days" },
  { value: "35", label: "35 - 35 days net" },
  { value: "4A", label: "4A - 45 days AMS" },
  { value: "40", label: "40 - NET 40 DAYS" },
  { value: "45", label: "45 - 45 days net" },
  { value: "5A", label: "5A - 5 days net" },
  { value: "52", label: "52 - Net 115 Days" },
  { value: "55", label: "55 - 55 days net" },
  { value: "6A", label: "6A - 60 days AMS" },
  { value: "60", label: "60 - 60 days net" },
  { value: "65", label: "65 - 65 days net" },
  { value: "68", label: "68 - Net 195 Days" },
  { value: "7A", label: "7A - 75 days AMS" },
  { value: "70", label: "70 - 70 days net" },
  { value: "75", label: "75 - 75 DAYS NET" },
  { value: "76", label: "76 - 1.5% Net 75" },
  { value: "9A", label: "9A - 90 days AMS" },
  { value: "90", label: "90 - 90 days net" },
];
const paymentTermCodes = new Set(paymentTermCodeOptions.map((option) => option.value));

const paymentTermOptions = computed(() => paymentTermCodeOptions);

const paymentMethodOptions = computed(
  () =>
    [
      {
        value: "cash",
        label: translateOr("supplierRegistration.paymentMethods.cash", "Cash"),
      },
      {
        value: "cheque",
        label: translateOr(
          "supplierRegistration.paymentMethods.cheque",
          "Cheque / Banker Draft",
        ),
      },
      {
        value: "wire",
        label: translateOr("supplierRegistration.paymentMethods.wire", "Wire Transfer"),
      },
      {
        value: "other",
        label: translateOr("supplierRegistration.paymentMethods.other", "Other"),
      },
    ] as Array<{ value: string; label: string }>,
);

const requiredFieldsByStep: Array<Array<keyof FormState>> = [
  ["companyName", "companyType", "supplierClassification", "operatingCurrency"],
  [
    "registeredOffice",
    "businessAddress",
    "businessRegistrationNumber",
    "contactName",
    "contactEmail",
    "contactPhone",
    "procurementEmail",
    "financeContactName",
    "financeContactPhone",
  ],
  ["deliveryLocation", "shipCode", "productOrigin"],
  ["invoiceType", "paymentMethods"],
  ["bankName", "bankAddress", "bankAccountNumber"],
  ["businessLicenseFile", "bankAccountFile"],
];

const fieldLabelMap = computed<Record<string, string>>(() => ({
  companyName: t("supplier.fields.companyName"),
  companyType: translateOr("supplierRegistration.fields.companyType", "Company Type"),
  companyTypeOther: translateOr("supplierRegistration.fields.companyTypeOther", "Other Company Type"),
  supplierClassification: translateOr("supplierRegistration.fields.supplierClassification", "Supplier Classification"),
  operatingCurrency: translateOr("supplierRegistration.fields.operatingCurrency", "Operating Currency"),
  registeredOffice: t("supplier.fields.address"),
  businessAddress: translateOr("supplierRegistration.fields.businessAddress", "Business Address"),
  businessRegistrationNumber: t("supplier.fields.businessRegistrationNumber"),
  contactName: t("supplier.fields.contactPerson"),
  contactEmail: t("supplier.fields.contactEmail"),
  procurementEmail: translateOr("supplierRegistration.fields.procurementEmail", "Procurement Contact Email"),
  contactPhone: t("supplier.fields.contactPhone"),
  financeContactName: translateOr("supplierRegistration.fields.financeContact", "Finance Contact"),
  financeContactEmail: translateOr("supplierRegistration.fields.financeContactEmail", "Finance Contact Email"),
  financeContactPhone: translateOr("supplierRegistration.fields.financeContactPhone", "Finance Contact Phone"),
  deliveryLocation: translateOr("supplierRegistration.fields.deliveryLocation", "Delivery Location"),
  shipCode: translateOr("supplierRegistration.fields.shipCode", "Incoterms"),
  productOrigin: translateOr("supplierRegistration.fields.productOrigin", "Product Origin"),
  invoiceType: translateOr("supplierRegistration.fields.invoiceType", "Invoice Type"),
  paymentMethods: translateOr("supplierRegistration.fields.paymentMethods", "Payment Methods"),
  paymentMethodsOther: translateOr("supplierRegistration.fields.paymentMethodsOther", "Other payment method"),
  paymentTermsDays: translateOr("supplierRegistration.fields.paymentTermsDays", "Payment Terms (days)"),
  bankName: translateOr("supplierRegistration.fields.bankName", "Bank Name"),
  bankAddress: translateOr("supplierRegistration.fields.bankAddress", "Bank Address"),
  bankAccountNumber: translateOr("supplierRegistration.fields.bankAccountNumber", "Bank Account Number"),
  businessLicenseFile: translateOr("supplierRegistration.documents.businessLicense.title", "Business License"),
  bankAccountFile: translateOr("supplierRegistration.documents.bankCertificate.title", "Bank Account Certificate"),
}));

const formatFieldLabel = (field: string) => fieldLabelMap.value[field] || field;

const stepIndexByField = requiredFieldsByStep.reduce((acc, fields, index) => {
  fields.forEach((field) => {
    acc[field] = index;
  });
  return acc;
}, {} as Record<keyof FormState, number>);

const stepIndexOverrides: Partial<Record<keyof FormState, number>> = {
  companyTypeOther: 0,
  financeContactEmail: 1,
  paymentTermsDays: 3,
  paymentMethodsOther: 3,
  businessLicenseFile: 5,
  bankAccountFile: 5,
};

const resolveStepIndex = (field: string) => {
  const typedField = field as keyof FormState;
  if (Object.prototype.hasOwnProperty.call(stepIndexOverrides, typedField)) {
    return stepIndexOverrides[typedField] ?? null;
  }
  if (Object.prototype.hasOwnProperty.call(stepIndexByField, typedField)) {
    return stepIndexByField[typedField];
  }
  return null;
};

const isFormField = (field: string): field is keyof FormState =>
  Object.prototype.hasOwnProperty.call(form, field);

const formatErrorCode = (field: string, code: string) => {
  switch (code) {
    case "REQUIRED":
      return translateOr("validation.required", "This field is required");
    case "INVALID_EMAIL":
      return translateOr("validation.email", "Please enter a valid email address");
    case "INVALID_PHONE":
      return translateOr("validation.phone", "Please enter a valid phone number");
    case "UNSUPPORTED_CURRENCY":
      return translateOr("supplierRegistration.validation.unsupportedCurrency", "Unsupported operating currency");
    case "UNSUPPORTED_CLASSIFICATION":
      return translateOr("supplierRegistration.validation.unsupportedClassification", "Unsupported supplier classification");
    case "UNSUPPORTED_SHIP_CODE":
      return translateOr("supplierRegistration.validation.unsupportedShipCode", "Unsupported delivery terms");
    case "INVALID_PAYMENT_TERMS":
      return translateOr(
        "supplierRegistration.validation.paymentTerms",
        "Please select a valid payment term.",
      );
    case "NOT_FOUND":
      if (field === "procurementEmail") {
        return translateOr(
          "supplierRegistration.validation.procurementEmailNotFound",
          "Procurement email not found",
        );
      }
      return translateOr("supplierRegistration.validation.notFound", "Not found");
    default:
      return code || translateOr("errors.general", "Unexpected error occurred.");
  }
};

const buildErrorSummary = (fieldErrors: Record<string, string>) => {
  const entries = Object.entries(fieldErrors);
  if (!entries.length) {
    return "";
  }
  const sorted = entries.sort((a, b) => {
    const stepA = resolveStepIndex(a[0]);
    const stepB = resolveStepIndex(b[0]);
    const safeA = stepA === null ? Number.MAX_SAFE_INTEGER : stepA;
    const safeB = stepB === null ? Number.MAX_SAFE_INTEGER : stepB;
    return safeA - safeB;
  });
  return sorted.map(([field, message]) => `${formatFieldLabel(field)}: ${message}`).join("; ");
};

const applyServerErrors = (message: string, fieldErrors: Record<string, string>) => {
  submitError.value = message;
  serverFieldErrors.value = fieldErrors;

  const formFields = Object.keys(fieldErrors).filter(isFormField);
  if (formFields.length) {
    missingFields.value = new Set(formFields as Array<keyof FormState>);
    const stepCandidates = formFields
      .map(resolveStepIndex)
      .filter((value): value is number => value !== null);
    if (stepCandidates.length) {
      currentStep.value = Math.min(...stepCandidates);
    }
  }
};

const formatBlacklistMessage = (template: string, reason: string) =>
  reason ? template.replace("{reason}", reason) : template;

const resolveDocumentField = (message: string) => {
  const normalized = message.toLowerCase();
  if (normalized.includes("business-license")) {
    return "businessLicenseFile";
  }
  if (normalized.includes("bank-account")) {
    return "bankAccountFile";
  }
  return null;
};

const extractErrorPayload = (error: unknown): Record<string, unknown> => {
  if (isPlainObject(error)) {
    const response = (error as { response?: { data?: unknown } }).response;
    if (response && isPlainObject(response.data)) {
      return response.data as Record<string, unknown>;
    }
    if (typeof (error as { message?: unknown }).message === "string") {
      return { message: (error as { message: string }).message };
    }
  }
  if (error instanceof Error) {
    return { message: error.message };
  }
  return {};
};

const parseSubmissionError = (error: unknown) => {
  const payload = extractErrorPayload(error);
  const fieldErrors: Record<string, string> = {};
  const rawMessage = typeof payload.message === "string" ? payload.message : "";
  const rawError = typeof payload.error === "string" ? payload.error : "";
  const rawReason = typeof payload.reason === "string" ? payload.reason : "";
  const rawField = typeof payload.field === "string" ? payload.field : "";
  const blacklistType = typeof payload.blacklist_type === "string" ? payload.blacklist_type : "";
  const errors = isPlainObject(payload.errors) ? (payload.errors as Record<string, unknown>) : null;

  if (rawMessage === "VALIDATION_FAILED" && errors) {
    Object.entries(errors).forEach(([field, code]) => {
      const normalizedCode = typeof code === "string" ? code : String(code ?? "");
      fieldErrors[field] = formatErrorCode(field, normalizedCode);
    });
  }

  if (rawMessage === "DUPLICATE_REGISTRATION" && rawField) {
    fieldErrors[rawField] =
      rawError || translateOr("supplierRegistration.errors.duplicate", "Duplicate registration");
  }

  if (blacklistType) {
    const isCreditCode = blacklistType === "credit_code";
    const blacklistTemplate = isCreditCode
      ? translateOr(
          "supplierRegistration.validation.blacklistCreditCodeReason",
          "This business registration number has been blacklisted: {reason}",
        )
      : translateOr(
          "supplierRegistration.validation.blacklistEmailReason",
          "This email address has been blacklisted: {reason}",
        );
    const blacklistMessage = formatBlacklistMessage(blacklistTemplate, rawReason);
    if (isCreditCode) {
      creditCodeError.value = blacklistMessage;
      fieldErrors.businessRegistrationNumber = blacklistMessage;
    } else {
      emailError.value = blacklistMessage;
      fieldErrors.contactEmail = blacklistMessage;
    }
  }

  if (rawMessage) {
    const docField = resolveDocumentField(rawMessage);
    if (docField) {
      if (rawMessage.toLowerCase().includes("document_exceeds_limit")) {
        fieldErrors[docField] = translateOr(
          "supplierRegistration.documents.errors.fileTooLarge",
          "File size exceeds 10MB limit.",
        );
      } else if (rawReason == "UNSUPPORTED_FILE_TYPE") {
        fieldErrors[docField] = translateOr(
          "supplierRegistration.documents.errors.unsupportedType",
          "Unsupported file type. Please upload a PDF or image.",
        );
      } else if (rawReason == "MIME_TYPE_MISMATCH") {
        fieldErrors[docField] = translateOr(
          "supplierRegistration.documents.errors.mimeMismatch",
          "File type does not match its content. Please upload the original file.",
        );
      } else if (!fieldErrors[docField]) {
        fieldErrors[docField] = translateOr(
          "supplierRegistration.documents.errors.invalidDocument",
          "Invalid document. Please upload again.",
        );
      }
    }
  }

  if (!Object.keys(fieldErrors).length && rawMessage) {
    const lowered = rawMessage.toLowerCase();
    if (lowered.includes("supplierclassification")) {
      fieldErrors.supplierClassification = translateOr(
        "supplierRegistration.validation.unsupportedClassification",
        "Unsupported supplier classification",
      );
    }
    if (lowered.includes("operatingcurrency")) {
      fieldErrors.operatingCurrency = translateOr(
        "supplierRegistration.validation.unsupportedCurrency",
        "Unsupported operating currency",
      );
    }
  }

  const summary = buildErrorSummary(fieldErrors);
  const message = summary
    ? `${translateOr(
        "supplierRegistration.errors.submitFailed",
        "Failed to submit supplier registration",
      )}: ${summary}`
    : rawError || rawMessage || translateOr("errors.general", "Unexpected error occurred.");

  return { message, fieldErrors };
};

const goNextStep = () => {
  if (!validateStep(currentStep.value)) {
    return;
  }
  if (!isLastStep.value) {
    currentStep.value += 1;
  }
};

const goPreviousStep = () => { if (currentStep.value > 0) currentStep.value -= 1; };
const getStepKey = (index: number) => steps[index]?.key ?? null;

const buildDraftPayload = () => JSON.parse(JSON.stringify({ ...form, paymentMethods: [...form.paymentMethods] }));

const cancelAutoSave = () => { if (autoSaveTimer) { clearTimeout(autoSaveTimer); autoSaveTimer = null; } };

const scheduleAutoSave = () => {
  if (loading.value || draftSaving.value || draftLoading.value || !changedFields.value.length) return;
  cancelAutoSave();
  if (typeof window !== "undefined") {
    autoSaveTimer = window.setTimeout(() => { autoSaveTimer = null; void handleSaveDraft({ silent: true }); }, AUTO_SAVE_DELAY);
  }
};

const applyFormValues = (values: Partial<FormState>) => {
  const defaults = createDefaultSupplierRegistrationForm();
  suppressChangeTracking.value = true;
  const formState = form as Record<string, unknown>;
  const incomingValues = values as Record<string, unknown>;

  Object.entries(defaults).forEach(([key, defaultValue]) => {
    const incoming = incomingValues[key];
    if (Array.isArray(defaultValue)) {
      const nextValue = Array.isArray(incoming) ? [...incoming] : [...defaultValue];
      formState[key] = nextValue;
    } else if (typeof defaultValue === "string") {
      const nextValue = incoming == null ? "" : String(incoming);
      formState[key] =
        key === "paymentTermsDays" ? nextValue.trim().toUpperCase() : nextValue;
    } else {
      formState[key] = incoming ?? defaultValue;
    }
  });

  approvalPreview.value = null;
  previewError.value = null;
  changedFields.value = [];
  draftError.value = null;
  tempAccount.value = null;
  tempAccountError.value = null;
  showTempPassword.value = false;
  cancelAutoSave();
  void nextTick(() => { suppressChangeTracking.value = false; });
};

const isFieldFilled = (field: keyof FormState) => {
  const value = form[field];
  if (Array.isArray(value)) {
    return value.length > 0;
  }
  if (value && typeof value === "object") {
    if ("content" in (value as Record<string, unknown>)) {
      const content = (value as { content?: unknown }).content;
      return typeof content === "string" && content.trim().length > 0;
    }
    return Object.keys(value as Record<string, unknown>).length > 0;
  }
  return String(value ?? "").trim().length > 0;
};

const validateStep = (stepIndex: number) => {
  const requiredFields = requiredFieldsByStep[stepIndex] || [];
  const missing = requiredFields.filter((field) => !isFieldFilled(field));
  if (missing.length) {
    missingFields.value = new Set(missing);
    notification.warning(
      translateOr(
        "supplierRegistration.validation.completeStep",
        "Please complete all required fields for this step before continuing.",
      ),
    );
    return false;
  }
  missingFields.value = new Set();

  if (stepIndex === 0 && form.companyType === "other" && !form.companyTypeOther.trim()) {
    notification.warning(
      translateOr(
        "supplierRegistration.validation.specifyCompanyType",
        "Please specify the company type.",
      ),
    );
    return false;
  }

  if (stepIndex === 1 && (creditCodeError.value || emailError.value)) {
    notification.error(
      translateOr(
        "supplierRegistration.validation.resolveBlacklist",
        "Please resolve the highlighted blacklist warnings before continuing.",
      ),
    );
    return false;
  }

  if (
    stepIndex === 3 &&
    form.paymentMethods.includes("other") &&
    !form.paymentMethodsOther.trim()
  ) {
    notification.warning(
      translateOr(
        "supplierRegistration.validation.describeOtherPayment",
        "Please describe the additional payment method.",
      ),
    );
    return false;
  }

  return true;
};

const normalizePaymentTermsDays = () =>
  String(form.paymentTermsDays ?? "")
    .trim()
    .toUpperCase();

const paymentTermsInRange = () => {
  const normalized = normalizePaymentTermsDays();
  if (!normalized) {
    return true;
  }
  if (paymentTermCodes.has(normalized)) {
    return true;
  }
  const value = Number(normalized);
  return Number.isFinite(value) && value >= 0 && value <= 365;
};

const buildPayload = (): SupplierRegistrationPayload => ({
  companyName: form.companyName.trim(),
  englishName: optional(form.englishName),
  chineseName: optional(form.chineseName),
  companyType: form.companyType,
  companyTypeOther: optional(form.companyTypeOther),
  supplierClassification: form.supplierClassification,
  authorizedCapital: undefined,
  issuedCapital: undefined,
  directors: undefined,
  owners: undefined,
  registeredOffice: form.registeredOffice.trim(),
  businessRegistrationNumber: form.businessRegistrationNumber.trim(),
  businessAddress: form.businessAddress.trim(),
  businessPhone: undefined,
  businessFax: undefined,
  contactName: form.contactName.trim(),
  contactEmail: form.contactEmail.trim(),
  procurementEmail: form.procurementEmail.trim(),
  contactPhone: form.contactPhone.trim(),
  financeContactName: form.financeContactName.trim(),
  financeContactEmail: optional(form.financeContactEmail),
  financeContactPhone: form.financeContactPhone.trim(),
  businessNature: optional(form.businessNature),
  operatingCurrency: form.operatingCurrency,
  deliveryLocation: form.deliveryLocation.trim(),
  shipCode: form.shipCode.trim(),
  productOrigin: form.productOrigin.trim(),
  productTypes: undefined,
  invoiceType: form.invoiceType || undefined,
  paymentTermsDays: normalizePaymentTermsDays() || undefined,
  paymentMethods: [...form.paymentMethods],
  paymentMethodsOther: optional(form.paymentMethodsOther),
  bankName: form.bankName.trim(),
  bankAddress: form.bankAddress.trim(),
  bankAccountNumber: form.bankAccountNumber.trim(),
  swiftCode: optional(form.swiftCode),
  notes: optional(form.notes),
  businessLicenseFile: form.businessLicenseFile ?? undefined,
  bankAccountFile: form.bankAccountFile ?? undefined,
});

const formatEta = (iso: string) => {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return date.toLocaleString();
};

const showRegistrationSuccessDialog = async (response: SupplierRegistrationResponse) => {
  const details: Array<{ label: string; value: string }> = [];

  if (response.applicationId) {
    details.push({
      label: translateOr("supplierRegistration.confirmation.applicationId", "Application ID"),
      value: String(response.applicationId),
    });
  }
  if (response.supplierCode) {
    details.push({
      label: translateOr("supplierRegistration.confirmation.supplierId", "Supplier ID"),
      value: response.supplierCode,
    });
  }
  if (response.trackingUrl) {
    const origin = typeof window !== "undefined" ? window.location.origin : "";
    const fullUrl = origin ? `${origin}${response.trackingUrl}` : response.trackingUrl;
    details.push({
      label: translateOr(
        "supplierRegistration.confirmation.viewStatus",
        "View Application Status"
      ),
      value: fullUrl,
    });
  }

  const messageVNode = h("div", { class: "registration-success-dialog__body" }, [
    h(
      "p",
      { class: "registration-success-dialog__intro" },
      translateOr(
        "supplierRegistration.confirmation.successMessage",
        "Thank you for submitting your supplier registration."
      )
    ),
    details.length
      ? h(
          "ul",
          { class: "registration-success-dialog__list" },
          details.map((item) =>
            h("li", { class: "registration-success-dialog__list-item" }, [
              h("span", { class: "registration-success-dialog__list-label" }, `${item.label}`),
              h("span", { class: "registration-success-dialog__list-value" }, item.value),
            ])
          )
        )
      : null,
    h(
      "p",
      { class: "registration-success-dialog__note" },
      translateOr(
        "supplierRegistration.confirmation.pendingMessage",
        "Your application is under review. We will notify you by email."
      )
    ),
  ]);

  await ElMessageBox.alert(
    messageVNode,
    translateOr("supplierRegistration.confirmation.title", "Application Submitted"),
    {
      confirmButtonText: translateOr(
        "supplierRegistration.confirmation.viewStatus",
        "View Application Status"
      ),
      type: "success",
      customClass: "registration-success-dialog",
    }
  );
};

const handlePreview = async () => {
  if (!isLastStep.value) {
    return;
  }
  if (!validateStep(currentStep.value)) {
    return;
  }
  if (!paymentTermsInRange()) {
    notification.warning(
      translateOr(
        "supplierRegistration.validation.paymentTerms",
        "Please select a valid payment term.",
      ),
    );
    return;
  }

  previewLoading.value = true;
  previewError.value = null;
  try {
    const payload = buildPayload();
    const preview = await previewApprovalFlow({
      payload,
      changedFields: [...changedFields.value],
    });
    approvalPreview.value = preview;
  } catch (error) {
    console.error("Failed to preview approval flow:", error);
    previewError.value =
      error instanceof Error
        ? error.message
        : translateOr(
            "supplierRegistration.preview.failed",
            "Unable to generate approval preview at this time.",
          );
  } finally {
    previewLoading.value = false;
  }
};

const handleSaveDraft = async ({ silent = false }: { silent?: boolean } = {}) => {
  if (!changedFields.value.length) {
    if (!silent) {
      notification.info(
        translateOr("supplierRegistration.draft.noChanges", "Nothing new to save."),
      );
    }
    return;
  }

  draftSaving.value = true;
  try {
    const formPayload = buildDraftPayload();
    const lastStepKey = getStepKey(currentStep.value);
    const response = await saveSupplierRegistrationDraft({
      draftToken: draftToken.value ?? undefined,
      form: formPayload,
      lastStep: lastStepKey,
    });

    draftToken.value = response.draftToken;
    draftExpiresAt.value = response.expiresAt ?? null;
    if (typeof window !== "undefined") {
      window.localStorage.setItem(DRAFT_STORAGE_KEY, response.draftToken);
    }

    const currentSnapshot = buildDraftPayload();
    changedFields.value = diffKeys(formPayload, currentSnapshot);
    draftError.value = null;

    if (changedFields.value.length) {
      scheduleAutoSave();
    }

    if (!silent) {
      notification.success(
        translateOr("supplierRegistration.draft.saveSuccess", "Draft saved successfully."),
      );
    }
  } catch (error) {
    console.error("Failed to save draft:", error);
    const message =
      error instanceof Error
        ? error.message
        : translateOr(
            "supplierRegistration.draft.saveFailed",
            "Unable to save draft.",
          );
    draftError.value = message;
    if (!silent) {
      notification.error(message);
    }
    scheduleAutoSave();
  } finally {
    draftSaving.value = false;
  }
};

const loadDraft = async (token: string) => {
  draftLoading.value = true;
  try {
    const response = await fetchSupplierRegistrationDraft(token);
    draftToken.value = response.draftToken;
    draftExpiresAt.value = response.expiresAt ?? null;
    applyFormValues(response.form as Partial<FormState>);
    submission.value = null;
    tempAccount.value = null;
    tempAccountError.value = null;
    showTempPassword.value = false;

    const stepIndex = response.lastStep
      ? steps.findIndex((step) => step.key === response.lastStep)
      : -1;
    if (stepIndex >= 0) {
      currentStep.value = stepIndex;
    }

    if (typeof window !== "undefined") {
      window.localStorage.setItem(DRAFT_STORAGE_KEY, response.draftToken);
    }
  } catch (error) {
    console.error("Failed to load draft:", error);
    draftError.value =
      error instanceof Error
        ? error.message
        : translateOr(
            "supplierRegistration.draft.loadFailed",
            "Unable to load saved draft.",
          );
    draftToken.value = null;
    draftExpiresAt.value = null;
    if (typeof window !== "undefined") {
      window.localStorage.removeItem(DRAFT_STORAGE_KEY);
    }
  } finally {
    draftLoading.value = false;
  }
};

const copyToClipboard = async (value: string, label: string): Promise<boolean> => {
  const successMsg = translateOr("supplierRegistration.tempAccount.copied", `${label} copied to clipboard.`);
  const failureMsg = translateOr("supplierRegistration.tempAccount.copyFailed", "Unable to copy to clipboard.");
  const succeeded = await utilCopyToClipboard(value);
  if (succeeded) notification.success(successMsg);
  else notification.warning(failureMsg);
  return succeeded;
};

const copyTrackingCredential = async (value: string | null | undefined, label: string, options: { markPassword?: boolean } = {}) => {
  if (!value) return;
  const copied = await copyToClipboard(value, label);
  if (copied && options.markPassword) { passwordCopied.value = true; schedulePasswordCopiedReset(); }
};

watch(form, (newVal, oldVal) => {
  if (suppressChangeTracking.value) return;
  Object.keys(newVal as Record<string, unknown>).forEach((key) => {
    const typedKey = key as keyof FormState;
    if ((newVal as FormState)[typedKey] !== (oldVal as FormState | undefined)?.[typedKey]) {
      if (!changedFields.value.includes(key)) {
        changedFields.value.push(key);
      }
      if (serverFieldErrors.value[typedKey]) {
        const nextErrors = { ...serverFieldErrors.value };
        delete nextErrors[typedKey];
        serverFieldErrors.value = nextErrors;
        if (!Object.keys(nextErrors).length) {
          submitError.value = null;
        }
      }
    }
  });
  if (approvalPreview.value) { approvalPreview.value = null; previewError.value = null; }
  scheduleAutoSave();
}, { deep: true });

const validateCreditCode = async () => {
  const creditCode = form.businessRegistrationNumber.trim();
  if (!creditCode) {
    creditCodeError.value = "";
    return;
  }

  validating.value = true;
  try {
    const result = await validateBlacklist({ credit_code: creditCode });
    if (result.is_blacklisted) {
      creditCodeError.value =
        result.severity === "critical"
          ? translateOr(
              "supplierRegistration.validation.blacklistCreditCode",
              "This registration number is blacklisted.",
            )
          : translateOr(
              "supplierRegistration.validation.blacklistCreditCodeReason",
              "This registration number requires manual review.",
            );
    } else {
      creditCodeError.value = "";
    }
  } catch (error) {
    console.error(
      translateOr(
        "supplierRegistration.errors.validateCreditCode",
        "Validation failed",
      ),
      error,
    );
    creditCodeError.value = "";
  } finally {
    validating.value = false;
  }
};

const validateContactEmail = async () => {
  const email = form.contactEmail.trim();
  if (!email) {
    emailError.value = "";
    return;
  }

  validating.value = true;
  try {
    const result = await validateBlacklist({ email });
    if (result.is_blacklisted) {
      emailError.value =
        result.severity === "critical"
          ? translateOr(
              "supplierRegistration.validation.blacklistEmail",
              "This email is blacklisted.",
            )
          : translateOr(
              "supplierRegistration.validation.blacklistEmailReason",
              "This email requires manual review.",
            );
    } else {
      emailError.value = "";
    }
  } catch (error) {
    console.error(
      translateOr(
        "supplierRegistration.errors.validateEmail",
        "Email validation failed",
      ),
      error,
    );
    emailError.value = "";
  } finally {
    validating.value = false;
  }
};

const handleSubmit = async () => {
  if (!validateStep(currentStep.value) || !paymentTermsInRange()) {
    if (!paymentTermsInRange()) notification.warning(translateOr("supplierRegistration.validation.paymentTerms", "Please select a valid payment term."));
    return;
  }
  if (creditCodeError.value || emailError.value) {
    notification.error(translateOr("supplierRegistration.validation.resolveBlacklist", "Please resolve the highlighted blacklist warnings before continuing."));
    return;
  }

  submitError.value = null;
  serverFieldErrors.value = {};

  tempAccount.value = null;
  tempAccountError.value = null;
  showTempPassword.value = false;
  loading.value = true;

  try {
    const response = await submitSupplierRegistration(buildPayload());
    submission.value = response;
    approvalPreview.value = null;
    previewError.value = null;
    changedFields.value = [];

    await showRegistrationSuccessDialog(response);
    openTrackingAccountDialog(response);

    if (draftToken.value && typeof window !== "undefined") window.localStorage.removeItem(DRAFT_STORAGE_KEY);
    draftToken.value = null;
    draftExpiresAt.value = null;
    cancelAutoSave();

    notification.success(`${translateOr("supplier.registration", "Supplier Registration")} ${translateOr("approval.notifications.approveSuccess", "submitted successfully")}`);
    submitError.value = null;
    serverFieldErrors.value = {};

    if (response.supplierId) {
      try {
        const issued = await issueTempAccount(response.supplierId, { currency: form.operatingCurrency });
        tempAccount.value = issued;
        showTempPassword.value = false;
      } catch (issueError) {
        console.error("Failed to issue temporary account:", issueError);
        tempAccountError.value = issueError instanceof Error ? issueError.message : translateOr("supplierRegistration.tempAccount.issueFailed", "Temporary account could not be issued automatically.");
      }
    }
  } catch (error) {
    console.error(translateOr("supplierRegistration.errors.submitFailed", "Registration failed"), error);
    const parsed = parseSubmissionError(error);
    applyServerErrors(parsed.message, parsed.fieldErrors);
    notification.error(parsed.message);
  } finally {
    loading.value = false;
  }
};

const toggleTempPassword = () => { showTempPassword.value = !showTempPassword.value; };
const viewRegistrationStatus = () => { if (!submission.value) return; router.push(`/registration-status/${submission.value.applicationId}`); };
const handleViewStatusFromDialog = () => { closeTrackingAccountDialog({ redirect: false }); viewRegistrationStatus(); };
const returnToLogin = () => { router.push("/login"); };

onMounted(() => {
  isMounted.value = true;
  const token = route.query.token as string | undefined;
  const rfq = route.query.rfqId as string | undefined;
  if (token && rfq) { invitationToken.value = token; rfqId.value = rfq; isFromInvitation.value = true; }

  const draftTokenFromQuery = route.query.draftToken as string | undefined;
  const storedDraftToken = typeof window !== "undefined" ? window.localStorage.getItem(DRAFT_STORAGE_KEY) : null;
  const candidateDraftToken = draftTokenFromQuery || storedDraftToken || null;
  if (candidateDraftToken) void loadDraft(candidateDraftToken);
});

onBeforeUnmount(() => {
  isMounted.value = false;
  cancelAutoSave();
  clearTrackingPasswordTimer();
  clearPasswordCopiedResetTimer();
});



</script>
<style scoped>
.registration-page {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding-bottom: 56px;
}

.registration-header {
  font-size: 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 24px 32px 0;
  color: #fff;
}

.registration-header h1 {
  font-weight: 600;
  margin: 0;
}

.registration-content {
  max-width: 980px;
  margin: 0 auto;
  padding: 32px 32px 64px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.draft-alert {
  border-radius: 12px;
}

.submit-alert {
  border-radius: 12px;
}

.invitation-banner {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px 20px;
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.9);
  box-shadow: 0 12px 24px rgba(31, 41, 55, 0.12);
  color: #1f2937;
}

.invitation-banner .banner-icon {
  font-size: 28px;
}

.invitation-banner .banner-content h3 {
  margin: 0;
  font-size: 18px;
}

.invitation-banner .banner-content p {
  margin: 4px 0 0;
  font-size: 14px;
  color: #4b5563;
}

:global(.registration-success-dialog) {
  max-width: 520px;
}

:global(.registration-success-dialog__body) {
  color: #1f2937;
}

:global(.registration-success-dialog__intro),
:global(.registration-success-dialog__note) {
  margin: 0 0 12px;
  line-height: 1.5;
}

:global(.registration-success-dialog__list) {
  list-style: none;
  padding: 0;
  margin: 0 0 12px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #f9fafb;
}

:global(.registration-success-dialog__list-item) {
  display: flex;
  flex-direction: column;
  padding: 10px 12px;
  border-bottom: 1px solid #e5e7eb;
}

:global(.registration-success-dialog__list-item:last-child) {
  border-bottom: none;
}

:global(.registration-success-dialog__list-label) {
  font-size: 12px;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

:global(.registration-success-dialog__list-value) {
  font-size: 14px;
  font-weight: 600;
  color: #111827;
}

.tracking-account-alert {
  margin-bottom: 16px;
}

.tracking-account-form :deep(.el-input) {
  width: 100%;
}

.tracking-account-password-note {
  display: block;
  margin-top: 4px;
  font-size: 12px;
  color: #6b7280;
}

.tracking-account-password-note--success {
  color: #16a34a;
}

.tracking-account-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.wizard-card {
  background: rgba(255, 255, 255, 0.95);
  border-radius: 20px;
  padding: 32px;
  box-shadow: 0 25px 65px rgba(15, 23, 42, 0.22);
  display: flex;
  flex-direction: column;
  gap: 28px;
}

.wizard-header {
  padding-bottom: 12px;
  border-bottom: 1px solid rgba(148, 163, 184, 0.2);
}

.wizard-body {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.draft-meta {
  font-size: 12px;
  color: #6b7280;
  margin-left: 12px;
}

.ghost-button {
  background: transparent;
  border: 1px solid #8b5cf6;
  color: #6d28d9;
}

.ghost-button:hover {
  border-color: #5b21b6;
  color: #4c1d95;
}

.registration-content :deep(.wizard-step) {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.registration-content :deep(.step-title) {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #1f2937;
}

.registration-content :deep(.step-description) {
  margin: 0;
  color: #6b7280;
  font-size: 14px;
}

.registration-content :deep(.form-grid) {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 18px 24px;
}

.registration-content :deep(label) {
  display: flex;
  flex-direction: column;
  gap: 8px;
  font-size: 13px;
  color: #374151;
}

.registration-content :deep(label input),
.registration-content :deep(label select),
.registration-content :deep(label textarea) {
  border: 1px solid #d1d5db;
  border-radius: 8px;
  padding: 10px 12px;
  font-size: 14px;
  color: #1f2937;
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.registration-content :deep(label textarea) {
  resize: vertical;
}

.registration-content :deep(label input:focus),
.registration-content :deep(label select:focus),
.registration-content :deep(label textarea:focus) {
  outline: none;
  border-color: #7c3aed;
  box-shadow: 0 0 0 2px rgba(124, 58, 237, 0.16);
}

.registration-content :deep(.category-badge) {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-left: 8px;
  padding: 2px 6px;
  border-radius: 12px;
  background: rgba(124, 58, 237, 0.12);
  color: #5b21b6;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.registration-content :deep(.span-2) {
  grid-column: span 2;
}

.registration-content :deep(.error-input) {
  border-color: #f97316;
  background: rgba(249, 115, 22, 0.08);
}

.registration-content :deep(.error-message) {
  color: #f97316;
  font-size: 12px;
}

.registration-content :deep(.payment-methods) {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.registration-content :deep(.checkbox-grid) {
  display: flex;
  flex-wrap: wrap;
  gap: 12px 20px;
}

.registration-content :deep(.preview-panel) {
  margin-top: 8px;
  padding: 16px;
  border-radius: 16px;
  background: rgba(248, 250, 252, 0.8);
  border: 1px solid rgba(148, 163, 184, 0.2);
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.registration-content :deep(.preview-steps) {
  list-style: none;
  display: grid;
  gap: 12px;
  padding: 0;
  margin: 0;
}

.registration-content :deep(.preview-steps li) {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
  padding: 12px 16px;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 4px 14px rgba(15, 23, 42, 0.08);
}

.registration-content :deep(.preview-risks ul) {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 8px;
}

.registration-content :deep(.risk-flag) {
  padding: 8px 12px;
  border-radius: 10px;
  font-size: 13px;
}

.registration-content :deep(.risk-flag.warning) {
  background: rgba(255, 193, 7, 0.18);
  color: #8a6d3b;
}

.registration-content :deep(.risk-flag.critical) {
  background: rgba(248, 113, 113, 0.18);
  color: #b91c1c;
}

.registration-content :deep(.wizard-footer) {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  border-top: 1px solid rgba(148, 163, 184, 0.18);
  padding-top: 16px;
}

.registration-content :deep(.wizard-footer .footer-left),
.registration-content :deep(.wizard-footer .footer-right) {
  display: flex;
  align-items: center;
  gap: 12px;
}

.registration-content :deep(.wizard-footer .el-button + .el-button) {
  margin-left: 0;
}

.registration-content :deep(.submission-card) {
  background: rgba(255, 255, 255, 0.92);
  border-radius: 20px;
  padding: 32px;
  box-shadow: 0 22px 55px rgba(15, 23, 42, 0.24);
  color: #303133;
}

.registration-content :deep(.submission-card h2) {
  margin-top: 0;
  font-size: 24px;
  font-weight: 600;
}

.registration-content :deep(.submission-card p) {
  color: #606266;
}

.registration-content :deep(.submission-details) {
  list-style: none;
  padding: 0;
  margin: 20px 0;
  display: grid;
  gap: 8px;
}

.registration-content :deep(.submission-actions) {
  margin-top: 24px;
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.registration-content :deep(.temp-account-card) {
  margin-top: 24px;
  padding: 20px;
  border-radius: 12px;
  background: #f5f7fa;
  border: 1px solid #ebeef5;
}

.registration-content :deep(.temp-account-credentials) {
  display: grid;
  gap: 12px;
  margin-bottom: 16px;
}

.registration-content :deep(.credential-row) {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.registration-content :deep(.credential-label) {
  font-weight: 600;
  color: #606266;
  min-width: 120px;
}

.registration-content :deep(.credential-value) {
  font-family: "Fira Code", "Courier New", monospace;
  background: #fff;
  padding: 4px 8px;
  border-radius: 6px;
  border: 1px dashed #dcdfe6;
  color: #303133;
}

.registration-content :deep(.temp-account-qr) {
  margin-top: 12px;
  text-align: center;
}

.registration-content :deep(.temp-account-qr img) {
  width: 160px;
  height: 160px;
  border-radius: 12px;
  border: 1px solid #dcdfe6;
  background: #fff;
  padding: 8px;
}

.registration-content :deep(.temp-account-qr p) {
  margin-top: 8px;
  font-size: 12px;
  color: #909399;
}

.registration-content :deep(.temp-account-error) {
  margin-top: 16px;
  color: #f56c6c;
  font-size: 13px;
}

@media (max-width: 768px) {
  .registration-content {
    padding: 16px 16px 48px;
  }

  .wizard-card {
    padding: 24px 20px;
  }

  .registration-content :deep(.form-grid) {
    grid-template-columns: 1fr;
  }

  .registration-content :deep(.span-2) {
    grid-column: span 1;
  }

  .registration-content :deep(.wizard-footer) {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }

  .registration-content :deep(.wizard-footer .footer-left),
  .registration-content :deep(.wizard-footer .footer-right) {
    justify-content: stretch;
  }

  .registration-content :deep(.credential-actions) {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
