<template>
  <Teleport to="body">
    <el-dialog
      v-model="dialogVisible"
      class="lockdown-announcement-dialog"
      :close-on-click-modal="false"
      :close-on-press-escape="false"
      :show-close="false"
      width="520px"
    >
      <template #header>
        <div class="dialog-header">
          <div class="header-icon">
            <el-icon :size="28">
              <WarningFilled />
            </el-icon>
          </div>
          <div class="header-text">
            <h2>{{ t("lockdown.dialog.title") }}</h2>
            <p>{{ t("lockdown.dialog.description") }}</p>
          </div>
        </div>
      </template>

      <div class="dialog-body">
        <div class="announcement-block">
          <strong>{{ t("lockdown.dialog.announcementLabel") }}</strong>
          <p>{{ announcementText }}</p>
        </div>
        <p v-if="lockdownStore.lastServerMessage" class="additional-message">
          {{ lockdownStore.lastServerMessage }}
        </p>
        <p v-if="activatedAtText" class="meta">
          {{ t("lockdown.dialog.activatedAt") }}: {{ activatedAtText }}
        </p>
        <p class="contact-hint">
          {{ t("lockdown.dialog.contact") }}
        </p>
      </div>

      <template #footer>
        <el-button type="primary" @click="handleConfirm">
          {{ t("lockdown.dialog.confirm") }}
        </el-button>
      </template>
    </el-dialog>
  </Teleport>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { WarningFilled } from "@element-plus/icons-vue";
import { useLockdownStore } from "@/stores/lockdown";

const { t, locale } = useI18n();
const lockdownStore = useLockdownStore();

const dialogVisible = computed({
  get: () => Boolean(lockdownStore.shouldShowAnnouncement),
  set: (value: boolean) => {
    if (!value) {
      lockdownStore.acknowledgeAnnouncement();
    }
  },
});

const announcementText = computed(() => {
  return lockdownStore.announcement ?? t("lockdown.banner.defaultAnnouncement");
});

const activatedAtText = computed(() => {
  const value = lockdownStore.activatedAt;
  if (!value) {
    return "";
  }

  try {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return new Intl.DateTimeFormat(locale.value as string | undefined, {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    }).format(date);
  } catch (error) {
    console.warn("[LockdownAnnouncementDialog] Failed to format activatedAt timestamp", error);
    return value;
  }
});

const handleConfirm = () => {
  dialogVisible.value = false;
};
</script>

<style scoped>
.lockdown-announcement-dialog :deep(.el-dialog__header) {
  padding-bottom: 12px;
}

.dialog-header {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: rgba(255, 71, 87, 0.15);
  color: #ff4757;
}

.header-text h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #ff4757;
}

.header-text p {
  margin: 0;
  font-size: 13px;
  color: #606266;
}

.dialog-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
  font-size: 14px;
  color: #303133;
}

.announcement-block {
  padding: 12px;
  border-radius: 8px;
  background: rgba(255, 71, 87, 0.08);
  border: 1px solid rgba(255, 71, 87, 0.2);
}

.announcement-block strong {
  display: block;
  margin-bottom: 6px;
  color: #c0392b;
  font-weight: 600;
}

.additional-message {
  margin: 0;
  padding: 10px;
  border-radius: 6px;
  background: rgba(255, 193, 7, 0.12);
  color: #8a6d3b;
}

.meta {
  font-size: 12px;
  color: #909399;
}

.contact-hint {
  font-size: 12px;
  color: #606266;
}

.lockdown-announcement-dialog :deep(.el-dialog__footer) {
  padding-top: 0;
}
</style>
