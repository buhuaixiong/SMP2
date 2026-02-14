<template>
  <div class="sidebar-wrapper">
    <aside class="sidebar" :class="{ collapsed }">
      <div class="sidebar-toggle">
        <el-button :icon="collapsed ? Expand : Fold" circle @click="handleToggle" size="small" />
      </div>

      <el-menu
        :default-active="activeRoute"
        :collapse="collapsed"
        :collapse-transition="false"
        class="sidebar-menu"
        @select="handleMenuSelect"
      >
        <el-menu-item v-for="item in menuItems" :key="item.path" :index="item.path">
          <el-icon><component :is="item.icon" /></el-icon>
          <template #title>
            <span>{{ t(item.label) }}</span>
          </template>
        </el-menu-item>
      </el-menu>
    </aside>

    <!-- Mobile overlay -->
    <div v-if="!collapsed && isMobile" class="sidebar-overlay" @click="handleToggle" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from "vue";
import { useRouter, useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/stores/auth";
import { Fold, Expand } from "@element-plus/icons-vue";
import {
  HomeFilled,
  Memo,
  Document,
  FolderOpened,
  Money,
  Clock,
  Setting,
  User,
  Management,
  Tickets,
  OfficeBuilding,
  Connection,
  Operation,
  Edit,
  Upload,
  TrendCharts,
  Checked,
  CircleCheck,
  EditPen,
  Files,
  Lock,
} from "@element-plus/icons-vue";
import { useSupplierStore } from "@/stores/supplier";

interface MenuItem {
  path: string;
  label: string;
  icon: any;
}

interface Props {
  collapsed: boolean;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "toggle", collapsed: boolean): void;
}>();

const router = useRouter();
const route = useRoute();
const { t } = useI18n();
const authStore = useAuthStore();
const supplierStore = useSupplierStore();
const { suppliers: supplierList, selectedSupplier, hasLoaded } = storeToRefs(supplierStore);

const isMobile = ref(false);

const activeRoute = computed(() => route.path);

const supplierRegistrationStatuses = new Set([
  "pending_info",
  "pending_review",
  "under_review",
  "pending_purchaser",
  "pending_purchase_manager",
  "pending_finance_manager",
]);

const currentSupplier = computed(() => {
  const supplierId = authStore.user?.supplierId;
  if (!supplierId) {
    return null;
  }
  const numericId = Number(supplierId);
  if (!Number.isFinite(numericId) || numericId <= 0) {
    return null;
  }
  if (selectedSupplier.value && selectedSupplier.value.id === numericId) {
    return selectedSupplier.value;
  }
  return supplierList.value.find((supplier) => supplier.id === numericId) ?? null;
});

const enforceRegistrationOnly = computed(() => {
  const role = authStore.user?.role;
  if (role !== "temp_supplier" && role !== "formal_supplier") {
    return false;
  }
  const supplier = currentSupplier.value;
  if (!supplier) {
    // Until supplier data is loaded, default to restricted nav to avoid exposing features
    return true;
  }
  return supplierRegistrationStatuses.has(supplier.status ?? "");
});

const menuItems = computed<MenuItem[]>(() => {
  const role = authStore.user?.role;
  const accountType = authStore.user?.accountType;

  if (role === "tracking" || accountType === "tracking") {
    return [
      { path: "/registration-status", label: "sidebar.registrationStatus", icon: Checked },
    ];
  }

  if (enforceRegistrationOnly.value) {
    return [
      { path: "/registration-status", label: "sidebar.registrationStatus", icon: Checked },
    ];
  }

  // Check if supplier is in registration workflow (has relatedApplicationId or is in registration status)
  const supplier = currentSupplier.value;
  const isInRegistrationFlow = supplier && (
    supplier.relatedApplicationId ||
    supplierRegistrationStatuses.has(supplier.status ?? "")
  );

  if (role === "temp_supplier") {
    const items = [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
    ];
    // Only show registration status if supplier is actively in registration process
    if (isInRegistrationFlow) {
      items.push({ path: "/registration-status", label: "sidebar.registrationStatus", icon: Checked });
    }
    items.push(
      { path: "/rfq", label: "sidebar.rfq", icon: Memo },
      { path: "/supplier/profile", label: "sidebar.profile", icon: User },
      { path: "/supplier/upgrade", label: "sidebar.upgrade", icon: TrendCharts },
      { path: "/supplier/change-requests", label: "sidebar.changeRequests", icon: EditPen },
      { path: "/supplier/file-uploads", label: "sidebar.fileUploads", icon: Files },
    );
    return items;
  }

  if (role === "formal_supplier") {
    const items = [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
    ];
    // Only show registration status if supplier is actively in registration process
    if (isInRegistrationFlow) {
      items.push({ path: "/registration-status", label: "sidebar.registrationStatus", icon: Checked });
    }
    items.push(
      { path: "/rfq", label: "sidebar.rfq", icon: Memo },
      { path: "/supplier/profile", label: "sidebar.profile", icon: User },
      { path: "/supplier/change-requests", label: "sidebar.changeRequests", icon: EditPen },
      { path: "/supplier/file-uploads", label: "sidebar.fileUploads", icon: Files },
      { path: "/reconciliation/supplier", label: "sidebar.reconciliation", icon: Money },
      { path: "/templates", label: "sidebar.templates", icon: Document },
    );
    return items;
  }

  if (role === "purchaser") {
    const items: MenuItem[] = [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
      { path: "/rfq", label: "sidebar.rfq", icon: Memo },
      { path: "/approvals", label: "sidebar.registrationApprovals", icon: Checked },
      { path: "/approval/upgrades", label: "sidebar.upgradeApproval", icon: TrendCharts },
      { path: "/approval/supplier-changes", label: "sidebar.changeApproval", icon: EditPen },
      { path: "/approval/file-uploads", label: "sidebar.fileApproval", icon: Files },
      { path: "/suppliers", label: "sidebar.suppliers", icon: User },
    ];

    if (authStore.isPurchasingGroupLeader) {
      items.splice(2, 0, { path: "/purchasing-groups", label: "sidebar.purchasingGroups", icon: Connection });
    }

    return items;
  }

  if (role === "procurement_manager") {
    return [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
      { path: "/purchasing-groups", label: "sidebar.purchasingGroups", icon: Connection },
      { path: "/tags", label: "sidebar.tags", icon: Tickets },
      { path: "/rfq", label: "sidebar.rfq", icon: Memo },
      { path: "/approvals", label: "sidebar.registrationApprovals", icon: Checked },
      { path: "/approval/upgrades", label: "sidebar.upgradeApproval", icon: TrendCharts },
      { path: "/approval/supplier-changes", label: "sidebar.changeApproval", icon: EditPen },
      { path: "/approval/file-uploads", label: "sidebar.fileApproval", icon: Files },
      { path: "/suppliers", label: "sidebar.suppliers", icon: User },
    ];
  }

  if (role === "quality_manager") {
    return [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
      { path: "/approvals", label: "sidebar.registrationApprovals", icon: Checked },
      { path: "/approval/upgrades", label: "sidebar.upgradeApproval", icon: TrendCharts },
      { path: "/approval/supplier-changes", label: "sidebar.changeApproval", icon: EditPen },
      { path: "/approval/file-uploads", label: "sidebar.fileApproval", icon: Files },
      { path: "/suppliers", label: "sidebar.suppliers", icon: User },
    ];
  }

  if (role === "procurement_director") {
    return [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
      { path: "/rfq", label: "sidebar.rfq", icon: Memo },
      { path: "/whitelist-blacklist", label: "sidebar.whitelistBlacklist", icon: User },
      { path: "/approvals", label: "sidebar.registrationApprovals", icon: Checked },
      { path: "/approval/upgrades", label: "sidebar.upgradeApproval", icon: TrendCharts },
      { path: "/approval/supplier-changes", label: "sidebar.changeApproval", icon: EditPen },
      { path: "/approval/file-uploads", label: "sidebar.fileApproval", icon: Files },
    ];
  }

  if (role === "finance_accountant") {
    return [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
      { path: "/approvals", label: "sidebar.registrationApprovals", icon: Checked },
      { path: "/reconciliation/accountant", label: "sidebar.reconciliation", icon: Money },
      { path: "/invoices", label: "sidebar.invoices", icon: Document },
    ];
  }
  if (role === "finance_cashier") {
    return [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
      { path: "/approvals", label: "sidebar.registrationApprovals", icon: Checked },
    ];
  }

  if (role === "finance_director") {
    return [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
      { path: "/approvals", label: "sidebar.registrationApprovals", icon: Checked },
      { path: "/reconciliation/accountant", label: "sidebar.reconciliation", icon: Money },
      { path: "/invoices", label: "sidebar.invoices", icon: Document },
      { path: "/approval/upgrades", label: "sidebar.upgradeApproval", icon: TrendCharts },
      { path: "/approval/supplier-changes", label: "sidebar.changeApproval", icon: EditPen },
      { path: "/approval/file-uploads", label: "sidebar.fileApproval", icon: Files },
    ];
  }

  if (role === "admin") {
    return [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
      { path: "/suppliers", label: "sidebar.suppliers", icon: User },
      { path: "/admin/supplier-import", label: "sidebar.supplierImport", icon: Upload },
      {
        path: "/admin/bulk-document-import",
        label: "sidebar.bulkDocumentImport",
        icon: FolderOpened,
      },
      { path: "/org-units", label: "sidebar.orgUnits", icon: Connection },
      { path: "/tags", label: "sidebar.tags", icon: Tickets },
      { path: "/admin/permissions", label: "sidebar.permissions", icon: Setting },
      { path: "/admin/email-settings", label: "sidebar.emailSettings", icon: Setting },
      { path: "/admin/exchange-rates", label: "sidebar.exchangeRates", icon: Money },
      { path: "/admin/audit-log", label: "sidebar.auditLog", icon: Document },
      { path: "/admin/emergency-lockdown", label: "sidebar.emergencyLockdown", icon: Lock },
      { path: "/admin/templates", label: "sidebar.templates", icon: Files },
    ];
  }

  if (role === "department_user") {
    return [
      { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
    ];
  }

  return [{ path: "/dashboard", label: "sidebar.home", icon: HomeFilled }];
});

const handleToggle = () => {
  emit("toggle", !props.collapsed);
};

const handleMenuSelect = (path: string) => {
  router.push(path);

  // Auto-collapse on mobile after selection
  if (isMobile.value && !props.collapsed) {
    emit("toggle", true);
  }
};

const checkMobile = () => {
  isMobile.value = window.innerWidth <= 768;
  if (isMobile.value && !props.collapsed) {
    emit("toggle", true);
  }
};

onMounted(() => {
  checkMobile();
  window.addEventListener("resize", checkMobile);

  const role = authStore.user?.role;
  if (
    (role === "temp_supplier" || role === "formal_supplier") &&
    !hasLoaded.value
  ) {
    supplierStore.fetchSuppliers({}, false).catch((error) => {
      console.error("[Sidebar] Failed to load supplier context for navigation", error);
    });
  }
});

onUnmounted(() => {
  window.removeEventListener("resize", checkMobile);
});
</script>

<style scoped>
.sidebar-wrapper {
  position: relative;
}

.sidebar {
  width: 200px;
  height: 100%;
  background-color: #001529;
  transition: width 0.3s ease;
  overflow-x: hidden;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}

.sidebar.collapsed {
  width: 64px;
}

.sidebar-toggle {
  padding: 16px;
  display: flex;
  justify-content: flex-end;
}

.sidebar-menu {
  border-right: none;
  background-color: transparent;
  flex: 1;
}

.sidebar-menu :deep(.el-menu-item) {
  color: rgba(255, 255, 255, 0.65);
  background-color: transparent;
}

.sidebar-menu :deep(.el-menu-item:hover) {
  color: #fff;
  background-color: rgba(255, 255, 255, 0.08);
}

.sidebar-menu :deep(.el-menu-item.is-active) {
  color: #fff;
  background-color: #1890ff;
}

.sidebar-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  z-index: 999;
}

@media (max-width: 768px) {
  .sidebar {
    position: fixed;
    top: 60px;
    left: 0;
    bottom: 0;
    z-index: 1000;
    transform: translateX(0);
    width: 200px;
  }

  .sidebar.collapsed {
    transform: translateX(-100%);
    width: 200px;
  }
}
</style>
