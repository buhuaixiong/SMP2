<template>
  <div class="summary-cards">
    <div class="card card-total">
      <div class="card-icon">📊</div>
      <div class="card-content">
        <div class="card-value">{{ totalCount }}</div>
        <div class="card-label">Total Suppliers</div>
      </div>
    </div>

    <button
      type="button"
      class="card card-attention"
      :class="{ active: needsAttentionActive }"
      @click="$emit('filter-needs-attention')"
    >
      <div class="card-icon">⚠️</div>
      <div class="card-content">
        <div class="card-value">{{ needsAttentionCount }}</div>
        <div class="card-label">Needs Attention</div>
      </div>
    </button>

    <button
      type="button"
      class="card card-pending"
      :class="{ active: pendingApprovalsActive }"
      @click="$emit('filter-pending')"
    >
      <div class="card-icon">⏳</div>
      <div class="card-content">
        <div class="card-value">{{ pendingApprovalsCount }}</div>
        <div class="card-label">Pending Approvals</div>
      </div>
    </button>

    <button
      type="button"
      class="card card-priority"
      :class="{ active: highPriorityActive }"
      @click="$emit('filter-high-priority')"
    >
      <div class="card-icon">🔥</div>
      <div class="card-content">
        <div class="card-value">{{ highPriorityCount }}</div>
        <div class="card-label">High Priority</div>
      </div>
    </button>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    totalCount: number;
    needsAttentionCount: number;
    pendingApprovalsCount: number;
    highPriorityCount: number;
    needsAttentionActive?: boolean;
    pendingApprovalsActive?: boolean;
    highPriorityActive?: boolean;
  }>(),
  {
    needsAttentionActive: false,
    pendingApprovalsActive: false,
    highPriorityActive: false,
  },
);

const emit = defineEmits<{
  (event: "filter-needs-attention"): void;
  (event: "filter-pending"): void;
  (event: "filter-high-priority"): void;
}>();
</script>

<style scoped>
.summary-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.card {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
  border-radius: 12px;
  border: 1px solid #e5e7eb;
  background: #ffffff;
  transition: all 0.2s ease;
}

.card.card-total {
  cursor: default;
}

button.card {
  cursor: pointer;
  text-align: left;
  width: 100%;
}

button.card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

button.card.active {
  border-color: #6366f1;
  background: #eef2ff;
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

.card-icon {
  font-size: 2rem;
  line-height: 1;
}

.card-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.card-value {
  font-size: 1.75rem;
  font-weight: 700;
  line-height: 1;
  color: #111827;
}

.card-label {
  font-size: 0.875rem;
  font-weight: 500;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.025em;
}

.card-total {
  border-color: #d1d5db;
}

.card-total .card-value {
  color: #374151;
}

.card-attention {
  border-color: #fecaca;
}

.card-attention .card-value {
  color: #dc2626;
}

.card-pending {
  border-color: #bfdbfe;
}

.card-pending .card-value {
  color: #2563eb;
}

.card-priority {
  border-color: #fed7aa;
}

.card-priority .card-value {
  color: #ea580c;
}

@media (max-width: 768px) {
  .summary-cards {
    grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  }

  .card {
    padding: 1rem;
  }

  .card-icon {
    font-size: 1.5rem;
  }

  .card-value {
    font-size: 1.5rem;
  }

  .card-label {
    font-size: 0.75rem;
  }
}
</style>
