<template>
  <section class="system-modules">
    <header class="panel-header">
      <h2>{{ t("systemModules.pageTitle") }}</h2>
      <p>{{ t("systemModules.subtitle") }}</p>
    </header>
    <div class="module-grid">
      <article v-for="module in modules" :key="module.key" class="module-card">
        <h3 class="module-title">{{ t(`systemModules.modules.${module.key}.name`) }}</h3>
        <ul class="feature-list">
          <li
            v-for="(feature, index) in t(`systemModules.modules.${module.key}.features`)"
            :key="index"
            class="feature-item"
          >
            {{ feature }}
          </li>
        </ul>
      </article>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const modules = [
  { key: "userManagement" },
  { key: "supplierManagement" },
  { key: "documentManagement" },
  { key: "contractManagement" },
  { key: "rfqProcess" },
  { key: "financeInvoice" },
  { key: "ratingEvaluation" },
];

defineOptions({ name: "SystemModulesPanel" });
</script>

<style scoped>
.system-modules {
  background: white;
  border-radius: 12px;
  padding: 32px;
  margin-bottom: 24px;
  box-shadow: 0 18px 45px rgba(33, 41, 72, 0.08);
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.panel-header h2 {
  margin: 0 0 4px 0;
  font-size: 22px;
  color: #303133;
  font-weight: 700;
}

.panel-header p {
  margin: 0;
  color: #606266;
  font-size: 14px;
}

.module-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
}

.module-card {
  border: 1px solid #e4e7ed;
  border-radius: 10px;
  padding: 20px;
  background: linear-gradient(180deg, #ffffff 0%, #f9fbff 100%);
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease,
    transform 0.2s ease;
}

.module-card:hover {
  border-color: #c0c4cc;
  box-shadow: 0 12px 30px rgba(93, 92, 222, 0.15);
  transform: translateY(-2px);
}

.module-title {
  margin: 0 0 12px 0;
  font-size: 16px;
  font-weight: 600;
  color: #4545a5;
}

.feature-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.feature-item {
  position: relative;
  padding-left: 18px;
  font-size: 14px;
  color: #4c4d63;
}

.feature-item::before {
  content: "";
  position: absolute;
  left: 0;
  top: 8px;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #5d5cde;
}
</style>
