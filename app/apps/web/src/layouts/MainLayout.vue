<template>
  <div class="main-layout">
    <AppHeader class="layout-header" />
    <div class="layout-container">
      <Sidebar :collapsed="sidebarCollapsed" @toggle="toggleSidebar" />
      <main class="main-content" :class="{ 'sidebar-collapsed': sidebarCollapsed }">
        <router-view v-slot="{ Component }">
          <keep-alive :include="['UnifiedDashboard']">
            <component :is="Component" />
          </keep-alive>
        </router-view>
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import AppHeader from "@/components/AppHeader.vue";
import Sidebar from "@/components/Sidebar.vue";

const sidebarCollapsed = ref(false);

const toggleSidebar = (collapsed: boolean) => {
  sidebarCollapsed.value = collapsed;
  localStorage.setItem("sidebarCollapsed", String(collapsed));
};

onMounted(() => {
  const saved = localStorage.getItem("sidebarCollapsed");
  if (saved !== null) {
    sidebarCollapsed.value = saved === "true";
  }
});
</script>

<style scoped>
.main-layout {
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
}

.layout-header {
  flex-shrink: 0;
  z-index: 1000;
}

.layout-container {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.main-content {
  flex: 1;
  overflow-y: auto;
  background-color: #f5f7fa;
  transition: margin-left 0.3s ease;
}

@media (max-width: 768px) {
  .layout-container {
    position: relative;
  }

  .main-content {
    margin-left: 0 !important;
  }
}
</style>
