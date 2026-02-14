<template>
  <div class="activation-page">
    <div class="activation-container">
      <div class="activation-card">
        <div class="activation-header">
          <el-icon class="activation-icon" :size="48">
            <Check v-if="activationSuccess" />
            <Key v-else />
          </el-icon>
          <h1>{{ t('activation.title') }}</h1>
          <p v-if="!activationSuccess" class="activation-subtitle">
            {{ t('activation.subtitle') }}
          </p>
        </div>

        <!-- Success State -->
        <div v-if="activationSuccess" class="activation-success">
          <el-result
            icon="success"
            :title="t('activation.success.title')"
            :sub-title="t('activation.success.subtitle')"
          >
            <template #extra>
              <div class="success-details">
                <p><strong>{{ t('activation.success.username') }}:</strong> {{ activationResult?.username }}</p>
                <p><strong>{{ t('activation.success.role') }}:</strong> {{ t(`roles.${activationResult?.role}`) }}</p>
              </div>
              <el-button type="primary" size="large" @click="goToLogin">
                {{ t('activation.success.goToLogin') }}
              </el-button>
            </template>
          </el-result>
        </div>

        <!-- Activation Form -->
        <el-form
          v-else
          ref="formRef"
          :model="form"
          :rules="rules"
          label-position="top"
          class="activation-form"
          @submit.prevent="handleSubmit"
        >
          <el-form-item :label="t('activation.form.token')" prop="token">
            <el-input
              v-model="form.token"
              :placeholder="t('activation.form.tokenPlaceholder')"
              :disabled="!!tokenFromUrl"
              size="large"
            >
              <template #prefix>
                <el-icon><Key /></el-icon>
              </template>
            </el-input>
            <div v-if="tokenFromUrl" class="form-hint">
              {{ t('activation.form.tokenFromUrl') }}
            </div>
          </el-form-item>

          <el-form-item :label="t('activation.form.password')" prop="password">
            <el-input
              v-model="form.password"
              type="password"
              :placeholder="t('activation.form.passwordPlaceholder')"
              size="large"
              show-password
            >
              <template #prefix>
                <el-icon><Lock /></el-icon>
              </template>
            </el-input>
            <div class="password-requirements">
              <p>{{ t('activation.form.passwordRequirements') }}</p>
              <ul>
                <li :class="{ valid: hasMinLength }">{{ t('activation.form.minLength') }}</li>
                <li :class="{ valid: hasUppercase }">{{ t('activation.form.hasUppercase') }}</li>
                <li :class="{ valid: hasLowercase }">{{ t('activation.form.hasLowercase') }}</li>
                <li :class="{ valid: hasNumber }">{{ t('activation.form.hasNumber') }}</li>
                <li :class="{ valid: hasSpecialChar }">{{ t('activation.form.hasSpecialChar') }}</li>
              </ul>
            </div>
          </el-form-item>

          <el-form-item :label="t('activation.form.confirmPassword')" prop="confirmPassword">
            <el-input
              v-model="form.confirmPassword"
              type="password"
              :placeholder="t('activation.form.confirmPasswordPlaceholder')"
              size="large"
              show-password
            >
              <template #prefix>
                <el-icon><Lock /></el-icon>
              </template>
            </el-input>
          </el-form-item>

          <el-alert
            v-if="error"
            :title="error"
            type="error"
            :closable="false"
            show-icon
            class="activation-error"
          />

          <el-button
            type="primary"
            size="large"
            :loading="loading"
            :disabled="!isFormValid"
            native-type="submit"
            class="activation-submit"
          >
            {{ t('activation.form.submit') }}
          </el-button>
        </el-form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { type FormInstance, type FormRules } from "element-plus";
import { Key, Lock, Check } from '@element-plus/icons-vue'
import { activateSupplierAccount, type AccountActivationResponse } from '@/api/public'


import { useNotification } from "@/composables";

const notification = useNotification();
const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const formRef = ref<FormInstance>()
const loading = ref(false)
const error = ref('')
const activationSuccess = ref(false)
const activationResult = ref<AccountActivationResponse | null>(null)

const tokenFromUrl = computed(() => route.query.token as string | undefined)

const form = ref({
  token: '',
  password: '',
  confirmPassword: '',
})

// Password validation computed properties
const hasMinLength = computed(() => form.value.password.length >= 8)
const hasUppercase = computed(() => /[A-Z]/.test(form.value.password))
const hasLowercase = computed(() => /[a-z]/.test(form.value.password))
const hasNumber = computed(() => /\d/.test(form.value.password))
const hasSpecialChar = computed(() => /[!@#$%^&*(),.?":{}|<>]/.test(form.value.password))

const isPasswordValid = computed(() => {
  return (
    hasMinLength.value &&
    hasUppercase.value &&
    hasLowercase.value &&
    hasNumber.value &&
    hasSpecialChar.value
  )
})

const isFormValid = computed(() => {
  return (
    form.value.token.trim() !== '' &&
    isPasswordValid.value &&
    form.value.password === form.value.confirmPassword
  )
})

const validatePassword = (rule: any, value: string, callback: any) => {
  if (!value) {
    callback(new Error(t('activation.validation.passwordRequired')))
  } else if (!isPasswordValid.value) {
    callback(new Error(t('activation.validation.passwordInvalid')))
  } else {
    callback()
  }
}

const validateConfirmPassword = (rule: any, value: string, callback: any) => {
  if (!value) {
    callback(new Error(t('activation.validation.confirmPasswordRequired')))
  } else if (value !== form.value.password) {
    callback(new Error(t('activation.validation.passwordMismatch')))
  } else {
    callback()
  }
}

const rules: FormRules = {
  token: [{ required: true, message: t('activation.validation.tokenRequired'), trigger: 'blur' }],
  password: [{ validator: validatePassword, trigger: 'blur' }],
  confirmPassword: [{ validator: validateConfirmPassword, trigger: 'blur' }],
}

const handleSubmit = async () => {
  if (!formRef.value) return

  try {
    await formRef.value.validate()
  } catch {
    return
  }

  loading.value = true
  error.value = ''

  try {
    const result = await activateSupplierAccount({
      token: form.value.token,
      password: form.value.password,
    })

    activationResult.value = result
    activationSuccess.value = true
    notification.success(t('activation.success.message'))
  } catch (err: any) {
    console.error('Activation failed:', err)
    error.value = err.message || t('activation.error.failed')
  } finally {
    loading.value = false
  }
}

const goToLogin = () => {
  router.push('/login')
}

onMounted(() => {
  if (tokenFromUrl.value) {
    form.value.token = tokenFromUrl.value
  }
})




</script>

<style scoped>
.activation-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 2rem;
}

.activation-container {
  width: 100%;
  max-width: 500px;
}

.activation-card {
  background: white;
  border-radius: 12px;
  padding: 3rem;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.activation-header {
  text-align: center;
  margin-bottom: 2rem;
}

.activation-icon {
  color: #667eea;
  margin-bottom: 1rem;
}

.activation-header h1 {
  font-size: 1.75rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
  color: #1f2937;
}

.activation-subtitle {
  color: #6b7280;
  font-size: 0.95rem;
}

.activation-form {
  margin-top: 2rem;
}

.form-hint {
  font-size: 0.85rem;
  color: #10b981;
  margin-top: 0.5rem;
}

.password-requirements {
  margin-top: 0.75rem;
  padding: 0.75rem;
  background: #f9fafb;
  border-radius: 6px;
  font-size: 0.85rem;
}

.password-requirements p {
  font-weight: 500;
  margin-bottom: 0.5rem;
  color: #4b5563;
}

.password-requirements ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

.password-requirements li {
  padding: 0.25rem 0;
  color: #9ca3af;
  display: flex;
  align-items: center;
}

.password-requirements li::before {
  content: '○';
  margin-right: 0.5rem;
  font-weight: bold;
}

.password-requirements li.valid {
  color: #10b981;
}

.password-requirements li.valid::before {
  content: '✓';
}

.activation-error {
  margin-bottom: 1.5rem;
}

.activation-submit {
  width: 100%;
  height: 48px;
  font-size: 1rem;
  font-weight: 500;
}

.activation-success {
  text-align: center;
}

.success-details {
  margin: 1.5rem 0;
  padding: 1.5rem;
  background: #f0fdf4;
  border-radius: 8px;
  text-align: left;
}

.success-details p {
  margin: 0.5rem 0;
  color: #166534;
}
</style>
