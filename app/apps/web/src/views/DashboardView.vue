<template>
  <div class="dashboard">
    <section class="hero">
      <div class="hero-text">
        <h1>{{ t("dashboard.welcome", { name: userName }) }}</h1>
        <p class="role-label">{{ t("dashboard.signedInAs", { role: roleLabel }) }}</p>
      </div>
      <div class="hero-actions">
        <button type="button" class="primary" @click="router.push({ name: 'suppliers' })">
          {{ t("dashboard.manageSuppliers") }}
        </button>
        <button type="button" class="ghost" :disabled="combinedLoading" @click="refreshData">
          {{ combinedLoading ? t("common.refreshing") : t("dashboard.refreshData") }}
        </button>
      </div>
    </section>

    <section class="stats">
      <article class="stat-card">
        <span class="stat-label">{{ t("dashboard.stats.totalSuppliers") }}</span>
        <span class="stat-number">{{ statsSummary.totalSuppliers }}</span>
      </article>
      <article class="stat-card">
        <span class="stat-label">{{ t("dashboard.stats.completedProfiles") }}</span>
        <span class="stat-number">{{ statsSummary.completedSuppliers }}</span>
        <span class="stat-subtext">{{
          t("dashboard.stats.nearlyComplete", { count: statsSummary.mostlyCompleteSuppliers })
        }}</span>
      </article>
      <article class="stat-card stat-card--alert">
        <span class="stat-label">{{ t("dashboard.stats.needsAttention") }}</span>
        <span class="stat-number">{{ statsSummary.pendingSuppliers }}</span>
        <span class="stat-subtext">{{ t("dashboard.stats.belowCompleteness") }}</span>
      </article>
      <article class="stat-card stat-card--accent">
        <span class="stat-label">{{ t("dashboard.stats.completionRate") }}</span>
        <span class="stat-number">{{ statsSummary.completionRate }}%</span>
        <span class="stat-subtext">{{
          t("dashboard.stats.averageCompleteness", { percent: statsSummary.averageCompletion })
        }}</span>
      </article>
      <article v-if="showReminderSummaryCard" class="stat-card stat-card--contracts">
        <span class="stat-label">{{ t("dashboard.stats.expiringContracts") }}</span>
        <span class="stat-number">{{ totalExpiringSuppliers }}</span>
        <ul
          v-if="!contractReminderSummaryLoading && contractReminderSnapshot.length"
          class="contract-reminder-list"
        >
          <li
            v-for="item in contractReminderSnapshot"
            :key="item.key"
            class="contract-reminder-item"
          >
            <span class="contract-reminder-label">{{ item.label }}</span>
            <span class="contract-reminder-count">{{ item.supplierCount }}</span>
          </li>
        </ul>
        <p v-else-if="contractReminderSummaryLoading" class="stat-subtext">
          {{ t("dashboard.contractReminders.loading") }}
        </p>
        <p v-else class="stat-subtext">{{ t("dashboard.contractReminders.noReminders") }}</p>
      </article>
      <article v-if="changeRequests.length" class="stat-card stat-card--secondary">
        <span class="stat-label">{{ t("dashboard.stats.openChangeRequests") }}</span>
        <span class="stat-number">{{ changeRequests.length }}</span>
      </article>
    </section>

    <section class="panel">
      <h2 class="panel-title">{{ t("dashboard.quickLinks.title") }}</h2>
      <div class="quick-links">
        <router-link class="quick-link" :to="{ name: 'approvals' }">{{
          t("dashboard.quickLinks.approvalQueue")
        }}</router-link>
        <router-link class="quick-link" :to="{ name: 'suppliers', query: { view: 'documents' } }">
          {{ t("dashboard.quickLinks.supplierDocuments") }}
        </router-link>
        <router-link v-if="isAdmin" class="quick-link" :to="{ name: 'admin-supplier-import' }">
          {{ t("dashboard.quickLinks.supplierImport") }}
        </router-link>
        <router-link v-if="isAdmin" class="quick-link" :to="{ name: 'admin-permissions' }">
          {{ t("dashboard.quickLinks.rolePermissions") }}
        </router-link>
      </div>
    </section>

    <SystemModulesPanel />
    <RolePermissionsTable />

    <section class="panel">
      <h2 class="panel-title">{{ t("dashboard.recentSuppliers.title") }}</h2>
      <div v-if="recentSuppliers.length === 0" class="empty-state">
        {{ t("dashboard.recentSuppliers.empty") }}
      </div>
      <ul v-else class="recent-list">
        <li v-for="supplier in recentSuppliers" :key="supplier.id" class="recent-item">
          <div class="recent-main">
            <span class="supplier-name">{{ supplier.companyName }}</span>
            <span class="supplier-meta"
              >{{ supplier.contactPerson }} - {{ supplier.contactEmail }}</span
            >
          </div>
          <span :class="['status-tag', getStatusClass(supplier.status)]">
            {{ getStatusText(supplier.status) }}
          </span>
        </li>
      </ul>
    </section>

    <section v-if="changeRequests.length" class="panel">
      <h2 class="panel-title">{{ t("dashboard.changeRequests.title") }}</h2>
      <ul class="recent-list">
        <li v-for="request in changeRequests.slice(0, 5)" :key="request.id" class="recent-item">
          <div class="recent-main">
            <span class="supplier-name">{{ t("supplier.title") }} #{{ request.supplierId }}</span>
            <span class="supplier-meta"
              >{{ request.status }} - {{ formatDate(request.submittedAt) }}</span
            >
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>

<script setup lang="ts">




import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";

import { storeToRefs } from "pinia";
import { useAuthStore } from "@/stores/auth";
import { useSupplierStore } from "@/stores/supplier";
import { useContractReminderStore } from "@/stores/contractReminders";
import { getStatusText } from "@/utils/helpers";
import SystemModulesPanel from "@/components/SystemModulesPanel.vue";
import RolePermissionsTable from "@/components/RolePermissionsTable.vue";


import { useNotification } from "@/composables";

const notification = useNotification();
defineOptions({ name: "DashboardView" });

const { t } = useI18n();

const router = useRouter();
const authStore = useAuthStore();
const supplierStore = useSupplierStore();
const contractReminderStore = useContractReminderStore();

const { suppliers, pendingSuppliers, approvedSuppliers, stats, loading, changeRequests } =
  storeToRefs(supplierStore);
const { summary: contractReminderSummary, loadingSummary: contractReminderSummaryLoading } =
  storeToRefs(contractReminderStore);

const statsLoading = ref(false);
const combinedLoading = computed(() => loading.value || statsLoading.value);
const statsSummary = computed(() => {
  const snapshot = stats.value;
  if (snapshot) {
    return snapshot;
  }
  const total = suppliers.value.length;
  const completed = approvedSuppliers.value.length;
  const pending = Math.max(total - completed, 0);
  return {
    totalSuppliers: total,
    completedSuppliers: completed,
    mostlyCompleteSuppliers: 0,
    pendingSuppliers: pending,
    completionRate: total ? Math.round((completed / total) * 100) : 0,
    averageCompletion: total ? Math.round((completed / total) * 100) : 0,
    completionBuckets: {
      complete: completed,
      mostly_complete: 0,
      needs_attention: pending,
    },
    requirementCatalog: { documents: [], profileFields: [] },
  };
});

const userName = computed(() => authStore.user?.name ?? "Guest");
const roleLabel = computed(() => authStore.roleDisplayName);
const isSupplierPortalUser = computed(() => {
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

const isAdmin = computed(() => authStore.user?.role === "admin");

const recentSuppliers = computed(() => suppliers.value.slice(0, 5));
const showReminderSummaryCard = computed(() => !isSupplierPortalUser.value);

const formatReminderBucketLabel = (bucketKey: string, leadDays?: number | null) => {
  if (bucketKey === "expired") {
    return t("dashboard.contractReminders.expired");
  }
  if (bucketKey && bucketKey.startsWith("within_") && leadDays) {
    return t("dashboard.contractReminders.withinDays", { days: leadDays });
  }
  return t("dashboard.contractReminders.upcoming");
};

const contractReminderSnapshot = computed(() => {
  const summary = contractReminderSummary.value;
  if (!summary) {
    return [] as Array<{ key: string; label: string; supplierCount: number }>;
  }
  const items = summary.buckets.map((bucket) => ({
    key: bucket.key,
    label: formatReminderBucketLabel(bucket.key, bucket.leadDays ?? null),
    supplierCount: bucket.supplierCount,
  }));
  items.unshift({
    key: "expired",
    label: "Expired",
    supplierCount: summary.expired.supplierCount,
  });
  return items;
});

const totalExpiringSuppliers = computed(() => {
  if (contractReminderSummaryLoading.value && !contractReminderSummary.value) {
    return "--";
  }
  const total = contractReminderSnapshot.value.reduce(
    (count, item) => count + item.supplierCount,
    0,
  );
  return String(total);
});

const getStatusClass = (status?: string | null) => {
  if (!status) return "status-info";
  if (status === "approved" || status === "qualified") return "status-success";
  if (status === "rejected" || status === "disqualified" || status === "terminated")
    return "status-danger";
  if (status.includes("pending")) return "status-warning";
  return "status-info";
};

const formatDate = (input?: string | null) => {
  if (!input) return "N/A";
  const date = new Date(input);
  return Number.isNaN(date.getTime()) ? input : date.toLocaleDateString();
};

const refreshData = async () => {
  await supplierStore.fetchSuppliers({}, true);
  if (!isSupplierPortalUser.value && typeof supplierStore.fetchChangeRequests === "function") {
    await supplierStore.fetchChangeRequests();
  }
  notification.success(t("dashboard.notifications.refreshSuccess"));
};

onMounted(async () => {
  if (!suppliers.value.length) {
    await supplierStore.fetchSuppliers();
  }
  if (!isSupplierPortalUser.value && typeof supplierStore.fetchChangeRequests === "function") {
    await supplierStore.fetchChangeRequests();
  }
});




</script>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1.5rem;
  max-width: 1200px;
  margin: 0 auto;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}

.hero {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: linear-gradient(135deg, #4f46e5 0%, #6366f1 100%);
  color: #fff;
  padding: 1.5rem;
  border-radius: 1rem;
  box-shadow: 0 18px 45px rgba(79, 70, 229, 0.25);
}

.hero-text h1 {
  margin: 0 0 0.35rem 0;
  font-size: 1.8rem;
  font-weight: 700;
}

.role-label {
  margin: 0;
  opacity: 0.85;
}

.hero-actions {
  display: flex;
  gap: 0.75rem;
}

.primary,
.ghost {
  padding: 0.6rem 1.2rem;
  border-radius: 999px;
  font-weight: 600;
  cursor: pointer;
  border: none;
  transition:
    transform 0.2s ease,
    box-shadow 0.2s ease;
}

.primary {
  background: #f97316;
  color: #fff;
}

.primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 12px 24px rgba(249, 115, 22, 0.3);
}

.ghost {
  background: rgba(255, 255, 255, 0.15);
  color: #fff;
}

.ghost:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.stat-card {
  background: #fff;
  border-radius: 1rem;
  padding: 1.25rem;
  box-shadow: 0 12px 32px rgba(15, 23, 42, 0.12);
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.stat-number {
  font-size: 1.8rem;
  font-weight: 700;
  color: #111827;
}

.stat-label {
  color: #6b7280;
  font-size: 0.9rem;
}

.stat-subtext {
  font-size: 0.8rem;
  color: inherit;
  opacity: 0.75;
}

.stat-card--alert {
  border-left-color: #f97316;
  background: #fff7ed;
  color: #9a3412;
}

.stat-card--accent {
  border-left-color: #0ea5e9;
  background: #f0f9ff;
  color: #0369a1;
}

.stat-card--contracts {
  border-left-color: #f97316;
  background: #fff7ed;
  color: #9a3412;
}

.stat-card--secondary {
  border-left-color: #6366f1;
}

.panel {
  background: #fff;
  border-radius: 1rem;
  box-shadow: 0 12px 32px rgba(15, 23, 42, 0.08);
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.panel-title {
  margin: 0;
  font-size: 1.2rem;
  font-weight: 600;
  color: #111827;
}

.quick-links {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.quick-link {
  padding: 0.5rem 1rem;
  border-radius: 999px;
  background: #eef2ff;
  color: #3730a3;
  text-decoration: none;
  font-weight: 600;
}

.quick-link:hover {
  background: #c7d2fe;
}

.empty-state {
  padding: 1.5rem;
  text-align: center;
  color: #6b7280;
  background: #f9fafb;
  border-radius: 0.75rem;
}

.recent-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.recent-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.recent-main {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.supplier-name {
  font-weight: 600;
  color: #111827;
}

.supplier-meta {
  color: #6b7280;
  font-size: 0.85rem;
}

.status-tag {
  padding: 0.35rem 0.75rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: capitalize;
}

.status-success {
  background: #dcfce7;
  color: #166534;
}

.status-danger {
  background: #fee2e2;
  color: #b91c1c;
}

.status-warning {
  background: #fef3c7;
  color: #92400e;
}

.status-info {
  background: #e0f2fe;
  color: #0369a1;
}

@media (max-width: 768px) {
  .hero {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .hero-actions {
    width: 100%;
    justify-content: flex-start;
  }
}

.contract-reminder-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 0.35rem;
}

.contract-reminder-item {
  display: flex;
  justify-content: space-between;
  font-size: 0.85rem;
  color: inherit;
}

.contract-reminder-label {
  font-weight: 500;
}

.contract-reminder-count {
  font-weight: 600;
}
</style>
