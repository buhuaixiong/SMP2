<template>
  <div class="file-upload-config-view">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>{{ t("fileUpload.configManagement") }}</h2>
          <el-button type="primary" :icon="Plus" @click="handleCreate">
            {{ t("fileUpload.createConfig") }}
          </el-button>
        </div>
      </template>

      <!-- Scan Statistics -->
      <el-row :gutter="20" style="margin-bottom: 24px">
        <el-col :span="8">
          <el-statistic :title="t('fileUpload.totalScans')" :value="statistics.total" />
        </el-col>
        <el-col :span="8">
          <el-statistic :title="t('fileUpload.cleanFiles')" :value="statistics.clean">
            <template #suffix>
              <el-icon style="color: #67c23a"><Check /></el-icon>
            </template>
          </el-statistic>
        </el-col>
        <el-col :span="8">
          <el-statistic :title="t('fileUpload.infectedFiles')" :value="statistics.infected">
            <template #suffix>
              <el-icon style="color: #f56c6c"><Close /></el-icon>
            </template>
          </el-statistic>
        </el-col>
      </el-row>

      <!-- Configurations Table -->
      <el-table v-loading="loading" :data="configs" border stripe style="width: 100%">
        <el-table-column prop="scenario" :label="t('fileUpload.scenario')" width="180" />
        <el-table-column prop="scenario_name" :label="t('fileUpload.scenarioName')" width="150" />
        <el-table-column prop="allowed_formats" :label="t('fileUpload.formats')" min-width="200">
          <template #default="{ row }">
            <el-tag
              v-for="format in row.allowed_formats.split(',')"
              :key="format"
              size="small"
              style="margin: 2px"
            >
              {{ format.trim() }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="max_file_size" :label="t('fileUpload.maxSize')" width="100">
          <template #default="{ row }">
            {{ formatFileSize(row.max_file_size) }}
          </template>
        </el-table-column>
        <el-table-column prop="max_file_count" :label="t('fileUpload.maxCount')" width="100" />
        <el-table-column
          prop="enable_virus_scan"
          :label="t('fileUpload.enableVirusScan')"
          width="120"
        >
          <template #default="{ row }">
            <el-tag :type="row.enable_virus_scan ? 'success' : 'info'">
              {{ row.enable_virus_scan ? t("common.yes") : t("common.no") }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="t('common.actions')" width="180" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" :icon="Edit" @click="handleEdit(row)">
              {{ t("common.edit") }}
            </el-button>
            <el-button
              link
              type="danger"
              size="small"
              :icon="Delete"
              @click="handleDelete(row)"
              :disabled="isProtectedScenario(row.scenario)"
            >
              {{ t("common.delete") }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Edit/Create Dialog -->
    <el-dialog
      v-model="dialogVisible"
      :title="editMode ? t('fileUpload.editConfig') : t('fileUpload.createConfig')"
      width="600px"
    >
      <el-form ref="formRef" :model="formData" :rules="formRules" label-width="120px">
        <el-form-item :label="t('fileUpload.scenario')" prop="scenario">
          <el-input
            v-model="formData.scenario"
            :disabled="editMode"
            :placeholder="t('common.pleaseInput')"
          />
        </el-form-item>
        <el-form-item :label="t('fileUpload.scenarioName')" prop="scenario_name">
          <el-input v-model="formData.scenario_name" :placeholder="t('common.pleaseInput')" />
        </el-form-item>
        <el-form-item :label="t('fileUpload.scenarioDescription')" prop="scenario_description">
          <el-input
            v-model="formData.scenario_description"
            type="textarea"
            :rows="3"
            :placeholder="t('common.pleaseInput')"
          />
        </el-form-item>
        <el-form-item :label="t('fileUpload.formats')" prop="allowed_formats">
          <el-input
            v-model="formData.allowed_formats"
            :placeholder="t('fileUpload.formatsPlaceholder')"
          />
          <div class="form-item-tip">
            {{ t("fileUpload.formatsTip") }}
          </div>
        </el-form-item>
        <el-form-item :label="t('fileUpload.maxSize')" prop="max_file_size">
          <el-input-number v-model="fileSizeMB" :min="1" :max="500" :step="1" />
          <span style="margin-left: 8px">MB</span>
        </el-form-item>
        <el-form-item :label="t('fileUpload.maxCount')" prop="max_file_count">
          <el-input-number v-model="formData.max_file_count" :min="1" :max="20" :step="1" />
        </el-form-item>
        <el-form-item :label="t('fileUpload.enableVirusScan')">
          <el-switch v-model="formData.enable_virus_scan" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ t("common.confirm") }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { type FormInstance, type FormRules } from "element-plus";
import { Plus, Edit, Delete, Check, Close } from "@element-plus/icons-vue";
import {
  fetchFileUploadConfigs,
  updateFileUploadConfig,
  createFileUploadConfig,
  deleteFileUploadConfig,
  fetchScanStatistics,
} from "@/api/fileUploadConfig";
import type { FileUploadConfig, ScanStatistics } from "@/types/fileUpload";


import { useNotification } from "@/composables";
const notification = useNotification();

const { t } = useI18n();

const loading = ref(false);
const submitting = ref(false);
const dialogVisible = ref(false);
const editMode = ref(false);
const formRef = ref<FormInstance>();

const configs = ref<FileUploadConfig[]>([]);
const statistics = ref<ScanStatistics>({ total: 0, clean: 0, infected: 0 });

const formData = ref<Partial<FileUploadConfig>>({
  scenario: "",
  scenario_name: "",
  scenario_description: "",
  allowed_formats: "",
  max_file_size: 50 * 1024 * 1024,
  max_file_count: 5,
  enable_virus_scan: 1,
});

// Convert file size to MB for display
const fileSizeMB = computed({
  get: () => Math.round((formData.value.max_file_size || 0) / (1024 * 1024)),
  set: (val) => {
    formData.value.max_file_size = val * 1024 * 1024;
  },
});

const formRules: FormRules = {
  scenario: [
    { required: true, message: t("common.pleaseInput"), trigger: "blur" },
    { pattern: /^[a-z_]+$/, message: t("fileUpload.validation.scenarioPattern"), trigger: "blur" },
  ],
  scenario_name: [{ required: true, message: t("common.pleaseInput"), trigger: "blur" }],
  allowed_formats: [{ required: true, message: t("common.pleaseInput"), trigger: "blur" }],
  max_file_size: [{ required: true, message: t("common.pleaseInput"), trigger: "blur" }],
  max_file_count: [{ required: true, message: t("common.pleaseInput"), trigger: "blur" }],
};

const protectedScenarios = [
  "rfq_price_comparison",
  "rfq_attachments",
  "supplier_documents",
  "contract_documents",
  "invoice_documents",
];

function isProtectedScenario(scenario: string): boolean {
  return protectedScenarios.includes(scenario);
}

function formatFileSize(bytes: number): string {
  const mb = bytes / (1024 * 1024);
  return `${mb.toFixed(0)}MB`;
}

async function loadConfigs() {
  loading.value = true;
  try {
    configs.value = await fetchFileUploadConfigs();
  } catch (error: any) {
    notification.error(error.message || t("fileUpload.messages.loadFailed"));
  } finally {
    loading.value = false;
  }
}

async function loadStatistics() {
  try {
    statistics.value = await fetchScanStatistics();
  } catch (error: any) {
    console.error("Failed to load statistics:", error);
  }
}

function handleCreate() {
  editMode.value = false;
  formData.value = {
    scenario: "",
    scenario_name: "",
    scenario_description: "",
    allowed_formats: ".pdf,.doc,.docx,.xlsx,.xls,.jpg,.jpeg,.png,.zip",
    max_file_size: 50 * 1024 * 1024,
    max_file_count: 5,
    enable_virus_scan: 1,
  };
  dialogVisible.value = true;
}

function handleEdit(row: FileUploadConfig) {
  editMode.value = true;
  formData.value = { ...row };
  dialogVisible.value = true;
}

async function handleSubmit() {
  if (!formRef.value) return;

  await formRef.value.validate(async (valid) => {
    if (!valid) return;

    submitting.value = true;
    try {
      if (editMode.value) {
        await updateFileUploadConfig(formData.value.scenario!, formData.value);
        notification.success(t("fileUpload.messages.updateSuccess"));
      } else {
        await createFileUploadConfig(formData.value);
        notification.success(t("fileUpload.messages.createSuccess"));
      }

      dialogVisible.value = false;
      await loadConfigs();
    } catch (error: any) {
      notification.error(error.message || t("fileUpload.messages.operationFailed"));
    } finally {
      submitting.value = false;
    }
  });
}

async function handleDelete(row: FileUploadConfig) {
  try {
    await notification.confirm(t("fileUpload.messages.deleteConfirm", { name: row.scenario_name }), t("common.confirm"), {
      confirmButtonText: t("common.confirm"),
      cancelButtonText: t("common.cancel"),
      type: "warning",
    });

    await deleteFileUploadConfig(row.scenario);
    notification.success(t("fileUpload.messages.deleteSuccess"));
    await loadConfigs();
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error.message || t("fileUpload.messages.deleteFailed"));
    }
  }
}

onMounted(() => {
  loadConfigs();
  loadStatistics();
});




</script>

<style scoped>
.file-upload-config-view {
  padding: 20px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-header h2 {
  margin: 0;
  font-size: 18px;
}

.form-item-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}
</style>
