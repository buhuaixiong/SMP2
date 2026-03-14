<template>
  <div v-if="lockdownStore.isActive" class="lockdown-banner">
    <div class="lockdown-content">
      <div class="lockdown-icon">
        <el-icon :size="24">
          <WarningFilled />
        </el-icon>
      </div>
      <div class="lockdown-message">
        <div class="lockdown-title">
          <strong>{{ t("lockdown.banner.title") }}</strong>
        </div>
        <div class="lockdown-announcement">
          {{ announcement }}
        </div>
      </div>
      <div v-if="isAdmin" class="lockdown-admin-actions">
        <el-button type="danger" size="small" @click="goToLockdownControl">
          {{ t("lockdown.banner.manageButton") }}
        </el-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { WarningFilled } from "@element-plus/icons-vue";
import { useLockdownStore } from "@/stores/lockdown";
import { useAuthStore } from "@/stores/auth";

defineOptions({ name: "LockdownBanner" });

const { t } = useI18n();
const router = useRouter();
const lockdownStore = useLockdownStore();
const authStore = useAuthStore();

const isAdmin = computed(() => authStore.user?.role === "admin");
const announcement = computed(() => {
  return lockdownStore.announcement || t("lockdown.banner.defaultAnnouncement");
});

const goToLockdownControl = () => {
  router.push({ name: "admin-emergency-lockdown" });
};
</script>

<style scoped>
.lockdown-banner {
  position: sticky;
  top: 0;
  z-index: 1000;
  background: linear-gradient(135deg, #ff6b6b 0%, #ff4757 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(255, 71, 87, 0.3);
  animation: slideDown 0.3s ease-out;
}

@keyframes slideDown {
  from {
    transform: translateY(-100%);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.lockdown-content {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 12px 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.lockdown-icon {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  background: rgba(255, 255, 255, 0.2);
  border-radius: 50%;
  animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
  0%,
  100% {
    transform: scale(1);
    opacity: 1;
  }
  50% {
    transform: scale(1.1);
    opacity: 0.8;
  }
}

.lockdown-message {
  flex: 1;
}

.lockdown-title {
  font-size: 16px;
  margin-bottom: 4px;
  letter-spacing: 0.5px;
}

.lockdown-announcement {
  font-size: 14px;
  opacity: 0.95;
  line-height: 1.5;
}

.lockdown-admin-actions {
  flex-shrink: 0;
}

.lockdown-admin-actions .el-button {
  background: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.4);
  color: white;
  font-weight: 600;
}

.lockdown-admin-actions .el-button:hover {
  background: rgba(255, 255, 255, 0.3);
  border-color: rgba(255, 255, 255, 0.6);
}

@media (max-width: 768px) {
  .lockdown-content {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
    padding: 16px;
  }

  .lockdown-icon {
    align-self: center;
  }

  .lockdown-message {
    text-align: center;
  }

  .lockdown-admin-actions {
    display: flex;
    justify-content: center;
  }
}
</style>
