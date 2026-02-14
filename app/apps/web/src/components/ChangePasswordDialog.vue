<template>
  <el-dialog
    v-model="visible"
    title="Change Password"
    width="500px"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    :show-close="!required"
  >
    <el-alert
      v-if="required"
      type="warning"
      :closable="false"
      show-icon
      style="margin-bottom: 20px"
    >
      For security reasons, you must change your password before continuing.
    </el-alert>

    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="140px"
      @submit.prevent="handleSubmit"
    >
      <el-form-item label="Current Password" prop="currentPassword">
        <el-input
          v-model="form.currentPassword"
          type="password"
          placeholder="Enter current password"
          show-password
          autocomplete="current-password"
        />
      </el-form-item>

      <el-form-item label="New Password" prop="newPassword">
        <el-input
          v-model="form.newPassword"
          type="password"
          placeholder="Enter new password (min 6 characters)"
          show-password
          autocomplete="new-password"
        />
      </el-form-item>

      <el-form-item label="Confirm Password" prop="confirmPassword">
        <el-input
          v-model="form.confirmPassword"
          type="password"
          placeholder="Re-enter new password"
          show-password
          autocomplete="new-password"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <span class="dialog-footer">
        <el-button v-if="!required" @click="handleCancel">Cancel</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          Change Password
        </el-button>
      </span>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">




import { ref, reactive, watch } from "vue";
import { type FormInstance, type FormRules } from "element-plus";
import { apiFetch } from "@/api/http";


import { useNotification } from "@/composables";

const notification = useNotification();
defineOptions({ name: "ChangePasswordDialog" });

interface Props {
  modelValue: boolean;
  required?: boolean;
}

interface Emits {
  (e: "update:modelValue", value: boolean): void;
  (e: "success"): void;
}

const props = withDefaults(defineProps<Props>(), {
  required: false,
});

const emit = defineEmits<Emits>();

const visible = ref(props.modelValue);
const formRef = ref<FormInstance>();
const submitting = ref(false);

const form = reactive({
  currentPassword: "",
  newPassword: "",
  confirmPassword: "",
});

const validateConfirmPassword = (_rule: any, value: string, callback: any) => {
  if (value === "") {
    callback(new Error("Please confirm your new password"));
  } else if (value !== form.newPassword) {
    callback(new Error("Passwords do not match"));
  } else {
    callback();
  }
};

const rules: FormRules = {
  currentPassword: [
    { required: true, message: "Please enter your current password", trigger: "blur" },
  ],
  newPassword: [
    { required: true, message: "Please enter a new password", trigger: "blur" },
    { min: 6, message: "Password must be at least 6 characters", trigger: "blur" },
  ],
  confirmPassword: [{ required: true, validator: validateConfirmPassword, trigger: "blur" }],
};

watch(
  () => props.modelValue,
  (newValue) => {
    visible.value = newValue;
  },
);

watch(visible, (newValue) => {
  emit("update:modelValue", newValue);
  if (!newValue) {
    resetForm();
  }
});

const resetForm = () => {
  form.currentPassword = "";
  form.newPassword = "";
  form.confirmPassword = "";
  formRef.value?.clearValidate();
};

const handleCancel = () => {
  if (!props.required) {
    visible.value = false;
  }
};

const handleSubmit = async () => {
  if (!formRef.value) return;

  try {
    const valid = await formRef.value.validate();
    if (!valid) return;
  } catch {
    return;
  }

  submitting.value = true;

  try {
    await apiFetch("/auth/change-password", {
      method: "POST",
      body: JSON.stringify({
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
      }),
    });

    notification.success("Password changed successfully");
    visible.value = false;
    emit("success");
  } catch (error) {
    const message = error instanceof Error ? error.message : "Failed to change password";
    notification.error(message);
  } finally {
    submitting.value = false;
  }
};




</script>

<style scoped>
.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
