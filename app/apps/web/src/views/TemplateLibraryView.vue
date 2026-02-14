<template>
  <div class="template-library">
    <PageHeader
      :title="t('templateLibrary.pageTitle')"
      :subtitle="t('templateLibrary.pageDescription')"
    />

    <section v-if="loading" class="panel">{{ t("common.loading") }}</section>

    <section v-else class="template-grid">
      <TemplateCard v-for="item in templates" :key="item.code" :template="item">
        <TemplateFileInfo
          v-if="item.file"
          :file="item.file"
          :label="t('templateLibrary.admin.currentVersion')"
          :updated-at-label="t('templateLibrary.lastUpload')"
          :uploaded-by-label="t('common.uploadedBy')"
        >
          <template #actions="{ file }">
            <a
              v-if="file"
              :href="file.downloadUrl"
              class="primary-btn"
              target="_blank"
              rel="noopener"
            >
              {{ t("common.download") }}
            </a>
          </template>
        </TemplateFileInfo>
        <p v-else class="no-file">{{ t("templateLibrary.noTemplateUploaded") }}</p>
      </TemplateCard>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";

import PageHeader from "@/components/layout/PageHeader.vue";
import TemplateCard from "@/components/template-library/TemplateCard.vue";
import TemplateFileInfo from "@/components/template-library/TemplateFileInfo.vue";
import { useTemplateLibraryStore } from "@/stores/templateLibrary";

defineOptions({ name: "TemplateLibraryView" });

const { t } = useI18n();
const store = useTemplateLibraryStore();

const templates = computed(() => store.templates);
const loading = computed(() => store.loading);

onMounted(() => {
  if (!store.templates.length) {
    store.loadTemplates().catch((error) => {
      console.error(t("templateLibrary.notifications.loadFailure"), error);
    });
  }
});
</script>

<style scoped>
.template-library {
  display: flex;
  flex-direction: column;
  gap: 24px;
  padding: 24px;
}

.template-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 20px;
}

.primary-btn {
  align-self: flex-start;
  background: #4451f2;
  color: #ffffff;
  padding: 8px 16px;
  border-radius: 10px;
  text-decoration: none;
  font-size: 14px;
}

.primary-btn:hover {
  background: #3844d8;
}

.no-file {
  margin: 0;
  color: #a0a3b8;
  font-size: 13px;
}

.panel {
  background: #ffffff;
  border-radius: 16px;
  padding: 20px;
  border: 1px solid rgba(120, 140, 200, 0.14);
  box-shadow: 0 10px 28px rgba(41, 66, 160, 0.08);
}

@media (max-width: 768px) {
  .template-library {
    padding: 16px;
  }
  .template-grid {
    grid-template-columns: 1fr;
  }
}
</style>
