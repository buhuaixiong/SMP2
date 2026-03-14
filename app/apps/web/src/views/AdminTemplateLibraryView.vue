<template>
  <div class="admin-template-library">
    <PageHeader
      :title="$t('templateLibrary.admin.title')"
      :subtitle="$t('templateLibrary.admin.subtitle')"
    />

    <section v-if="loading" class="panel">{{ $t("templateLibrary.admin.loading") }}</section>

    <section v-else class="template-grid">
      <TemplateCard v-for="item in templates" :key="item.code" :template="item">
        <template #actions>
          <button type="button" class="link-btn" @click="toggleHistory(item.code)">
            {{
              historyExpanded[item.code]
                ? $t("templateLibrary.admin.hideHistory")
                : $t("templateLibrary.admin.showHistory")
            }}
          </button>
        </template>

        <TemplateFileInfo
          :file="item.file"
          :label="$t('templateLibrary.admin.currentVersion')"
          :empty-text="$t('templateLibrary.admin.noFileUploaded')"
          :updated-at-label="$t('templateLibrary.admin.updatedAt')"
          :uploaded-by-label="$t('common.uploadedBy')"
        />

        <TemplateUploadPanel
          :template-code="item.code"
          :loading="uploading"
          :uploading-code="uploadingCode"
          :uploading-label="$t('templateLibrary.admin.uploading')"
          :idle-label="$t('templateLibrary.admin.uploadNewVersion')"
          :hint="$t('templateLibrary.admin.uploadHint')"
          :error="errors[item.code]"
          @file-selected="onFileSelected"
        />

        <transition name="fade">
          <TemplateHistoryList
            v-if="historyExpanded[item.code]"
            :records="history[item.code] ?? []"
            :loading="historyLoading[item.code]"
            :loading-text="$t('templateLibrary.admin.historyLoading')"
            :empty-text="$t('templateLibrary.admin.noHistory')"
            :active-label="$t('templateLibrary.admin.active')"
          />
        </transition>
      </TemplateCard>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, defineAsyncComponent } from "vue";
import { useI18n } from "vue-i18n";

import PageHeader from "@/components/layout/PageHeader.vue";
import TemplateCard from "@/components/template-library/TemplateCard.vue";
import { useTemplateLibraryStore } from "@/stores/templateLibrary";

defineOptions({ name: "AdminTemplateLibraryView" });

const { t } = useI18n();
const store = useTemplateLibraryStore();

const TemplateFileInfo = defineAsyncComponent(
  () => import("@/components/template-library/TemplateFileInfo.vue"),
);
const TemplateHistoryList = defineAsyncComponent(
  () => import("@/components/template-library/TemplateHistoryList.vue"),
);
const TemplateUploadPanel = defineAsyncComponent(
  () => import("@/components/template-library/TemplateUploadPanel.vue"),
);

const templates = computed(() => store.templates);
const loading = computed(() => store.loading);
const uploading = computed(() => store.uploading);
const history = computed(() => store.history);
const historyLoading = store.historyLoading;

const historyExpanded = reactive<Record<string, boolean>>({});
const uploadingCode = ref<string | null>(null);
const errors = reactive<Record<string, string | null>>({});

const ensureHistoryLoaded = async (code: string) => {
  if (history.value[code]) {
    return;
  }
  try {
    await store.loadHistory(code);
  } catch (error) {
    console.error(t("templateLibrary.admin.errors.historyLoadFailed"), error);
    errors[code] = (error as Error)?.message || t("templateLibrary.admin.errors.historyLoadFailed");
  }
};

const toggleHistory = async (code: string) => {
  historyExpanded[code] = !historyExpanded[code];
  if (historyExpanded[code]) {
    await ensureHistoryLoaded(code);
  }
};

const onFileSelected = async ({ code, file }: { code: string; file: File }) => {
  if (!file) {
    return;
  }
  errors[code] = null;
  uploadingCode.value = code;
  try {
    await store.uploadTemplate(code, file);
    await ensureHistoryLoaded(code);
  } catch (error) {
    console.error(t("templateLibrary.admin.errors.uploadFailed"), error);
    errors[code] = (error as Error)?.message || t("templateLibrary.admin.errors.uploadFailed");
  } finally {
    uploadingCode.value = null;
  }
};

onMounted(() => {
  if (!store.templates.length) {
    store.loadTemplates().catch((error) => {
      console.error(t("templateLibrary.notifications.loadFailure"), error);
    });
  }
});
</script>

<style scoped>
.admin-template-library {
  display: flex;
  flex-direction: column;
  gap: 24px;
  padding: 24px;
}

.template-grid {
  display: grid;
  gap: 20px;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
}
.link-btn {
  background: transparent;
  border: none;
  color: #4451f2;
  cursor: pointer;
  font-size: 14px;
  padding: 0;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.18s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.panel {
  background: #ffffff;
  border-radius: 16px;
  padding: 20px;
  border: 1px solid rgba(120, 140, 200, 0.14);
  box-shadow: 0 10px 28px rgba(41, 66, 160, 0.08);
}

@media (max-width: 768px) {
  .admin-template-library {
    padding: 16px;
  }
  .template-grid {
    grid-template-columns: 1fr;
  }
}
</style>
