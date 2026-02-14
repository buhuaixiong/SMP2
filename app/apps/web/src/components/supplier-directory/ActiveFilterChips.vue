<template>
  <div class="active-filter-chips" v-if="chips.length || showClear">
    <button
      v-for="chip in chips"
      :key="chip.key"
      class="chip"
      type="button"
      @click="$emit('remove', chip.key)"
    >
      {{ chip.label }} &times;
    </button>
    <button v-if="showClear" class="chip clear" type="button" @click="$emit('clear')">
      Clear all
    </button>
  </div>
</template>

<script setup lang="ts">
export interface ActiveChip {
  key: string;
  label: string;
}

withDefaults(
  defineProps<{
    chips: ActiveChip[];
    showClear?: boolean;
  }>(),
  {
    chips: () => [],
    showClear: false,
  },
);

defineEmits<{
  (event: "remove", key: string): void;
  (event: "clear"): void;
}>();
</script>

<style scoped>
.active-filter-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.chip {
  border: none;
  border-radius: 999px;
  padding: 0.35rem 0.75rem;
  background: #eef2ff;
  color: #3730a3;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background 0.2s ease;
}

.chip.clear {
  background: #fee2e2;
  color: #b91c1c;
}

.chip:hover {
  background: #c7d2fe;
}
</style>
