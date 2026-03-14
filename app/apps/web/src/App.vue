<template>
  <el-config-provider :locale="elementLocale">
    <div id="app">
      <LockdownAnnouncementDialog />
      <template v-if="isLoginPage">
        <RouterView />
      </template>
      <template v-else>
        <LockdownBanner />
        <MainLayout />
      </template>
    </div>
  </el-config-provider>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted } from "vue";
import { useRoute } from "vue-router";
import { useLocaleStore } from "./stores/locale";
import { useLockdownStore } from "./stores/lockdown";
import MainLayout from "./layouts/MainLayout.vue";
import LockdownBanner from "./components/LockdownBanner.vue";
import LockdownAnnouncementDialog from "./components/LockdownAnnouncementDialog.vue";
import en from "element-plus/es/locale/lang/en";
import zhCn from "element-plus/es/locale/lang/zh-cn";
import th from "element-plus/es/locale/lang/th";

const route = useRoute();
const localeStore = useLocaleStore();
const lockdownStore = useLockdownStore();
const isLoginPage = computed(() => route.name === "login");

const elementLocaleMap = {
  en: en,
  zh: zhCn,
  th: th,
};

const elementLocale = computed(() => {
  return elementLocaleMap[localeStore.currentLocale] || en;
});

onMounted(() => {
  lockdownStore.startPolling();
});

onBeforeUnmount(() => {
  lockdownStore.stopPolling();
});
</script>

