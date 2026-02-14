<template>
  <div class="unified-dashboard">
    <!-- Welcome Section -->
    <section class="welcome-section">
      <div class="welcome-content">
        <h1>{{ t("dashboard.welcome", { name: userName }) }}</h1>
        <p class="role-info">
          <el-tag :type="roleTagType" size="large">{{ roleLabel }}</el-tag>
        </p>
      </div>
      <div class="welcome-actions">
        <el-button :icon="Refresh" @click="handleRefresh" :loading="loading">
          {{ t("common.refresh") }}
        </el-button>
      </div>
    </section>

    <SupplierDashboardPanel v-if="isSupplierUser" />

    <!-- Todos Section -->
    <section class="todos-section">
      <h2>
        <el-icon><Memo /></el-icon>
        {{ t("dashboard.todos") }}
      </h2>

      <div v-if="loading" class="loading-container">
        <el-skeleton :rows="3" animated />
      </div>

      <el-empty
        v-else-if="todos.length === 0"
        :description="t('dashboard.noTodos')"
        :image-size="120"
      >
        <template #image>
          <el-icon :size="120" color="#909399">
            <CircleCheck />
          </el-icon>
        </template>
      </el-empty>

      <el-row v-else :gutter="20" class="todos-grid">
        <el-col v-for="todo in todos" :key="todo.type" :xs="24" :sm="12" :md="8" :lg="6">
          <TodoCard
            :title="resolveTodoTitle(todo)"
            :count="todo.count"
            :icon="todo.icon"
            :priority="todo.priority"
            @click="handleTodoClick(todo)"
          />
        </el-col>
      </el-row>
    </section>
  </div>
</template>

<script setup lang="ts">




import { computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";

import { Refresh, Memo, CircleCheck } from "@element-plus/icons-vue";
import { useAuthStore } from "@/stores/auth";
import { useTodoItems } from "@/composables/useTodoItems";
import { useSupplierStore } from "@/stores/supplier";
import TodoCard from "@/components/TodoCard.vue";
import SupplierDashboardPanel from "@/components/SupplierDashboardPanel.vue";
import type { TodoItem } from "@/api/dashboard";
import { SupplierStatus } from "@/types";


import { useNotification } from "@/composables";

const notification = useNotification();
const router = useRouter();
const { t } = useI18n();
const authStore = useAuthStore();
const { todos, loading, fetchTodos } = useTodoItems();
const supplierStore = useSupplierStore();

const isSupplierUser = computed(() => {
  const role = authStore.user?.role;
  return role === "temp_supplier" || role === "formal_supplier";
});

const userName = computed(() => authStore.user?.name || t("common.user"));

const ROLE_TRANSLATION_KEYS: Record<string, string> = {
  admin: "role.admin",
  temp_supplier: "role.tempSupplier",
  formal_supplier: "role.formalSupplier",
  purchaser: "role.purchaser",
  procurement_manager: "role.procurementManager",
  procurement_director: "role.procurementDirector",
  quality_manager: "role.qualityManager",
  finance_accountant: "role.financeAccountant",
  finance_director: "role.financeDirector",
  department_user: "role.departmentUser",
};

const roleLabel = computed(() => {
  const role = authStore.user?.role;
  if (!role) return "";

  const key = ROLE_TRANSLATION_KEYS[role];
  if (!key) {
    return role;
  }
  const translated = t(key);
  return translated === key ? role : translated;
});

const ROLE_TAG_TYPES: Record<string, "primary" | "success" | "warning" | "info" | "danger"> = {
  admin: "danger",
  temp_supplier: "info",
  formal_supplier: "success",
  purchaser: "primary",
  procurement_manager: "success",
  procurement_director: "warning",
  quality_manager: "warning",
  finance_accountant: "primary",
  finance_director: "danger",
  department_user: "info",
};

const roleTagType = computed<"primary" | "success" | "warning" | "info" | "danger">(() => {
  const role = authStore.user?.role ?? "";
  return ROLE_TAG_TYPES[role] ?? "info";
});

const resolveTodoTitle = (todo: TodoItem): string => {
  const key = `dashboard.todoItems.${todo.type}`;
  const translated = t(key);
  return translated === key ? todo.title : translated;
};

const handleTodoClick = (todo: TodoItem) => {
  router.push(todo.route);
};

const handleRefresh = async () => {
  try {
    await fetchTodos();
    notification.success(t("common.refreshSuccess"));
  } catch (error) {
    notification.error(t("common.refreshFailed"));
  }
};

const REGISTRATION_STATUSES = new Set<string>([
  SupplierStatus.PENDING_INFO,
  "pending_review", // legacy status value
  SupplierStatus.UNDER_REVIEW,
  SupplierStatus.PENDING_PURCHASER,
  SupplierStatus.PENDING_PURCHASE_MANAGER,
  SupplierStatus.PENDING_FINANCE_MANAGER,
]);

const isTrackingAccount = (user: typeof authStore.user) => {
  if (!user) {
    return false;
  }
  return (
    user.role === "tracking" ||
    user.accountType === "tracking" ||
    user.relatedApplicationId != null ||
    user.tempAccountId != null
  );
};

const hasCompletedRegistration = (value: unknown): boolean => {
  if (value === true || value === 1) {
    return true;
  }
  if (typeof value === "string") {
    const normalized = value.trim().toLowerCase();
    return normalized === "1" || normalized === "true" || normalized === "completed";
  }
  return false;
};

onMounted(async () => {
  const user = authStore.user;

  // Check if user is using a registration tracking account
  // These accounts should only see registration status
  if (isTrackingAccount(user)) {
    router.replace("/registration-status");
    return;
  }

  // Check if supplier user is still in registration/approval phase
  // Even formal supplier code accounts should only see registration status during approval
  if (isSupplierUser.value && user?.supplierId) {
    try {
      // Fetch supplier to check status
      await supplierStore.fetchSuppliers({}, false);
      const supplier = supplierStore.suppliers.find((s) => s.id === user.supplierId);

      if (supplier) {
        const registrationCompleted = hasCompletedRegistration(supplier.registrationCompleted);
        if (!registrationCompleted && REGISTRATION_STATUSES.has(supplier.status)) {
          // Supplier still in approval process - redirect to registration status
          router.replace("/registration-status");
          return;
        }
      }
    } catch (error) {
      console.error("Failed to check supplier status:", error);
    }
  }

  fetchTodos();
});




</script>

<style scoped>
.unified-dashboard {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.welcome-section {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 32px;
  flex-wrap: wrap;
  gap: 16px;
}

.welcome-content h1 {
  margin: 0 0 8px 0;
  font-size: 28px;
  font-weight: 600;
  color: #303133;
}

.role-info {
  margin: 0;
}

.welcome-actions {
  display: flex;
  gap: 12px;
}

.todos-section {
  margin-bottom: 32px;
}

.todos-section h2 {
  margin: 0 0 20px 0;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
  display: flex;
  align-items: center;
  gap: 8px;
}

.loading-container {
  padding: 20px;
}

.todos-grid {
  margin-top: 20px;
}

@media (max-width: 768px) {
  .unified-dashboard {
    padding: 16px;
  }

  .welcome-content h1 {
    font-size: 22px;
  }

  .welcome-section {
    margin-bottom: 24px;
  }

  .todos-section h2 {
    font-size: 18px;
  }
}
</style>



