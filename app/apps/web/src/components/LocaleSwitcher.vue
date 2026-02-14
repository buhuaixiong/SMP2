<template>
  <div class="locale-switcher">
    <select
      class="locale-select"
      :value="selectedLocale"
      :aria-label="t('common.language')"
      @change="handleChange"
    >
      <option v-for="option in options" :key="option.value" :value="option.value">
        {{ option.label }}
      </option>
    </select>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { SupportedLocale } from "@/i18n";
import { localeOptions } from "@/i18n";
import { useLocaleStore } from "@/stores/locale";

const { t } = useI18n();
const localeStore = useLocaleStore();

const options = localeOptions;

const selectedLocale = computed(() => localeStore.currentLocale);

const handleChange = async (event: Event) => {
  const target = event.target as HTMLSelectElement;
  const nextLocale = target.value as SupportedLocale;
  await localeStore.setLocale(nextLocale);
};
</script>

<style scoped>
.locale-switcher {
  display: inline-flex;
  align-items: center;
}

.locale-select {
  border: 1px solid #dcdfe6;
  border-radius: 999px;
  padding: 6px 12px;
  font-size: 0.85rem;
  background-color: rgba(255, 255, 255, 0.9);
  color: #303133;
  cursor: pointer;
  transition:
    border-color 0.2s ease,
    background-color 0.2s ease;
}

.locale-select:focus {
  outline: none;
  border-color: #5d5cde;
  background-color: #ffffff;
}
</style>
