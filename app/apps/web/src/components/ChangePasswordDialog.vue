<template>
  <el-dialog
    v-model="visible"
    :title="t('auth.changePassword.title')"
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
      {{ t("auth.changePassword.requiredMessage") }}
    </el-alert>

    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="140px"
      @submit.prevent="handleSubmit"
    >
      <el-form-item
        :label="t('auth.changePassword.fields.currentPassword')"
        prop="currentPassword"
        :error="serverErrors.currentPassword"
      >
        <el-input
          v-model="form.currentPassword"
          type="password"
          :placeholder="t('auth.changePassword.placeholders.currentPassword')"
          show-password
          autocomplete="current-password"
          @input="serverErrors.currentPassword = ''"
        />
      </el-form-item>

      <el-form-item
        :label="t('auth.changePassword.fields.newPassword')"
        prop="newPassword"
        :error="serverErrors.newPassword"
      >
        <el-input
          v-model="form.newPassword"
          type="password"
          :placeholder="t('auth.changePassword.placeholders.newPassword')"
          show-password
          autocomplete="new-password"
          @input="serverErrors.newPassword = ''"
        />
      </el-form-item>

      <el-form-item :label="t('auth.changePassword.fields.confirmPassword')" prop="confirmPassword">
        <el-input
          v-model="form.confirmPassword"
          type="password"
          :placeholder="t('auth.changePassword.placeholders.confirmPassword')"
          show-password
          autocomplete="new-password"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <span class="dialog-footer">
        <el-button v-if="!required" @click="handleCancel">{{
          t("auth.changePassword.actions.cancel")
        }}</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ t("auth.changePassword.actions.submit") }}
        </el-button>
      </span>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, ref, reactive, watch } from "vue";
import { type FormInstance, type FormRules } from "element-plus";
import { apiFetch } from "@/api/http";
import { useI18n } from "vue-i18n";

import { useNotification } from "@/composables";
import {
  createChangePasswordRules,
  getChangePasswordServerErrors,
} from "@/components/changePasswordDialog";

const notification = useNotification();
const { t } = useI18n();
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

const serverErrors = reactive({
  currentPassword: "",
  newPassword: "",
});

const rules = computed<FormRules>(() => createChangePasswordRules(form, t));

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
  serverErrors.currentPassword = "";
  serverErrors.newPassword = "";
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
  serverErrors.currentPassword = "";
  serverErrors.newPassword = "";

  try {
    await apiFetch("/auth/change-password", {
      method: "POST",
      body: JSON.stringify({
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
      }),
    });

    notification.success(t("auth.changePassword.notifications.success"));
    visible.value = false;
    emit("success");
  } catch (error) {
    const message =
      error instanceof Error ? error.message : t("auth.changePassword.notifications.failure");
    const mappedErrors = getChangePasswordServerErrors(message, t);
    serverErrors.currentPassword = mappedErrors.currentPassword ?? "";
    serverErrors.newPassword = mappedErrors.newPassword ?? "";
    notification.error(mappedErrors.currentPassword ?? mappedErrors.newPassword ?? message);
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
