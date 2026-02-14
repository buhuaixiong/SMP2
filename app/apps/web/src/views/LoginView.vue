<template>
  <div class="login-container">
    <LocaleSwitcher class="login-locale-switcher" />
    <div class="login-card">
      <div class="login-header">
        <img :src="brandImage" :alt="t('auth.brandAlt')" class="login-logo" />
        <p>{{ t("auth.prompt") }}</p>
      </div>

      <form class="login-form" @submit.prevent="handleLogin">
        <div class="form-item">
          <label class="form-label" for="username">{{ t("auth.usernameLabel") }}</label>
          <el-input
            id="username"
            v-model.trim="loginForm.username"
            autocomplete="username"
            :placeholder="t('registration.login.placeholder')"
            class="form-input"
            required
          >
            <template #prepend>
              <i class="el-icon-user" />
            </template>
          </el-input>
          <div class="form-helper">
            <i class="el-icon-info" />
            <span>{{ t("auth.usernameHelper") }}</span>
          </div>
          <p class="login-credential-hint">
            {{ t("registration.login.helper") }}
          </p>
        </div>

        <div class="form-item">
          <label class="form-label" for="password">{{ t("auth.passwordLabel") }}</label>
          <input
            id="password"
            v-model="loginForm.password"
            type="password"
            autocomplete="current-password"
            :placeholder="t('auth.passwordPlaceholder')"
            class="form-input"
            required
            @keyup.enter="handleLogin"
          />
        </div>

        <button class="login-btn" :disabled="loading" type="submit">
          {{ loading ? t("auth.signingIn") : t("auth.signIn") }}
        </button>
      </form>

      <div class="login-footer">
        <RouterLink class="register-link" to="/supplier-registration">
          {{ t("auth.registerCta") }}
        </RouterLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from "vue";
import { useRouter, useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import { useAuthStore } from "@/stores/auth";
import { useNotification } from "@/composables";

defineOptions({ name: "LoginView" });

const { t } = useI18n();
const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const notification = useNotification();

const loading = ref(false);
const loginForm = reactive({ username: "", password: "" });
const brandImage = new URL("../assets/login-logo.png", import.meta.url).href;

const handleLogin = async () => {
  if (loading.value) return;
  if (!loginForm.username || !loginForm.password) {
    notification.warning(t("auth.validation.missingCredentials"));
    return;
  }

  try {
    loading.value = true;
    await authStore.login({ username: loginForm.username.trim(), password: loginForm.password });
    notification.success(t("auth.notifications.success"));
    router.replace((route.query.redirect as string) || "/");
  } catch (error) {
    notification.error(error instanceof Error && error.message ? error.message : t("auth.notifications.failure"));
  } finally {
    loginForm.password = "";
    loading.value = false;
  }
};
</script>

<style scoped>
.login-container {
  position: relative;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}

.login-locale-switcher {
  position: absolute;
  top: 20px;
  right: 20px;
}

.login-locale-switcher :deep(.locale-select) {
  background: rgba(255, 255, 255, 0.95);
  border-color: rgba(255, 255, 255, 0.9);
  color: #303133;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
}

.login-card {
  background: white;
  border-radius: 16px;
  padding: 40px;
  width: 100%;
  max-width: 460px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.1);
}

.login-header {
  text-align: center;
  margin-bottom: 24px;
}

.login-logo {
  width: 100%;
  max-width: 320px;
  margin: 0 auto 12px auto;
  display: block;
}

.login-header p {
  color: #909399;
  margin: 0;
  font-size: 14px;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 13px;
  color: #606266;
}

.form-helper {
  margin: 0;
  font-size: 12px;
  color: #909399;
  display: flex;
  align-items: center;
  gap: 4px;
}

.login-credential-hint {
  margin: 6px 0 0;
  font-size: 12px;
  color: #606266;
}

.form-input {
  padding: 12px 16px;
  border: 2px solid #e4e7ed;
  border-radius: 8px;
  font-size: 14px;
  transition: border-color 0.3s;
}

:deep(.form-input .el-input__inner) {
  padding: 12px 16px;
  border: 2px solid #e4e7ed;
  border-radius: 8px;
  font-size: 14px;
  transition: border-color 0.3s;
}

.form-input:focus {
  outline: none;
  border-color: #5d5cde;
}

:deep(.form-input .el-input__inner:focus) {
  border-color: #5d5cde;
}

.login-btn {
  padding: 12px;
  background: #5d5cde;
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 16px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.3s;
  margin-top: 16px;
}

.login-btn:hover {
  background: #4a4bc9;
}

.login-btn:disabled {
  background: #ccc;
  cursor: not-allowed;
}

.login-footer {
  margin-top: 20px;
  text-align: center;
}

.register-link {
  color: #5d5cde;
  font-size: 13px;
  text-decoration: none;
}

.register-link:hover {
  text-decoration: underline;
}
</style>
