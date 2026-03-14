<template>
  <article class="template-card">
    <header class="card-header">
      <div class="card-titles">
        <h2 class="card-title">{{ template.name }}</h2>
        <p v-if="template.code" class="card-code">{{ template.code }}</p>
      </div>
      <div v-if="$slots.actions" class="card-actions">
        <slot name="actions" :template="template" />
      </div>
    </header>

    <p v-if="template.description" class="card-description">
      {{ template.description }}
    </p>

    <slot :template="template" />
  </article>
</template>

<script setup lang="ts">
import { toRef } from "vue";

import type { TemplateDefinition } from "@/types";

defineOptions({ name: "TemplateCard" });

const props = defineProps<{
  template: TemplateDefinition;
}>();

const template = toRef(props, "template");
</script>

<style scoped>
.template-card {
  background: #ffffff;
  border-radius: 16px;
  border: 1px solid rgba(120, 140, 200, 0.14);
  box-shadow: 0 10px 28px rgba(41, 66, 160, 0.08);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.card-titles {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.card-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #1c1f2e;
}

.card-code {
  margin: 0;
  font-size: 12px;
  color: #7a7f99;
  background: #f2f4ff;
  padding: 4px 8px;
  border-radius: 999px;
  width: fit-content;
}

.card-actions {
  display: inline-flex;
  gap: 8px;
  align-items: center;
}

.card-description {
  margin: 0;
  color: #555a77;
  font-size: 14px;
  line-height: 1.6;
}
</style>
