<template>
  <div class="registration-status-page">
    <header class="status-header">
      <el-button link type="primary" @click="goBack">
        {{ t("common.back") }}
      </el-button>
      <h1>{{ t("registrationStatus.title") }}</h1>
      <p class="status-subtitle">
        {{ t("registrationStatus.subtitle") }}
      </p>
    </header>

    <el-alert
      v-if="!!error"
      type="error"
      :title="error"
      show-icon
      class="status-alert"
    />

    <div v-if="loading" class="status-loading">
      <el-skeleton :rows="6" animated />
    </div>

    <div v-else-if="status">
      <section class="status-summary">
        <el-card shadow="hover">
          <div class="summary-grid">
            <div>
              <span class="summary-label">{{ t("registrationStatus.fields.applicationId") }}</span>
              <span class="summary-value">{{ status.applicationId }}</span>
            </div>
            <div>
              <span class="summary-label">{{ t("registrationStatus.fields.supplierCode") }}</span>
              <span class="summary-value">{{ status.supplierCode || "-" }}</span>
            </div>
            <div>
              <span class="summary-label">{{ t("registrationStatus.fields.status") }}</span>
              <el-tag :type="statusTagType" class="summary-tag">{{ statusLabel }}</el-tag>
            </div>
            <div>
              <span class="summary-label">{{ t("registrationStatus.fields.stage") }}</span>
              <span class="summary-value">{{ status.supplierStage || t("registrationStatus.stage.pending") }}</span>
            </div>
            <div>
              <span class="summary-label">{{ t("registrationStatus.fields.currentApprover") }}</span>
              <span class="summary-value">{{ status.currentApprover || "-" }}</span>
            </div>
            <div>
              <span class="summary-label">{{ t("registrationStatus.fields.lastUpdated") }}</span>
              <span class="summary-value">{{ formatDate(status.updatedAt) }}</span>
            </div>
          </div>
        </el-card>
      </section>

      <section class="status-history">
        <h2>{{ t("registrationStatus.history.title") }}</h2>
        <el-empty
          v-if="status.history.length === 0"
          :description="t('registrationStatus.history.empty')"
        />
        <el-timeline v-else>
          <el-timeline-item
            v-for="item in status.history"
            :key="itemKey(item)"
            :type="item.type === 'approval' ? 'primary' : 'success'"
            :timestamp="formatDateTime(item.occurredAt)"
          >
            <div class="timeline-entry">
              <div class="timeline-title">
                {{ formatHistoryTitle(item) }}
              </div>
              <div v-if="item.comments" class="timeline-comments">
                {{ item.comments }}
              </div>
            </div>
          </el-timeline-item>
        </el-timeline>
      </section>

      <section v-if="status.tempAccount" class="temp-account-section">
        <el-card shadow="never">
          <template #header>
            <div class="card-header">
              <span>{{ t("registrationStatus.tempAccount.title") }}</span>
              <el-tag size="small" type="info">{{ tempAccountStatusLabel }}</el-tag>
            </div>
          </template>

          <dl class="temp-account-grid">
            <div>
              <dt>{{ t("auth.usernameLabel") }}</dt>
              <dd>
                {{ status.tempAccount.username || "-" }}
                <el-button
                  v-if="status.tempAccount.username"
                  size="small"
                  type="primary"
                  link
                  @click="copy(status.tempAccount.username, t('auth.usernameLabel'))"
                >
                  {{ t("common.copy") }}
                </el-button>
              </dd>
            </div>
            <div>
              <dt>{{ t("auth.passwordLabel") }}</dt>
              <dd>
                {{ maskedPassword }}
                <el-button
                  v-if="status.tempAccount.username"
                  size="small"
                  type="primary"
                  link
                  @click="togglePassword"
                >
                  {{ showPassword ? t("common.hide") : t("common.show") }}
                </el-button>
              </dd>
            </div>
            <div>
              <dt>{{ t("registrationStatus.tempAccount.expiresAt") }}</dt>
              <dd>{{ formatDate(status.tempAccount.expiresAt) }}</dd>
            </div>
            <div>
              <dt>{{ t("registrationStatus.tempAccount.lastLogin") }}</dt>
              <dd>{{ formatDateTime(status.tempAccount.lastLoginAt) }}</dd>
            </div>
          </dl>
        </el-card>
      </section>
    </div>

    <el-empty
      v-else
      class="status-empty"
      :description="t('registrationStatus.empty')"
    />
  </div>
</template>

<script setup lang="ts">




import { ref, computed, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";

import {
  fetchSupplierRegistrationStatus,
  fetchSupplierRegistrationStatusBySupplier,
  fetchSupplierRegistrationStatusForCurrentUser,
  type SupplierRegistrationStatusResponse,
} from "@/api/public";
import { useAuthStore } from "@/stores/auth";


import { useNotification } from "@/composables";
const notification = useNotification();

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const authStore = useAuthStore();

const loading = ref(true);
const error = ref<string | null>(null);
const status = ref<SupplierRegistrationStatusResponse | null>(null);
const showPassword = ref(false);

const resolvedApplicationId = computed(() => {
  const paramId = route.params.applicationId as string | undefined;
  const queryId = route.query.applicationId as string | undefined;
  return paramId || queryId || null;
});

const statusLabel = computed(() => {
  if (!status.value) return "-";
  const key = `registrationStatus.statuses.${status.value.status}`;
  const translation = t(key);
  return translation === key ? status.value.status : translation;
});

const statusTagType = computed(() => {
  switch (status.value?.status) {
    case "approved":
    case "completed":
      return "success";
    case "rejected":
      return "danger";
    case "under_review":
    case "pending":
    default:
      return "info";
  }
});

const maskedPassword = computed(() => {
  if (!status.value?.tempAccount?.username) {
    return "-";
  }
  const actual = status.value.tempAccount.username;
  if (showPassword.value) {
    return actual;
  }
  return actual.replace(/./g, "*");
});

const tempAccountStatusLabel = computed(() => {
  const state = status.value?.tempAccount?.status;
  if (!state) {
    return t("registrationStatus.tempAccount.statuses.unknown");
  }
  const key = `registrationStatus.tempAccount.statuses.${state}`;
  const translated = t(key);
  return translated === key ? state : translated;
});

const goBack = () => {
  if (window.history.length > 1) {
    router.back();
  } else {
    router.push({ name: "dashboard" });
  }
};

const loadStatus = async () => {
  loading.value = true;
  error.value = null;
  status.value = null;
  showPassword.value = false;

  try {
    const id = resolvedApplicationId.value ? Number(resolvedApplicationId.value) : null;
    const relatedId = authStore.user?.relatedApplicationId ?? null;
    if (id && Number.isFinite(id)) {
      status.value = await fetchSupplierRegistrationStatus(id);
    } else if (relatedId != null) {
      status.value = await fetchSupplierRegistrationStatus(relatedId);
    } else if (authStore.user?.supplierId != null) {
      status.value = await fetchSupplierRegistrationStatusBySupplier(authStore.user.supplierId);
    } else if (authStore.user) {
      status.value = await fetchSupplierRegistrationStatusForCurrentUser();
    } else {
      error.value = t("registrationStatus.errors.noIdentifier");
    }
  } catch (err) {
    console.error("Failed to fetch registration status", err);
    error.value = err instanceof Error ? err.message : t("errors.general");
  } finally {
    loading.value = false;
  }
};

const copy = async (value: string, label: string) => {
  try {
    await navigator.clipboard.writeText(value);
    notification.success(t("registrationStatus.actions.copied", { label }));
  } catch (err) {
    console.error("Clipboard copy failed", err);
    notification.error(t("errors.copyFailed"));
  }
};

const togglePassword = () => {
  showPassword.value = !showPassword.value;
};

const formatDate = (value: string | null | undefined) => {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString();
};

const formatDateTime = (value: string | null | undefined) => {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
};

const formatHistoryTitle = (item: SupplierRegistrationStatusResponse["history"][number]) => {
  if (item.type === "registration") {
    return t("registrationStatus.history.submitted");
  }
  const key = `registrationStatus.history.steps.${item.step}`;
  const stepLabel = t(key);
  return stepLabel === key
    ? `${item.step ?? "approval"} (${item.status ?? "-"})`
    : `${stepLabel} (${item.status ?? "-"})`;
};

const itemKey = (item: SupplierRegistrationStatusResponse["history"][number]) => {
  return `${item.type}-${item.step ?? "root"}-${item.occurredAt ?? "n/a"}`;
};

watch(
  () => [resolvedApplicationId.value, authStore.user?.supplierId, authStore.user?.relatedApplicationId],
  () => {
    loadStatus();
  },
  { immediate: true },
);




</script>

<style scoped>
.registration-status-page {
  max-width: 960px;
  margin: 0 auto;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.status-header {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.status-header h1 {
  margin: 0;
  font-size: 26px;
  font-weight: 600;
}

.status-subtitle {
  margin: 0;
  font-size: 14px;
  color: #606266;
}

.status-alert {
  margin-top: -8px;
}

.status-loading {
  padding: 24px;
  border-radius: 12px;
  background: #ffffff;
  border: 1px solid #ebeef5;
}

.status-summary {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 16px;
}

.summary-label {
  display: block;
  font-size: 12px;
  color: #909399;
  text-transform: uppercase;
  letter-spacing: 0.8px;
  margin-bottom: 6px;
}

.summary-value {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.summary-tag {
  margin-top: 4px;
}

.status-history {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.timeline-entry {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.timeline-title {
  font-weight: 600;
  color: #303133;
}

.timeline-comments {
  font-size: 13px;
  color: #606266;
}

.temp-account-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.temp-account-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 12px;
  margin: 0;
}

.temp-account-grid dt {
  font-size: 12px;
  text-transform: uppercase;
  color: #909399;
}

.temp-account-grid dd {
  margin: 4px 0 0 0;
  font-size: 15px;
  color: #303133;
  display: flex;
  align-items: center;
  gap: 8px;
}

.status-empty {
  margin-top: 32px;
}

@media (max-width: 600px) {
  .registration-status-page {
    padding: 16px;
  }
}
</style>
