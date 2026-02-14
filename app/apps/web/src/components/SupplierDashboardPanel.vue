<template>
  <section v-if="isSupplierUser && currentSupplier" class="supplier-dashboard-panel">
    <div class="dashboard-hero">
      <div class="hero-text">
        <p class="hero-kicker">
          {{ t("dashboard.supplierPanel.heroKicker", { name: supplierWelcomeName }) }}
        </p>
        <h2 class="hero-title">
          {{ supplierWorkspaceName }}
        </h2>
        <p class="hero-subtitle">
          {{ t("dashboard.supplierPanel.heroSubtitle") }}
        </p>
      </div>
      <div class="hero-actions">
        <div class="hero-metric">
          <span class="metric-number">{{ totalPendingTasks }}</span>
          <span class="metric-label">{{ t("dashboard.supplierPanel.metrics.pending") }}</span>
        </div>
        <button
          class="hero-refresh"
          type="button"
          :disabled="invitationLoading"
          @click="refreshSupplierTasks"
        >
          {{
            invitationLoading
              ? t("dashboard.supplierPanel.metrics.refreshing")
              : t("dashboard.supplierPanel.metrics.refresh")
          }}
        </button>
      </div>
    </div>

    <div class="task-grid">
      <article v-if="shouldShowRegistrationCard" class="task-card registration-status-card">
        <header class="task-card-header">
          <h3>{{ t("dashboard.supplierPanel.cards.registrationApplication.title", "Registration Application") }}</h3>
          <el-tag :type="registrationStatusTagType" size="small">
            {{ registrationStatusLabel }}
          </el-tag>
        </header>
        <p class="task-description">
          {{ t("dashboard.supplierPanel.cards.registrationApplication.description", "Track your registration approval progress") }}
        </p>
        <div class="task-actions" style="margin-top: 12px;">
          <el-button size="small" type="primary" @click="router.push('/registration-status')">
            {{ t("dashboard.supplierPanel.cards.registrationApplication.action", "View Details") }}
          </el-button>
        </div>
      </article>

      <article class="task-card">
        <header class="task-card-header">
          <h3>{{ t("dashboard.supplierPanel.cards.requiredDocuments.title") }}</h3>
          <span class="task-badge">{{ missingDocumentTypes.length }}</span>
        </header>
        <p class="task-description">
          {{ t("dashboard.supplierPanel.cards.requiredDocuments.description") }}
        </p>
        <ul class="task-list">
          <li v-for="doc in missingDocumentTypes.slice(0, 3)" :key="doc.type">
            {{ doc.label }}
          </li>
          <li v-if="missingDocumentTypes.length === 0" class="task-empty">
            {{ t("dashboard.supplierPanel.cards.requiredDocuments.empty") }}
          </li>
          <li v-else-if="missingDocumentTypes.length > 3" class="task-extra">
            {{
              t("dashboard.supplierPanel.cards.requiredDocuments.more", {
                count: missingDocumentTypes.length - 3,
              })
            }}
          </li>
        </ul>
      </article>

      <article class="task-card">
        <header class="task-card-header">
          <h3>{{ t("dashboard.supplierPanel.cards.expiringDocuments.title") }}</h3>
          <span class="task-badge">{{ expiringDocuments.length }}</span>
        </header>
        <p class="task-description">
          {{ t("dashboard.supplierPanel.cards.expiringDocuments.description") }}
        </p>
        <ul class="task-list">
          <li v-for="doc in expiringDocuments.slice(0, 3)" :key="'exp-' + doc.id" class="task-row">
            <span>{{ doc.label }}</span>
            <span class="task-meta">{{ formatDaysCountdown(doc.daysRemaining) }}</span>
          </li>
          <li v-if="expiringDocuments.length === 0" class="task-empty">
            {{ t("dashboard.supplierPanel.cards.expiringDocuments.empty") }}
          </li>
          <li v-else-if="expiringDocuments.length > 3" class="task-extra">
            {{
              t("dashboard.supplierPanel.cards.expiringDocuments.more", {
                count: expiringDocuments.length - 3,
              })
            }}
          </li>
        </ul>
      </article>

      <article class="task-card">
        <header class="task-card-header">
          <h3>{{ t("dashboard.supplierPanel.cards.rfqs.title") }}</h3>
          <span class="task-badge">{{ pendingRfqInvitations.length }}</span>
        </header>
        <p class="task-description">
          {{ t("dashboard.supplierPanel.cards.rfqs.description") }}
        </p>
        <ul class="task-list">
          <li v-if="invitationLoading" class="task-empty">
            {{ t("dashboard.supplierPanel.cards.rfqs.loading") }}
          </li>
          <li
            v-else
            v-for="rfq in pendingRfqInvitations.slice(0, 3)"
            :key="'rfq-' + rfq.id"
            class="task-row"
          >
            <span>{{ rfq.title }}</span>
            <span class="task-meta">{{ formatDaysCountdown(rfq.daysRemaining) }}</span>
          </li>
          <li v-if="!invitationLoading && pendingRfqInvitations.length === 0" class="task-empty">
            {{ t("dashboard.supplierPanel.cards.rfqs.empty") }}
          </li>
          <li v-else-if="!invitationLoading && pendingRfqInvitations.length > 3" class="task-extra">
            {{
              t("dashboard.supplierPanel.cards.rfqs.more", {
                count: pendingRfqInvitations.length - 3,
              })
            }}
          </li>
        </ul>
      </article>

      <article class="task-card">
        <header class="task-card-header">
          <h3>{{ t("dashboard.supplierPanel.cards.profile.title") }}</h3>
          <span class="task-badge">{{ missingProfileFields.length }}</span>
        </header>
        <p class="task-description">
          {{ t("dashboard.supplierPanel.cards.profile.description") }}
        </p>
        <ul class="task-list">
          <li v-for="field in missingProfileFields.slice(0, 3)" :key="field.key">
            {{ field.label }}
          </li>
          <li v-if="missingProfileFields.length === 0" class="task-empty">
            {{ t("dashboard.supplierPanel.cards.profile.empty") }}
          </li>
          <li v-else-if="missingProfileFields.length > 3" class="task-extra">
            {{
              t("dashboard.supplierPanel.cards.profile.more", {
                count: missingProfileFields.length - 3,
              })
            }}
          </li>
        </ul>
      </article>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import { useSupplierStore } from "@/stores/supplier";
import { useAuthStore } from "@/stores/auth";
import { listSupplierRfqInvitations } from "@/api/rfq";
import { fetchSupplierRegistrationStatusBySupplier, type SupplierRegistrationStatusResponse } from "@/api/public";
import {
  SupplierStage,
  type SupplierComplianceSummary,
  type SupplierRfqInvitationSummary,
} from "@/types";

const supplierStore = useSupplierStore();
const authStore = useAuthStore();
const router = useRouter();
const { t } = useI18n();

const registrationStatus = ref<SupplierRegistrationStatusResponse | null>(null);
const registrationStatusLoading = ref(false);

const emptyComplianceSummary: SupplierComplianceSummary = {
  requiredProfileFields: [],
  missingProfileFields: [],
  requiredDocumentTypes: [],
  missingDocumentTypes: [],
  isProfileComplete: true,
  isDocumentComplete: true,
  isComplete: true,
  profileScore: 100,
  documentScore: 100,
  overallScore: 100,
  completionCategory: "complete",
  missingItems: [],
};

const isSupplierUser = computed(() => {
  const user = authStore.user;
  if (!user || user.supplierId === null || user.supplierId === undefined) {
    return false;
  }
  const staffRoles = new Set([
    "admin",
    "purchaser",
    "procurement_manager",
    "procurement_director",
    "finance_accountant",
    "finance_director",
  ]);
  if (staffRoles.has(user.role)) {
    return false;
  }
  const permissions = new Set(user.permissions || []);
  if (permissions.has("supplier.segment.manage")) {
    return false;
  }
  return true;
});

const supplierSelfId = computed(() => {
  const raw = authStore.user?.supplierId;
  if (raw === null || raw === undefined) {
    return null;
  }
  const numeric = typeof raw === "number" ? raw : Number(raw);
  return Number.isFinite(numeric) ? Math.trunc(numeric) : null;
});

const currentSupplier = computed(() => supplierStore.selectedSupplier);
const isTempSupplierAccount = computed(() => authStore.user?.role === "temp_supplier");

const supplierWelcomeName = computed(() => authStore.user?.name ?? t("common.user"));
const supplierWorkspaceName = computed(
  () => currentSupplier.value?.companyName || t("dashboard.supplierPanel.defaultWorkspaceName"),
);

const supplierInvitations = ref<SupplierRfqInvitationSummary[]>([]);
const invitationLoading = ref(false);
const invitationsLoadedOnce = ref(false);
const supplierContextReady = ref(false);

const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1000;
const EXPIRY_THRESHOLD_DAYS = 30;

type ExpiringDocumentSummary = {
  id: number;
  label: string;
  daysRemaining: number;
};

const complianceSummary = computed(
  () => currentSupplier.value?.complianceSummary ?? emptyComplianceSummary,
);

const missingProfileFields = computed(() => complianceSummary.value.missingProfileFields ?? []);

const missingDocumentTypes = computed(() => complianceSummary.value.missingDocumentTypes ?? []);

const expiringDocuments = computed<ExpiringDocumentSummary[]>(() => {
  const docs = currentSupplier.value?.documents ?? [];
  const now = Date.now();
  const thresholdMs = EXPIRY_THRESHOLD_DAYS * MILLISECONDS_PER_DAY;

  return docs
    .map((doc) => {
      if (!doc.expiresAt) {
        return null;
      }
      const timestamp = Date.parse(doc.expiresAt);
      if (!Number.isFinite(timestamp)) {
        return null;
      }
      const delta = timestamp - now;
      if (delta > thresholdMs || delta < -thresholdMs) {
        return null;
      }
      const label =
        doc.originalName && doc.originalName.trim().length
          ? doc.originalName
          : doc.docType && doc.docType.trim().length
            ? doc.docType.replace(/_/g, " ")
            : "Supporting document";
      const daysRemaining = Math.ceil(delta / MILLISECONDS_PER_DAY);
      return {
        id: doc.id,
        label,
        daysRemaining,
      };
    })
    .filter((item): item is ExpiringDocumentSummary => item !== null)
    .sort((a, b) => a.daysRemaining - b.daysRemaining);
});

const pendingRfqInvitations = computed(() =>
  supplierInvitations.value
    .filter((invitation) => invitation.needsResponse)
    .slice()
    .sort((a, b) => {
      const aValue = a.daysRemaining ?? Number.POSITIVE_INFINITY;
      const bValue = b.daysRemaining ?? Number.POSITIVE_INFINITY;
      return aValue - bValue;
    }),
);

const totalPendingTasks = computed(
  () =>
    missingDocumentTypes.value.length +
    expiringDocuments.value.length +
    pendingRfqInvitations.value.length +
    missingProfileFields.value.length,
);

const REGISTRATION_STAGE_KEYS = new Set(["", "registration", "potential", "prospective", SupplierStage.TEMPORARY]);

const hasCompletedRegistration = (value: unknown) => {
  if (value === true) return true;
  if (value === 1) return true;
  if (typeof value === "string") {
    const normalized = value.trim().toLowerCase();
    return normalized === "1" || normalized === "true" || normalized === "completed";
  }
  return false;
};

const shouldCheckRegistrationStatus = computed(() => {
  if (isTempSupplierAccount.value) {
    return false;
  }
  const supplier = currentSupplier.value;
  if (!supplier) {
    return false;
  }
  const stageKey = String(supplier.stage ?? "").trim().toLowerCase();
  if (stageKey && !REGISTRATION_STAGE_KEYS.has(stageKey)) {
    return false;
  }
  if (hasCompletedRegistration(supplier.registrationCompleted)) {
    return false;
  }
  return true;
});

const ensureSupplierContext = async (force = false) => {
  if (!isSupplierUser.value || supplierSelfId.value == null) {
    supplierContextReady.value = false;
    return false;
  }
  if (supplierContextReady.value && !force) {
    return true;
  }
  try {
    await supplierStore.fetchSuppliers({}, force);
    if (
      !supplierStore.selectedSupplier ||
      supplierStore.selectedSupplier.id !== supplierSelfId.value
    ) {
      await supplierStore.selectSupplier(supplierSelfId.value);
    }
    supplierContextReady.value = true;
    return true;
  } catch (error) {
    console.error("Failed to prepare supplier context", error);
    return false;
  }
};

const loadSupplierInvitations = async (force = false) => {
  if (!supplierContextReady.value) {
    return;
  }
  if (invitationLoading.value) {
    return;
  }
  if (!force && invitationsLoadedOnce.value) {
    return;
  }
  invitationLoading.value = true;
  try {
    const data = await listSupplierRfqInvitations();
    supplierInvitations.value = data ?? [];
    invitationsLoadedOnce.value = true;
  } catch (error) {
    console.error("Failed to load supplier RFQ invitations", error);
  } finally {
    invitationLoading.value = false;
  }
};

const refreshSupplierTasks = async () => {
  const ready = await ensureSupplierContext(true);
  if (!ready || !currentSupplier.value) {
    return;
  }
  try {
  await supplierStore.refreshDocuments(currentSupplier.value.id);
  } catch (error) {
    console.error("Failed to refresh supplier documents", error);
  }
  await loadSupplierInvitations(true);
  if (shouldCheckRegistrationStatus.value) {
    await loadRegistrationStatus();
  } else {
    registrationStatus.value = null;
  }
};

const loadRegistrationStatus = async () => {
  if (!authStore.user?.supplierId || !shouldCheckRegistrationStatus.value) {
    registrationStatus.value = null;
    return;
  }
  if (registrationStatusLoading.value) {
    return;
  }
  registrationStatusLoading.value = true;
  try {
    registrationStatus.value = await fetchSupplierRegistrationStatusBySupplier(
      authStore.user.supplierId
    );
  } catch (error) {
    console.error("Failed to load registration status:", error);
    registrationStatus.value = null;
  } finally {
    registrationStatusLoading.value = false;
  }
};

const shouldShowRegistrationCard = computed(() => {
  if (!shouldCheckRegistrationStatus.value || !registrationStatus.value) return false;
  const status = registrationStatus.value.status;
  return status === 'pending' || status === 'under_review' || status === 'pending_approval';
});

const registrationStatusLabel = computed(() => {
  if (!registrationStatus.value) return "";
  const status = registrationStatus.value.status;
  const key = `registrationStatus.statuses.${status}`;
  const translation = t(key);
  return translation === key ? status : translation;
});

const registrationStatusTagType = computed(() => {
  const status = registrationStatus.value?.status;
  switch (status) {
    case "approved":
    case "completed":
      return "success";
    case "rejected":
      return "danger";
    case "under_review":
    case "pending":
    case "pending_approval":
    default:
      return "warning";
  }
});

watch(
  [isSupplierUser, supplierSelfId],
  async ([supplierFlag, supplierId], [prevFlag, prevSupplierId]) => {
    if (!supplierFlag || supplierId == null) {
      supplierContextReady.value = false;
      supplierInvitations.value = [];
      invitationsLoadedOnce.value = false;
      return;
    }
    const supplierChanged = prevFlag === undefined || prevSupplierId !== supplierId;
    const ready = await ensureSupplierContext(supplierChanged);
    if (!ready) {
      return;
    }
    if (supplierChanged) {
      invitationsLoadedOnce.value = false;
    }
    await loadSupplierInvitations(supplierChanged);
  },
  { immediate: true },
);

watch(
  shouldCheckRegistrationStatus,
  (needsStatus) => {
    if (needsStatus) {
      void loadRegistrationStatus();
    } else {
      registrationStatus.value = null;
    }
  },
  { immediate: true },
);

const formatDaysCountdown = (days: number | null) => {
  if (days === null || Number.isNaN(days)) {
    return t("dashboard.supplierPanel.countdowns.dueSoon");
  }
  if (days < 0) {
    const overdueDays = Math.abs(days);
    if (overdueDays === 1) {
      return t("dashboard.supplierPanel.countdowns.overdueOne");
    }
    return t("dashboard.supplierPanel.countdowns.overdue", { count: overdueDays });
  }
  if (days === 0) {
    return t("dashboard.supplierPanel.countdowns.dueToday");
  }
  if (days === 1) {
    return t("dashboard.supplierPanel.countdowns.dueInOne");
  }
  return t("dashboard.supplierPanel.countdowns.dueIn", { count: days });
};
</script>

<style scoped>
.supplier-dashboard-panel {
  display: flex;
  flex-direction: column;
  gap: 24px;
  background: #ffffff;
  border-radius: 18px;
  padding: 24px;
  box-shadow: 0 16px 48px rgba(57, 72, 160, 0.12);
  border: 1px solid rgba(138, 146, 255, 0.12);
}

.dashboard-hero {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  background: linear-gradient(135deg, #7c5cff 0%, #5c6cff 100%);
  color: #ffffff;
  border-radius: 16px;
  padding: 28px 32px;
}

.hero-text {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-width: 480px;
}

.hero-kicker {
  margin: 0;
  font-size: 0.95rem;
  opacity: 0.85;
  letter-spacing: 0.02em;
}

.hero-title {
  margin: 0;
  font-size: 1.9rem;
  font-weight: 700;
  line-height: 1.3;
}

.hero-subtitle {
  margin: 0;
  font-size: 1rem;
  opacity: 0.92;
  line-height: 1.6;
}

.hero-actions {
  display: flex;
  align-items: center;
  gap: 24px;
}

.hero-metric {
  display: flex;
  flex-direction: column;
  gap: 4px;
  align-items: flex-start;
}

.metric-number {
  font-size: 2.4rem;
  font-weight: 700;
}

.metric-label {
  font-size: 0.95rem;
  opacity: 0.85;
}

.hero-refresh {
  border: none;
  background: #ffffff;
  color: #4f46e5;
  padding: 10px 18px;
  border-radius: 999px;
  font-weight: 600;
  cursor: pointer;
  transition:
    transform 0.15s ease,
    box-shadow 0.15s ease;
  box-shadow: 0 12px 30px rgba(255, 255, 255, 0.2);
}

.hero-refresh:disabled {
  opacity: 0.7;
  cursor: default;
  box-shadow: none;
}

.hero-refresh:not(:disabled):hover {
  transform: translateY(-1px);
  box-shadow: 0 16px 36px rgba(255, 255, 255, 0.28);
}

.task-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 20px;
}

.task-card {
  background: #f9f9ff;
  border-radius: 16px;
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 14px;
  border: 1px solid rgba(118, 127, 255, 0.14);
  box-shadow: 0 12px 32px rgba(51, 62, 131, 0.12);
}

.task-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.task-card-header h3 {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 600;
  color: #111827;
}

.task-badge {
  background: #eef2ff;
  color: #4338ca;
  padding: 4px 10px;
  border-radius: 999px;
  font-size: 0.85rem;
  font-weight: 600;
}

.task-description {
  margin: 0;
  color: #6b7280;
  font-size: 0.92rem;
}

.task-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.task-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.task-meta {
  font-size: 0.85rem;
  color: #4f46e5;
  font-weight: 600;
  white-space: nowrap;
}

.task-empty {
  color: #6b7280;
  font-size: 0.9rem;
}

.task-extra {
  color: #4338ca;
  font-size: 0.9rem;
  font-weight: 600;
}

@media (max-width: 960px) {
  .dashboard-hero {
    flex-direction: column;
    align-items: flex-start;
  }

  .hero-actions {
    flex-direction: column;
    align-items: flex-start;
    width: 100%;
    gap: 16px;
  }

  .hero-metric {
    align-items: flex-start;
  }
}

@media (max-width: 720px) {
  .task-grid {
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  }
}
</style>
