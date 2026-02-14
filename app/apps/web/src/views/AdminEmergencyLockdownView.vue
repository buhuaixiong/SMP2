<template>
  <div class="admin-emergency-lockdown">
    <PageHeader
      :title="t('lockdown.admin.title')"
      :subtitle="t('lockdown.admin.subtitle')"
    />

    <section v-if="lockdownStore.loading" class="panel">
      <el-skeleton :rows="6" animated />
    </section>

    <section v-else class="content">
      <!-- Current Status Panel -->
      <div class="status-panel" :class="{ active: lockdownStore.isActive }">
        <div class="status-header">
          <div class="status-icon">
            <el-icon :size="48">
              <component :is="lockdownStore.isActive ? Lock : Unlock" />
            </el-icon>
          </div>
          <div class="status-info">
            <h2>
              {{
                lockdownStore.isActive
                  ? t("lockdown.admin.status.active")
                  : t("lockdown.admin.status.inactive")
              }}
            </h2>
            <p v-if="lockdownStore.isActive && lockdownStore.activatedAt" class="status-meta">
              {{ t("lockdown.admin.activatedAt") }}:
              {{ formatDateTime(lockdownStore.activatedAt) }}
            </p>
            <p v-if="lockdownStore.isActive && lockdownStore.activatedBy" class="status-meta">
              {{ t("lockdown.admin.activatedBy") }}: {{ lockdownStore.activatedBy }}
            </p>
          </div>
        </div>

        <div v-if="lockdownStore.isActive" class="status-details">
          <div class="detail-item">
            <strong>{{ t("lockdown.admin.reason") }}:</strong>
            <p>{{ lockdownStore.reason || t("lockdown.admin.noReason") }}</p>
          </div>
          <div class="detail-item">
            <strong>{{ t("lockdown.admin.announcement") }}:</strong>
            <p>{{ lockdownStore.announcement || t("lockdown.admin.noAnnouncement") }}</p>
          </div>
        </div>
      </div>

      <!-- Control Panel -->
      <div class="control-panel">
        <div v-if="!lockdownStore.isActive" class="activate-section">
          <h3>{{ t("lockdown.admin.activate.title") }}</h3>
          <p class="warning-text">{{ t("lockdown.admin.activate.warning") }}</p>

          <el-form
            :model="activateForm"
            :rules="activateRules"
            ref="activateFormRef"
            label-width="140px"
          >
            <el-form-item :label="t('lockdown.admin.form.reason')" prop="reason">
              <el-input
                v-model="activateForm.reason"
                type="textarea"
                :rows="3"
                :placeholder="t('lockdown.admin.form.reasonPlaceholder')"
                maxlength="500"
                show-word-limit
              />
            </el-form-item>

            <el-form-item :label="t('lockdown.admin.form.announcement')" prop="announcement">
              <el-input
                v-model="activateForm.announcement"
                type="textarea"
                :rows="4"
                :placeholder="t('lockdown.admin.form.announcementPlaceholder')"
                maxlength="1000"
                show-word-limit
              />
            </el-form-item>

            <el-form-item>
              <el-button
                type="danger"
                size="large"
                :loading="activating"
                :icon="Lock"
                @click="handleActivate"
              >
                {{ t("lockdown.admin.activate.button") }}
              </el-button>
            </el-form-item>
          </el-form>
        </div>

        <div v-else class="deactivate-section">
          <h3>{{ t("lockdown.admin.deactivate.title") }}</h3>
          <p>{{ t("lockdown.admin.deactivate.description") }}</p>
          <el-button
            type="success"
            size="large"
            :loading="deactivating"
            :icon="Unlock"
            @click="handleDeactivate"
          >
            {{ t("lockdown.admin.deactivate.button") }}
          </el-button>
        </div>
      </div>

      <!-- History Panel -->
      <div class="history-panel">
        <div class="history-header">
          <h3>{{ t("lockdown.admin.history.title") }}</h3>
          <el-button type="primary" size="small" :loading="historyLoading" @click="loadHistory">
            {{ t("lockdown.admin.history.refresh") }}
          </el-button>
        </div>

        <el-table
          v-if="!historyLoading && history.length > 0"
          :data="history"
          stripe
          style="width: 100%"
        >
          <el-table-column prop="action" :label="t('lockdown.admin.history.action')" width="180">
            <template #default="{ row }">
              <el-tag
                :type="row.action === 'LOCKDOWN_ACTIVATED' ? 'danger' : 'success'"
                size="small"
              >
                {{ formatAction(row.action) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column
            prop="actorName"
            :label="t('lockdown.admin.history.actor')"
            width="150"
          />
          <el-table-column
            prop="timestamp"
            :label="t('lockdown.admin.history.timestamp')"
            width="180"
          >
            <template #default="{ row }">
              {{ formatDateTime(row.timestamp) }}
            </template>
          </el-table-column>
          <el-table-column
            prop="ipAddress"
            :label="t('lockdown.admin.history.ipAddress')"
            width="150"
          />
          <el-table-column :label="t('lockdown.admin.history.details')">
            <template #default="{ row }">
              <div v-if="row.changes.reason">
                <strong>{{ t("lockdown.admin.reason") }}:</strong> {{ row.changes.reason }}
              </div>
              <div v-if="row.changes.announcement">
                <strong>{{ t("lockdown.admin.announcement") }}:</strong>
                {{ row.changes.announcement }}
              </div>
            </template>
          </el-table-column>
        </el-table>

        <div v-else-if="historyLoading" class="history-loading">
          <el-skeleton :rows="5" animated />
        </div>

        <div v-else class="history-empty">
          {{ t("lockdown.admin.history.empty") }}
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">




import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { type FormInstance, type FormRules } from "element-plus";
import { Lock, Unlock } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import { useLockdownStore } from "@/stores/lockdown";
import type { LockdownHistoryEntry } from "@/api/system";


import { useNotification } from "@/composables";

const notification = useNotification();
defineOptions({ name: "AdminEmergencyLockdownView" });

const { t } = useI18n();
const lockdownStore = useLockdownStore();

const activateFormRef = ref<FormInstance>();
const activateForm = reactive({
  reason: "",
  announcement: "",
});

const activateRules: FormRules = {
  reason: [
    { required: true, message: t("lockdown.admin.form.reasonRequired"), trigger: "blur" },
    { min: 10, message: t("lockdown.admin.form.reasonMinLength"), trigger: "blur" },
  ],
  announcement: [
    { required: true, message: t("lockdown.admin.form.announcementRequired"), trigger: "blur" },
    { min: 20, message: t("lockdown.admin.form.announcementMinLength"), trigger: "blur" },
  ],
};

const activating = ref(false);
const deactivating = ref(false);
const historyLoading = ref(false);
const history = ref<LockdownHistoryEntry[]>([]);

const formatDateTime = (dateStr: string) => {
  const date = new Date(dateStr);
  return date.toLocaleString();
};

const formatAction = (action: string) => {
  return action === "LOCKDOWN_ACTIVATED"
    ? t("lockdown.admin.history.activated")
    : t("lockdown.admin.history.deactivated");
};

const handleActivate = async () => {
  if (!activateFormRef.value) return;

  await activateFormRef.value.validate(async (valid) => {
    if (!valid) return;

    try {
      await notification.confirm(
        t("lockdown.admin.activate.confirmMessage"),
        t("lockdown.admin.activate.confirmTitle"),
        {
          confirmButtonText: t("common.confirm"),
          cancelButtonText: t("common.cancel"),
          type: "warning",
        },
      );

      activating.value = true;
      await lockdownStore.activate({
        reason: activateForm.reason,
        announcement: activateForm.announcement,
      });

      notification.success(t("lockdown.admin.activate.success"));

      // Reset form
      activateForm.reason = "";
      activateForm.announcement = "";
      activateFormRef.value?.resetFields();

      // Reload history
      await loadHistory();
    } catch (error: any) {
      if (error !== "cancel") {
        console.error("Error activating lockdown:", error);
        notification.error(t("lockdown.admin.activate.error"));
      }
    } finally {
      activating.value = false;
    }
  });
};

const handleDeactivate = async () => {
  try {
    await notification.confirm(
      t("lockdown.admin.deactivate.confirmMessage"),
      t("lockdown.admin.deactivate.confirmTitle"),
      {
        confirmButtonText: t("common.confirm"),
        cancelButtonText: t("common.cancel"),
        type: "success",
      },
    );

    deactivating.value = true;
    await lockdownStore.deactivate();

    notification.success(t("lockdown.admin.deactivate.success"));

    // Reload history
    await loadHistory();
  } catch (error: any) {
    if (error !== "cancel") {
      console.error("Error deactivating lockdown:", error);
      notification.error(t("lockdown.admin.deactivate.error"));
    }
  } finally {
    deactivating.value = false;
  }
};

const loadHistory = async () => {
  historyLoading.value = true;
  try {
    await lockdownStore.loadHistory(50);
    history.value = lockdownStore.history;
  } catch (error) {
    console.error("Error loading history:", error);
    notification.error(t("lockdown.admin.history.error"));
  } finally {
    historyLoading.value = false;
  }
};

onMounted(async () => {
  await lockdownStore.checkFullStatus();
  await loadHistory();
});




</script>

<style scoped>
.admin-emergency-lockdown {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.panel {
  background: white;
  padding: 24px;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.status-panel {
  background: white;
  padding: 32px;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
  border-left: 4px solid #67c23a;
}

.status-panel.active {
  border-left-color: #f56c6c;
  background: linear-gradient(135deg, #fff5f5 0%, white 100%);
}

.status-header {
  display: flex;
  align-items: center;
  gap: 24px;
  margin-bottom: 24px;
}

.status-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 80px;
  height: 80px;
  border-radius: 50%;
  background: rgba(103, 194, 58, 0.1);
  color: #67c23a;
}

.status-panel.active .status-icon {
  background: rgba(245, 108, 108, 0.1);
  color: #f56c6c;
}

.status-info h2 {
  font-size: 24px;
  margin: 0 0 8px 0;
  color: #303133;
}

.status-meta {
  font-size: 14px;
  color: #606266;
  margin: 4px 0;
}

.status-details {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 24px;
  padding-top: 24px;
  border-top: 1px solid #ebeef5;
}

.detail-item strong {
  display: block;
  margin-bottom: 8px;
  color: #303133;
}

.detail-item p {
  margin: 0;
  color: #606266;
  line-height: 1.6;
}

.control-panel {
  background: white;
  padding: 32px;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.activate-section h3,
.deactivate-section h3 {
  font-size: 20px;
  color: #303133;
  margin: 0 0 16px 0;
}

.warning-text {
  background: #fff4e6;
  border-left: 4px solid #e6a23c;
  padding: 12px 16px;
  margin-bottom: 24px;
  color: #606266;
  line-height: 1.6;
}

.deactivate-section p {
  color: #606266;
  margin-bottom: 16px;
  line-height: 1.6;
}

.history-panel {
  background: white;
  padding: 32px;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.history-header h3 {
  font-size: 20px;
  color: #303133;
  margin: 0;
}

.history-loading,
.history-empty {
  text-align: center;
  padding: 48px 24px;
  color: #909399;
}

@media (max-width: 768px) {
  .status-details {
    grid-template-columns: 1fr;
  }

  .status-header {
    flex-direction: column;
    text-align: center;
  }

  .history-header {
    flex-direction: column;
    align-items: stretch;
    gap: 16px;
  }
}
</style>
