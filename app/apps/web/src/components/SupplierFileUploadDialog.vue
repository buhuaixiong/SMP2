<template>
  <el-dialog v-model="dialogVisible" title="上传文件" width="600px" :before-close="handleClose">
    <el-alert title="审批流程说明" type="warning" :closable="false" style="margin-bottom: 20px">
      <p>文件上传后将进入<strong>5级审批流程</strong>:</p>
      <ol style="margin: 8px 0; padding-left: 20px">
        <li>采购员审批</li>
        <li>品质审批</li>
        <li>采购经理审批</li>
        <li>采购总监审批</li>
        <li>财务总监审批</li>
      </ol>
      <p style="margin-top: 8px; color: #909399; font-size: 13px">
        文件审批完成后才能生效。如任何步骤被拒绝,需重新提交。
      </p>
    </el-alert>

    <el-form ref="formRef" :model="formData" :rules="formRules" label-width="100px">
      <el-form-item label="文件选择" prop="file" required>
        <el-upload
          ref="uploadRef"
          :auto-upload="false"
          :limit="1"
          :on-change="handleFileChange"
          :on-remove="handleFileRemove"
          :file-list="fileList"
          accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png"
        >
          <el-button type="primary">
            <el-icon><Upload /></el-icon>
            选择文件
          </el-button>
          <template #tip>
            <div class="el-upload__tip">支持格式: PDF, Word, Excel, 图片 (最大20MB)</div>
          </template>
        </el-upload>
      </el-form-item>

      <el-form-item label="文件说明" prop="fileDescription">
        <el-input
          v-model="formData.fileDescription"
          type="textarea"
          :rows="3"
          placeholder="请简要说明文件内容和用途(可选)"
          maxlength="500"
          show-word-limit
        />
      </el-form-item>

      <el-form-item label="生效日期" prop="validFrom" required>
        <el-date-picker
          v-model="formData.validFrom"
          type="date"
          placeholder="选择生效日期"
          :disabled-date="disabledStartDate"
          style="width: 100%"
          format="YYYY-MM-DD"
          value-format="YYYY-MM-DD"
        />
      </el-form-item>

      <el-form-item label="过期日期" prop="validTo" required>
        <el-date-picker
          v-model="formData.validTo"
          type="date"
          placeholder="选择过期日期"
          :disabled-date="disabledEndDate"
          style="width: 100%"
          format="YYYY-MM-DD"
          value-format="YYYY-MM-DD"
        />
      </el-form-item>

      <el-alert v-if="selectedFile" type="info" :closable="false" style="margin-bottom: 16px">
        <div style="display: flex; align-items: center; gap: 8px">
          <el-icon><Document /></el-icon>
          <span><strong>已选文件:</strong> {{ selectedFile.name }}</span>
          <span style="color: #909399">({{ formatFileSize(selectedFile.size) }})</span>
        </div>
      </el-alert>
    </el-form>

    <template #footer>
      <span class="dialog-footer">
        <el-button @click="handleClose">取消</el-button>
        <el-button
          type="primary"
          :loading="uploading"
          :disabled="!selectedFile"
          @click="handleSubmit"
        >
          上传并提交审批
        </el-button>
      </span>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">




import { ref, reactive, computed } from "vue";

import { Upload, Document } from "@element-plus/icons-vue";
import type { UploadFile } from "element-plus";
import { uploadFileForApproval } from "@/api/fileUploads";


import { useNotification } from "@/composables";

const notification = useNotification();
const props = defineProps<{
  visible: boolean;
  supplierId: number;
}>();

const emit = defineEmits<{
  (e: "update:visible", value: boolean): void;
  (e: "success"): void;
}>();

const dialogVisible = computed({
  get: () => props.visible,
  set: (value) => emit("update:visible", value),
});

const formRef = ref();
const uploadRef = ref();
const uploading = ref(false);
const fileList = ref<UploadFile[]>([]);

const formData = reactive({
  fileDescription: "",
  validFrom: "",
  validTo: "",
});

const formRules = {
  validFrom: [{ required: true, message: "请选择生效日期", trigger: "change" }],
  validTo: [
    { required: true, message: "请选择过期日期", trigger: "change" },
    {
      validator: (rule: any, value: any, callback: any) => {
        if (value && formData.validFrom && value <= formData.validFrom) {
          callback(new Error("过期日期必须晚于生效日期"));
        } else {
          callback();
        }
      },
      trigger: "change",
    },
  ],
};

const selectedFile = ref<File | null>(null);

// Disable dates before today for start date
const disabledStartDate = (time: Date) => {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return time.getTime() < today.getTime();
};

// Disable dates before or equal to validFrom for end date
const disabledEndDate = (time: Date) => {
  if (!formData.validFrom) {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return time.getTime() < today.getTime();
  }
  const fromDate = new Date(formData.validFrom);
  fromDate.setHours(0, 0, 0, 0);
  return time.getTime() <= fromDate.getTime();
};

const handleFileChange = (file: UploadFile) => {
  selectedFile.value = file.raw || null;
};

const handleFileRemove = () => {
  selectedFile.value = null;
};

const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + " " + sizes[i];
};

const handleClose = () => {
  if (uploading.value) {
    notification.warning("文件正在上传,请稍候...");
    return;
  }
  dialogVisible.value = false;
  resetForm();
};

const resetForm = () => {
  formData.fileDescription = "";
  formData.validFrom = "";
  formData.validTo = "";
  selectedFile.value = null;
  fileList.value = [];
  if (formRef.value) {
    formRef.value.clearValidate();
  }
  if (uploadRef.value) {
    uploadRef.value.clearFiles();
  }
};

const handleSubmit = async () => {
  if (!selectedFile.value) {
    notification.warning("请选择要上传的文件");
    return;
  }

  // Validate form
  try {
    await formRef.value.validate();
  } catch (error) {
    notification.warning("请完整填写表单");
    return;
  }

  if (!formData.validFrom || !formData.validTo) {
    notification.warning("请选择文件有效期");
    return;
  }

  // File size validation (20MB)
  const maxSize = 20 * 1024 * 1024;
  if (selectedFile.value.size > maxSize) {
    notification.error("文件大小不能超过20MB");
    return;
  }

  try {
    uploading.value = true;

    const result = await uploadFileForApproval({
      supplierId: props.supplierId,
      file: selectedFile.value,
      fileDescription: formData.fileDescription || undefined,
      validFrom: formData.validFrom,
      validTo: formData.validTo,
    });

    notification.success({
      message: result.message || "文件上传成功,已进入审批流程",
      duration: 3000,
    });

    emit("success");
    dialogVisible.value = false;
    resetForm();
  } catch (error: any) {
    console.error("Failed to upload file:", error);
    notification.error(error?.message || "文件上传失败,请稍后重试");
  } finally {
    uploading.value = false;
  }
};




</script>

<style scoped>
.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.el-upload__tip {
  color: #909399;
  font-size: 12px;
  margin-top: 7px;
}
</style>
