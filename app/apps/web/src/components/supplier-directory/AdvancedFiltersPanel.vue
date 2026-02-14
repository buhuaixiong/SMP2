<template>
  <section class="advanced-filters">
    <label>
      <span>Stage</span>
      <select :value="stage" @change="onStage">
        <option value="">All</option>
        <option v-for="option in stageOptions" :key="option.value" :value="option.value">
          {{ option.label }}
        </option>
      </select>
    </label>
    <label>
      <span>Category</span>
      <select :value="category" @change="onCategory">
        <option value="">All</option>
        <option v-for="option in categoryOptions" :key="option" :value="option">
          {{ option }}
        </option>
      </select>
    </label>
    <label>
      <span>Region</span>
      <select :value="region" @change="onRegion">
        <option value="">All</option>
        <option v-for="option in regionOptions" :key="option" :value="option">
          {{ option }}
        </option>
      </select>
    </label>
    <label>
      <span>Importance</span>
      <select :value="importance" @change="onImportance">
        <option value="">All</option>
        <option v-for="option in importanceOptions" :key="option" :value="option">
          {{ option }}
        </option>
      </select>
    </label>
    <label>
      <span>Tag</span>
      <select :value="tag" @change="onTag">
        <option value="">All</option>
        <option v-for="option in tagOptions" :key="option" :value="option">
          {{ option }}
        </option>
      </select>
    </label>
  </section>
</template>

<script setup lang="ts">
interface OptionPair {
  value: string;
  label: string;
}

const props = defineProps<{
  stage: string;
  category: string;
  region: string;
  importance: string;
  tag: string;
  stageOptions: OptionPair[];
  categoryOptions: string[];
  regionOptions: string[];
  importanceOptions: string[];
  tagOptions: string[];
}>();

const emit = defineEmits<{
  (event: "update:stage", value: string): void;
  (event: "update:category", value: string): void;
  (event: "update:region", value: string): void;
  (event: "update:importance", value: string): void;
  (event: "update:tag", value: string): void;
}>();

const onStage = (event: Event) => emit("update:stage", (event.target as HTMLSelectElement).value);
const onCategory = (event: Event) =>
  emit("update:category", (event.target as HTMLSelectElement).value);
const onRegion = (event: Event) => emit("update:region", (event.target as HTMLSelectElement).value);
const onImportance = (event: Event) =>
  emit("update:importance", (event.target as HTMLSelectElement).value);
const onTag = (event: Event) => emit("update:tag", (event.target as HTMLSelectElement).value);
</script>

<style scoped>
.advanced-filters {
  margin-top: 0.75rem;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.75rem;
}

label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  font-size: 0.85rem;
  color: #374151;
}

select {
  padding: 0.5rem 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 0.95rem;
}
</style>
