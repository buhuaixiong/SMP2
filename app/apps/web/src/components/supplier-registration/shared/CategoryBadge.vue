<template>
  <span
    v-if="show"
    class="category-badge"
    :title="tooltip"
  >
    {{ label }}
  </span>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

defineOptions({ name: "CategoryBadge" });

const props = withDefaults(
  defineProps<{
    show: boolean;
    label?: string;
    tooltip?: string;
  }>(),
  {
    label: "Category A",
    tooltip: undefined,
  }
);

const { t } = useI18n();

const computedTooltip = computed(() => {
  return props.tooltip || t("supplierRegistration.categoryA.tooltip", "Editing this field after registration will trigger full approval chain");
});
</script>

<style scoped>
.category-badge {
  display: inline-block;
  margin-left: 8px;
  padding: 2px 8px;
  font-size: 11px;
  font-weight: 600;
  color: #e6a23c;
  background-color: #fdf6ec;
  border: 1px solid #f5dab1;
  border-radius: 4px;
  cursor: help;
  vertical-align: middle;
}

.category-badge:hover {
  background-color: #faecd8;
}
</style>
