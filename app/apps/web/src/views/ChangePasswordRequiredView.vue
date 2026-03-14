<template>
  <div class="required-password-page">
    <div class="required-password-backdrop" />
    <ChangePasswordDialog v-model="dialogVisible" required @success="handleSuccess" />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";

import ChangePasswordDialog from "@/components/ChangePasswordDialog.vue";
import { useAuthStore } from "@/stores/auth";
import { useNotification } from "@/composables";
import { getChangePasswordRequiredSuccessRedirect } from "@/views/changePasswordRequiredView";

defineOptions({ name: "ChangePasswordRequiredView" });

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const notification = useNotification();

const dialogVisible = computed({
  get: () => true,
  set: () => {
    // required flow cannot be dismissed
  },
});

const handleSuccess = async () => {
  await authStore.fetchMe();
  notification.success("Password changed successfully.");

  const redirect = getChangePasswordRequiredSuccessRedirect(
    typeof route.query.redirect === "string" ? route.query.redirect : null,
  );

  await router.replace(redirect);
};
</script>

<style scoped>
.required-password-page {
  position: relative;
  min-height: 100vh;
}

.required-password-backdrop {
  position: fixed;
  inset: 0;
  background: linear-gradient(135deg, #f4f6fb 0%, #e7ecf7 100%);
}
</style>
