<template>
  <header class="app-header">
    <div class="header-left">
      <button class="back-btn" type="button" @click="goBack">
        {{ t("header.back") }}
      </button>
      <span class="brand-title">{{ t("common.appName") }}</span>
    </div>
    <div v-if="authStore.isAuthenticated" class="header-right">
      <div class="user-meta">
        <span class="user-name">{{ userName }}</span>
        <span class="user-role">{{ roleLabel }}</span>
      </div>
      <button class="logout-btn" type="button" :disabled="logoutPending" @click="handleLogout">
        {{ logoutPending ? t("header.signingOut") : t("header.signOut") }}
      </button>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { useAuthStore } from "@/stores/auth";

const FALLBACK_ROUTE = { name: "dashboard" };

defineOptions({ name: "AppHeader" });

const { t } = useI18n();
const authStore = useAuthStore();
const router = useRouter();
const logoutPending = ref(false);

const userName = computed(() => authStore.user?.name ?? t("common.userFallback"));
const roleLabel = computed(() => authStore.roleDisplayName);

const goBack = () => {
  const current = router.currentRoute.value.fullPath;
  if (typeof window !== "undefined" && window.history.length > 1) {
    router.back();
    window.setTimeout(() => {
      if (router.currentRoute.value.fullPath === current) {
        router.push(FALLBACK_ROUTE);
      }
    }, 200);
  } else {
    router.push(FALLBACK_ROUTE);
  }
};

const handleLogout = async () => {
  if (logoutPending.value) {
    return;
  }
  logoutPending.value = true;
  try {
    await authStore.logout();
    await router.replace({ name: "login" }).catch(() => router.push({ name: "login" }));
  } finally {
    logoutPending.value = false;
  }
};
</script>

<style scoped>
.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background-color: #34495e;
  color: #fff;
  padding: 0.75rem 1.5rem;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.back-btn {
  padding: 0.35rem 0.9rem;
  border-radius: 20px;
  border: none;
  background: rgba(255, 255, 255, 0.2);
  color: #fff;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s ease;
}

.back-btn:hover {
  background: rgba(255, 255, 255, 0.3);
}

.brand-title {
  font-size: 1.1rem;
  font-weight: 600;
}

.user-meta {
  display: flex;
  flex-direction: column;
  text-align: right;
  line-height: 1.2;
}

.user-name {
  font-weight: 600;
}

.user-role {
  font-size: 0.75rem;
  opacity: 0.8;
}

.logout-btn {
  padding: 0.35rem 0.9rem;
  border-radius: 20px;
  border: none;
  background: #e74c3c;
  color: #fff;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s ease;
}

.logout-btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.logout-btn:not(:disabled):hover {
  background: #c0392b;
}
</style>
