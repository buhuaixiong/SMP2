<template>
  <header class="page-header" :class="[`page-header--${align}`]">
    <div class="page-header__main">
      <slot name="title">
        <h1 v-if="title">{{ title }}</h1>
      </slot>
      <slot name="subtitle">
        <p v-if="subtitle" class="page-header__subtitle">{{ subtitle }}</p>
      </slot>
    </div>
    <div v-if="$slots.actions" class="page-header__actions">
      <slot name="actions" />
    </div>
  </header>
</template>

<script setup lang="ts">
defineOptions({ name: "PageHeader" });

withDefaults(
  defineProps<{
    title?: string;
    subtitle?: string;
    align?: "start" | "center" | "end";
  }>(),
  {
    title: undefined,
    subtitle: undefined,
    align: "start",
  },
);

defineSlots<{
  title?: () => any;
  subtitle?: () => any;
  actions?: () => any;
}>();
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
}

.page-header--center {
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.page-header--end {
  flex-direction: row;
  justify-content: space-between;
  text-align: right;
}

.page-header__main {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.page-header__actions {
  display: inline-flex;
  gap: 8px;
  align-items: center;
}

.page-header h1 {
  margin: 0;
  font-size: 26px;
  font-weight: 600;
  color: #1f2633;
}

.page-header__subtitle {
  margin: 0;
  color: #5f6278;
  font-size: 14px;
}

@media (max-width: 768px) {
  .page-header {
    flex-direction: column;
    align-items: stretch;
  }
  .page-header__actions {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
