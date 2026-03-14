import { createRouter, createWebHistory } from "vue-router";
import { useAuthStore } from "@/stores/auth";
import { resolveForcedPasswordChangeNavigation } from "@/utils/authPasswordChangeRouting";

const ROLE_HOME_ROUTE_NAME = "dashboard";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/login",
      name: "login",
      component: () => import("@/views/LoginView.vue"),
    },
    {
      path: "/supplier-registration",
      name: "supplier-registration",
      component: () => import("@/views/SupplierRegistrationView.vue"),
    },
    {
      path: "/activate",
      name: "account-activation",
      component: () => import("@/views/AccountActivationView.vue"),
      meta: { requiresAuth: false },
    },
    {
      path: "/registration-status/:applicationId?",
      name: "registration-status",
      component: () => import("@/views/RegistrationStatusView.vue"),
      meta: {
        requiresAuth: true,
        title: "Registration Status",
      },
    },
    {
      path: "/rfq-invitation/:token",
      name: "rfq-invitation",
      component: () => import("@/views/RfqInvitationView.vue"),
      meta: { requiresAuth: false },
    },
    {
      path: "/auto-login",
      name: "auto-login",
      component: () => import("@/views/AutoLoginView.vue"),
      meta: { requiresAuth: false },
    },
    {
      path: "/change-password-required",
      name: "change-password-required",
      component: () => import("@/views/ChangePasswordRequiredView.vue"),
      meta: { requiresAuth: true },
    },
    {
      path: "/",
      redirect: "/dashboard",
    },
    {
      path: "/dashboard",
      name: ROLE_HOME_ROUTE_NAME,
      component: () => import("@/views/UnifiedDashboard.vue"),
      meta: { requiresAuth: true },
    },
    {
      path: "/suppliers",
      name: "suppliers",
      component: () => import("@/views/SupplierDirectoryView.vue"),
      meta: {
        requiresAuth: true,
        roles: [
          "purchaser",
          "procurement_manager",
          "quality_manager",
          "temp_supplier",
          "formal_supplier",
        ],
      },
    },
    {
      path: "/item-master",
      name: "item-master",
      component: () => import("@/views/ItemMasterView.vue"),
      meta: {
        requiresAuth: true,
        permissions: ["item_master.view.own", "item_master.view.all"],
      },
    },
    {
      path: "/approvals",
      name: "approvals",
      component: () => import("@/views/ApprovalQueueView.vue"),
      meta: {
        requiresAuth: true,
        roles: [
          "quality_manager",
          "purchaser",
          "procurement_manager",
          "procurement_director",
          "finance_director",
          "finance_accountant",
          "finance_cashier",
        ],
      },
    },
    {
      path: "/admin/supplier-import",
      name: "admin-supplier-import",
      component: () => import("@/views/AdminSupplierImportView.vue"),
      meta: { requiresAuth: true, roles: ["admin"] },
    },
    {
      path: "/admin/bulk-document-import",
      name: "admin-bulk-document-import",
      component: () => import("@/views/AdminBulkDocumentImportView.vue"),
      meta: { requiresAuth: true, roles: ["admin"] },
    },
    {
      path: "/admin/permissions",
      name: "admin-permissions",
      component: () => import("@/views/AdminPermissionsView.vue"),
      meta: { requiresAuth: true, roles: ["admin"] },
    },
    {
      path: "/admin/templates",
      name: "admin-templates",
      component: () => import("@/views/AdminTemplateLibraryView.vue"),
      meta: { requiresAuth: true, roles: ["admin"] },
    },
    {
      path: "/admin/audit-log",
      name: "admin-audit-log",
      component: () => import("@/views/AdminAuditLogView.vue"),
      meta: { requiresAuth: true, roles: ["admin"] },
    },
    {
      path: "/admin/emergency-lockdown",
      name: "admin-emergency-lockdown",
      component: () => import("@/views/AdminEmergencyLockdownView.vue"),
      meta: { requiresAuth: true, roles: ["admin"] },
    },
    {
      path: "/admin/email-settings",
      name: "admin-email-settings",
      component: () => import("@/views/EmailSettingsView.vue"),
      meta: { requiresAuth: true, roles: ["admin"] },
    },
    {
      path: "/admin/exchange-rates",
      name: "admin-exchange-rates",
      component: () => import("@/views/AdminExchangeRateManagementView.vue"),
      meta: { requiresAuth: true, roles: ["admin"], title: "Exchange Rate Management" },
    },
    {
      path: "/purchasing-groups",
      name: "purchasing-groups",
      component: () => import("@/views/PurchasingGroupsView.vue"),
      meta: { requiresAuth: true, roles: ["admin", "procurement_manager"], allowGroupLeader: true },
    },
    {
      path: "/tags",
      name: "tag-management",
      component: () => import("@/views/TagManagementView.vue"),
      meta: { requiresAuth: true, roles: ["admin", "procurement_manager"] },
    },
    {
      path: "/org-units",
      name: "organizational-units",
      component: () => import("@/views/OrganizationalUnitsView.vue"),
      meta: { requiresAuth: true, permissions: ["admin.org_units.manage"] },
    },
    {
      path: "/rfq",
      name: "rfq-management",
      component: () => import("@/views/RfqManagementView.vue"),
      meta: {
        requiresAuth: true,
        roles: [
          "purchaser",
          "procurement_manager",
          "procurement_director",
          "temp_supplier",
          "formal_supplier",
          "supplier",
        ],
      },
    },
    {
      path: "/rfq/create",
      name: "rfq-create",
      component: () => import("@/views/RfqCreateView.vue"),
      meta: { requiresAuth: true, roles: ["purchaser"] },
    },
    {
      path: "/rfq/:id",
      name: "rfq-detail",
      component: () => import("@/views/RfqDetailView.vue"),
      meta: { requiresAuth: true },
    },
    {
      path: "/rfq/:id/edit",
      name: "rfq-edit",
      component: () => import("@/views/RfqCreateView.vue"),
      meta: { requiresAuth: true, roles: ["purchaser"] },
    },
    // Reconciliation Routes
    {
      path: "/reconciliation/supplier",
      name: "supplier-reconciliation",
      component: () => import("@/views/SupplierReconciliationView.vue"),
      meta: { requiresAuth: true, roles: ["formal_supplier"], title: "Supplier Reconciliation" },
    },
    {
      path: "/reconciliation/accountant",
      name: "accountant-reconciliation",
      component: () => import("@/views/AccountantReconciliationDashboard.vue"),
      meta: {
        requiresAuth: true,
        roles: ["finance_accountant", "finance_director"],
        title: "Reconciliation Management",
      },
    },
    // Invoice Management Routes
    {
      path: "/invoices",
      name: "invoice-management",
      component: () => import("@/views/FinanceAccountantInvoiceView.vue"),
      meta: {
        requiresAuth: true,
        roles: ["finance_accountant", "finance_director", "admin"],
        title: "Invoice Management",
      },
    },
    // Whitelist & Blacklist Management Routes
    {
      path: "/whitelist-blacklist",
      name: "whitelist-blacklist",
      component: () => import("@/views/SupplierWhitelistBlacklistView.vue"),
      meta: {
        requiresAuth: true,
        roles: ["procurement_director", "admin"],
        title: "Whitelist & Blacklist Management",
      },
    },
    {
      path: "/supplier/profile",
      name: "supplier-profile",
      component: () => import("@/views/SupplierProfileView.vue"),
      meta: {
        requiresAuth: true,
        roles: ["temp_supplier", "formal_supplier"],
        title: "My Company Profile",
      },
    },
    // Supplier Change Request Routes
    {
      path: "/supplier/change-requests",
      name: "supplier-change-requests",
      component: () => import("@/views/SupplierChangeRequestsView.vue"),
      meta: {
        requiresAuth: true,
        roles: ["temp_supplier", "formal_supplier"],
        title: "My Change Requests",
      },
    },
    {
      path: "/approval/supplier-changes",
      name: "supplier-change-approvals",
      component: () => import("@/views/SupplierChangeApprovalView.vue"),
      meta: {
        requiresAuth: true,
        roles: [
          "purchaser",
          "quality_manager",
          "procurement_manager",
          "procurement_director",
          "finance_director",
        ],
        title: "Supplier Change Approvals",
      },
    },
    // File Upload Approval Routes
    {
      path: "/supplier/file-uploads",
      name: "supplier-file-uploads",
      component: () => import("@/views/SupplierFileUploadsView.vue"),
      meta: {
        requiresAuth: true,
        roles: ["temp_supplier", "formal_supplier"],
        title: "File Upload Management",
      },
    },
    {
      path: "/approval/file-uploads",
      name: "file-upload-approvals",
      component: () => import("@/views/FileUploadApprovalView.vue"),
      meta: {
        requiresAuth: true,
        roles: [
          "purchaser",
          "quality_manager",
          "procurement_manager",
          "procurement_director",
          "finance_director",
        ],
        title: "File Upload Approvals",
      },
    },
    // Supplier Upgrade Routes
    {
      path: "/supplier/upgrade",
      name: "supplier-upgrade",
      component: () => import("@/views/UpgradeManagementView.vue"),
      meta: {
        requiresAuth: true,
        roles: ["temp_supplier"],
        title: "Transfer to Official Supplier",
      },
    },
    {
      path: "/approval/upgrades",
      name: "upgrade-approvals",
      component: () => import("@/views/ApprovalDashboardView.vue"),
      meta: {
        requiresAuth: true,
        roles: [
          "purchaser",
          "quality_manager",
          "procurement_manager",
          "procurement_director",
          "finance_director",
        ],
        title: "Upgrade Approvals",
      },
    },
  ],
});

router.beforeEach(async (to) => {
  const authStore = useAuthStore();

  if (authStore.isAuthenticated && !authStore.user) {
    try {
      await authStore.fetchMe();
    } catch {
      await authStore.logout();
      return { name: "login", query: { redirect: to.fullPath } };
    }
  }

  const passwordChangeNavigation = resolveForcedPasswordChangeNavigation({
    toName: typeof to.name === "string" ? to.name : null,
    toFullPath: to.fullPath,
    isAuthenticated: authStore.isAuthenticated,
    user: authStore.user,
    roleHomeRouteName: ROLE_HOME_ROUTE_NAME,
  });

  if (passwordChangeNavigation) {
    return passwordChangeNavigation;
  }

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return { name: "login", query: { redirect: to.fullPath } };
  }

  const allowedRoles = (to.meta as { roles?: string[]; allowGroupLeader?: boolean }).roles;
  const allowGroupLeader = (to.meta as { allowGroupLeader?: boolean }).allowGroupLeader;
  if (allowedRoles && allowedRoles.length > 0) {
    const userRole = authStore.user?.role;
    const isGroupLeader = allowGroupLeader && authStore.isPurchasingGroupLeader;
    if ((!userRole || !allowedRoles.includes(userRole)) && !isGroupLeader) {
      return { name: ROLE_HOME_ROUTE_NAME };
    }
  }

  const requiredPermissions = (to.meta as { permissions?: string[] }).permissions;
  if (requiredPermissions && requiredPermissions.length > 0) {
    let userPermissions = authStore.user?.permissions ?? [];
    let hasPermission = requiredPermissions.some((permission) =>
      userPermissions.includes(permission),
    );

    if (!hasPermission) {
      try {
        await authStore.fetchMe();
      } catch {
        // Ignore refresh failure and fall back to local permissions.
      }

      userPermissions = authStore.user?.permissions ?? [];
      hasPermission = requiredPermissions.some((permission) =>
        userPermissions.includes(permission),
      );
    }

    if (!hasPermission) {
      return { name: ROLE_HOME_ROUTE_NAME };
    }
  }

  return true;
});

router.onError((error, to) => {
  const message = error instanceof Error ? error.message : String(error ?? "");
  const chunkLoadFailed =
    /ChunkLoadError|Loading chunk .* failed|Failed to fetch dynamically imported module|CSS chunk load failed/i.test(
      message,
    );
  if (!chunkLoadFailed || typeof window === "undefined") {
    return;
  }

  console.warn("[router] Recovering from failed chunk load, forcing hard navigation", error);
  if (to?.fullPath) {
    window.location.assign(to.fullPath);
  } else {
    window.location.reload();
  }
});

export default router;
