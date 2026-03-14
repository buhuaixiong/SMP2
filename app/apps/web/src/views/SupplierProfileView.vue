<template>
  <div class="supplier-profile-view">
    <PageHeader
      v-if="!isLoading"
      :title="t('supplier.profilePage.title')"
      :subtitle="isReadOnly ? undefined : t('supplier.profilePage.subtitle')"
    >
      <template #actions>
        <el-button link class="back-button" @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          {{ t("common.back") }}
        </el-button>
      </template>
    </PageHeader>

    <el-skeleton v-if="isLoading" :rows="6" animated />

    <el-empty
      v-else-if="!currentSupplier"
      :description="t('supplier.profilePage.noSupplier')"
      class="profile-empty"
    />

    <template v-else>
      <el-row :gutter="20" class="summary-row">
        <el-col :xs="24" :md="12">
          <el-card class="summary-card">
            <template #header>
              <div class="card-header">
                <span>{{ t('supplier.profilePage.statusCard.title') }}</span>
              </div>
            </template>

            <el-descriptions :column="1" border size="small">
              <el-descriptions-item :label="t('supplier.profilePage.statusCard.status')">
                <el-tag type="info">{{ statusLabel }}</el-tag>
              </el-descriptions-item>
              <el-descriptions-item label="Compliance">
                <div class="completion">
                  <span class="percent">{{ profileCompletion }}%</span>
                  <span class="hint">{{ t('supplier.profile.completion') }}</span>
                </div>
              </el-descriptions-item>
              <el-descriptions-item :label="t('supplier.profilePage.statusCard.missingFields')">
                <template v-if="missingProfileFields.length">
                  <el-tag
                    v-for="field in missingProfileFields"
                    :key="field.key"
                    type="danger"
                    class="chip"
                  >
                    {{ field.label }}
                  </el-tag>
                </template>
                <span v-else>{{ t('supplier.profilePage.statusCard.noMissingFields') }}</span>
              </el-descriptions-item>
              <el-descriptions-item :label="t('supplier.profilePage.statusCard.missingDocuments')">
                <template v-if="missingDocumentTypes.length">
                  <el-tag
                    v-for="doc in missingDocumentTypes"
                    :key="doc.type"
                    type="warning"
                    class="chip"
                  >
                    {{ doc.label }}
                  </el-tag>
                </template>
                <span v-else>{{ t('supplier.profilePage.statusCard.noMissingDocuments') }}</span>
              </el-descriptions-item>
            </el-descriptions>
          </el-card>
        </el-col>

        <el-col :xs="24" :md="12">
          <el-card class="summary-card">
            <template #header>
              <div class="card-header">
                <span>{{ t('supplier.profilePage.documentsCard.title') }}</span>
              </div>
            </template>

            <div class="documents-summary">
              <p>
                {{ t('supplier.documents.title') }}: {{ documentsUploaded }}
              </p>
              <el-alert
                v-if="!isReadOnly && missingDocumentTypes.length"
                type="warning"
                :closable="false"
                show-icon
                class="alert"
              >
                <span>{{ t('supplier.profilePage.documentsCard.uploadCta') }}</span>
              </el-alert>
              <el-button v-if="!isReadOnly" type="primary" @click="goToFileUploads">
                {{ t('supplier.profilePage.documentsCard.uploadCta') }}
              </el-button>
            </div>
          </el-card>
        </el-col>
      </el-row>

      <el-card class="form-card">
        <template #header>
          <div class="card-header">
            <span>
              {{
                isReadOnly
                  ? t('supplier.profilePage.title')
                  : t('supplier.profilePage.formCard.title')
              }}
            </span>
            <small v-if="!isReadOnly">{{ t('supplier.profilePage.formCard.description') }}</small>
          </div>
        </template>

        <el-form ref="formRef" :model="form" :rules="rules" label-position="top">
          <el-row :gutter="20">
            <el-col :xs="24" :md="12">
              <el-form-item prop="companyName" :label="t('supplier.fields.companyName')">
                <el-input v-model="form.companyName" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="companyId" :label="t('supplier.fields.companyId')">
                <el-input v-model="form.companyId" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="contactPerson" :label="t('supplier.fields.contactPerson')">
                <el-input v-model="form.contactPerson" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="contactEmail" :label="t('supplier.fields.contactEmail')">
                <el-input v-model="form.contactEmail" type="email" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="contactPhone" :label="t('supplier.fields.contactPhone')">
                <el-input v-model="form.contactPhone" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="category" :label="t('supplier.fields.category')">
                <el-select v-model="form.category" filterable allow-create :disabled="isReadOnly">
                  <el-option value="Raw material" :label="t('supplier.categories.rawMaterial')" />
                  <el-option value="IT" :label="t('supplier.categories.it')" />
                  <el-option value="Services" :label="t('supplier.categories.services')" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="region" :label="t('supplier.fields.region')">
                <el-input v-model="form.region" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="businessRegistrationNumber" :label="t('supplier.fields.businessRegistrationNumber')">
                <el-input v-model="form.businessRegistrationNumber" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24">
              <el-form-item prop="address" :label="t('supplier.fields.address')">
                <el-input v-model="form.address" type="textarea" :rows="2" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="bankAccount" :label="t('supplier.fields.bankAccount')">
                <el-input v-model="form.bankAccount" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="paymentTerms" :label="t('supplier.fields.paymentTerms')">
                <el-input v-model="form.paymentTerms" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24" :md="12">
              <el-form-item prop="paymentCurrency" :label="t('supplier.fields.paymentCurrency')">
                <el-input v-model="form.paymentCurrency" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
            <el-col :xs="24">
              <el-form-item prop="notes" :label="t('supplier.fields.notes')">
                <el-input v-model="form.notes" type="textarea" :rows="3" :readonly="isReadOnly" />
              </el-form-item>
            </el-col>
          </el-row>

          <el-form-item v-if="!isReadOnly">
            <el-button type="primary" :loading="isSubmitting" @click="handleSubmit">
              {{
                isSubmitting
                  ? t('supplier.profilePage.formCard.submitting')
                  : t('supplier.profilePage.formCard.submit')
              }}
            </el-button>
            <el-button text type="primary" @click="goToFileUploads">
              {{ t('supplier.profilePage.documentsCard.uploadCta') }}
            </el-button>
          </el-form-item>
        </el-form>
      </el-card>
    </template>
  </div>
</template>

<script setup lang="ts">




import { computed, onMounted, reactive, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";

import type { FormInstance, FormRules } from "element-plus";
import PageHeader from "@/components/layout/PageHeader.vue";
import { ArrowLeft } from "@element-plus/icons-vue";
import { useAuthStore } from "@/stores/auth";
import { useSupplierStore } from "@/stores/supplier";
import type { Supplier } from "@/types";


import { useNotification } from "@/composables";

const notification = useNotification();
const router = useRouter();
const { t } = useI18n();
const authStore = useAuthStore();
const supplierStore = useSupplierStore();

const supplierId = computed(() => {
  const raw = authStore.user?.supplierId;
  if (raw === null || raw === undefined) {
    return null;
  }
  const numeric = typeof raw === "number" ? raw : Number(raw);
  return Number.isFinite(numeric) ? Math.trunc(numeric) : null;
});

const isLoading = ref(false);
const isSubmitting = ref(false);
const isReadOnly = ref(true);
const formRef = ref<FormInstance>();

const form = reactive({
  companyName: "",
  companyId: "",
  contactPerson: "",
  contactPhone: "",
  contactEmail: "",
  category: "",
  address: "",
  businessRegistrationNumber: "",
  region: "",
  bankAccount: "",
  paymentTerms: "",
  paymentCurrency: "",
  notes: "",
});

const rules: FormRules = {
  companyName: [{ required: true, message: t("common.required"), trigger: "blur" }],
  companyId: [{ required: true, message: t("common.required"), trigger: "blur" }],
  contactPerson: [{ required: true, message: t("common.required"), trigger: "blur" }],
  contactEmail: [{ required: true, message: t("common.required"), trigger: "blur" }],
  contactPhone: [{ required: true, message: t("common.required"), trigger: "blur" }],
  category: [{ required: true, message: t("common.required"), trigger: "change" }],
  address: [{ required: true, message: t("common.required"), trigger: "blur" }],
  businessRegistrationNumber: [{ required: true, message: t("common.required"), trigger: "blur" }],
  region: [{ required: true, message: t("common.required"), trigger: "blur" }],
  bankAccount: [{ required: true, message: t("common.required"), trigger: "blur" }],
  paymentTerms: [{ required: true, message: t("common.required"), trigger: "blur" }],
  paymentCurrency: [{ required: true, message: t("common.required"), trigger: "blur" }],
};

const currentSupplier = computed<Supplier | null>(() => supplierStore.selectedSupplier ?? null);

const profileCompletion = computed(() => currentSupplier.value?.profileCompletion ?? 0);
const documentsUploaded = computed(() => currentSupplier.value?.documents?.length ?? 0);

const missingProfileFields = computed<Array<{ key: string; label: string }>>(
  () => currentSupplier.value?.complianceSummary?.missingProfileFields ?? [],
);
const missingDocumentTypes = computed<Array<{ type: string; label: string }>>(
  () => currentSupplier.value?.complianceSummary?.missingDocumentTypes ?? [],
);

const statusLabel = computed(() => {
  const statusKey = currentSupplier.value?.status ?? "unknown";
  const translated = t(`supplier.status.${statusKey}`, statusKey);
  return translated === statusKey ? statusKey : translated;
});

const populateForm = (supplier: Supplier | null) => {
  form.companyName = supplier?.companyName ?? "";
  form.companyId = supplier?.companyId ?? "";
  form.contactPerson = supplier?.contactPerson ?? "";
  form.contactPhone = supplier?.contactPhone ?? "";
  form.contactEmail = supplier?.contactEmail ?? "";
  form.category = supplier?.category ?? "";
  form.address = supplier?.address ?? "";
  form.businessRegistrationNumber = supplier?.businessRegistrationNumber ?? "";
  form.region = supplier?.region ?? "";
  form.bankAccount = supplier?.bankAccount ?? "";
  form.paymentTerms = supplier?.paymentTerms ?? "";
  form.paymentCurrency = supplier?.paymentCurrency ?? "";
  form.notes = supplier?.notes ?? "";
};

watch(currentSupplier, (value) => {
  populateForm(value);
});

const loadSupplier = async () => {
  if (!supplierId.value) {
    return;
  }
  isLoading.value = true;
  try {
    await supplierStore.selectSupplier(supplierId.value);
  } catch (error) {
    console.error('Failed to load supplier profile', error);
    notification.error(t('supplier.profilePage.loadError'));
  } finally {
    isLoading.value = false;
  }
};

const resolveFieldLabel = (key: string) => {
  const translationKey = `supplier.fields.${key}`;
  const translated = t(translationKey);
  return translated === translationKey ? key : translated;
};

const resolveDocumentLabel = (type: string) => {
  const mapping: Record<string, string> = {
    business_license: t('supplier.documents.categories.businessLicense'),
    tax_certificate: t('supplier.documents.categories.taxCertificate'),
    bank_information: t('supplier.documents.categories.bankInformation'),
  };
  return mapping[type] || type;
};

const handleSubmit = async () => {
  if (isReadOnly.value) {
    return;
  }
  if (!supplierId.value || !formRef.value) {
    return;
  }
  try {
    await formRef.value.validate();
  } catch {
    return;
  }
  isSubmitting.value = true;
  try {
    const payload = { ...form };
    const result = await supplierStore.updateSupplier(supplierId.value, payload);
    if (result && typeof result === 'object' && ("isChangeRequest" in result || "flow" in result)) {
      notification.success(t('supplier.profilePage.formCard.changeRequestCreated'));
    } else {
      notification.success(t('supplier.profilePage.formCard.updateSuccess'));
    }
    await loadSupplier();
  } catch (error) {
    console.error('Failed to submit profile update', error);
    notification.error(t('supplier.profilePage.formCard.submitError'));
  } finally {
    isSubmitting.value = false;
  }
};

const goToFileUploads = () => {
  router.push('/supplier/file-uploads');
};

const handleBack = () => {
  router.back();
};

onMounted(() => {
  populateForm(currentSupplier.value);
  loadSupplier();
});




</script>

<style scoped>
.supplier-profile-view {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 24px;
}

.summary-row {
  margin-top: 4px;
}

.summary-card {
  height: 100%;
}

.card-header {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.card-header span {
  font-weight: 600;
  font-size: 16px;
}

.card-header small {
  color: #909399;
}

.completion {
  display: flex;
  align-items: center;
  gap: 12px;
}

.completion .percent {
  font-size: 20px;
  font-weight: 600;
  color: #409eff;
}

.completion .hint {
  color: #909399;
}

.chip {
  margin-right: 8px;
  margin-bottom: 6px;
}

.documents-summary {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.documents-summary .alert {
  margin: 0;
}

.form-card {
  margin-top: 8px;
}

.profile-empty {
  margin-top: 40px;
}

.back-button {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: #409eff;
  font-size: 14px;
  padding: 4px 8px;
}

.back-button:hover {
  color: #66b1ff;
}

@media (max-width: 768px) {
  .supplier-profile-view {
    padding: 16px;
  }
}
</style>
