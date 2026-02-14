<template>
  <el-card :class="['todo-card', `todo-card--${priority}`]" @click="$emit('click')">
    <div class="card-content">
      <div class="card-icon">
        <el-icon :size="32">
          <component :is="iconComponent" />
        </el-icon>
      </div>
      <div class="card-info">
        <div class="card-count">{{ count }}</div>
        <div class="card-title">{{ title }}</div>
      </div>
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { computed } from "vue";
import {
  User,
  FolderOpened,
  Memo,
  Clock,
  Document,
  Edit,
  CircleCheck,
  Tickets,
  Warning,
} from "@element-plus/icons-vue";

interface Props {
  title: string;
  count: number;
  icon?: string;
  priority?: "high" | "warning" | "info" | "success";
}

const props = withDefaults(defineProps<Props>(), {
  icon: "Document",
  priority: "info",
});

defineEmits<{
  (e: "click"): void;
}>();

const iconComponent = computed(() => {
  const iconMap: Record<string, any> = {
    User,
    FolderOpened,
    Memo,
    Clock,
    Document,
    Edit,
    CircleCheck,
    Tickets,
    Warning,
  };
  return iconMap[props.icon] || Document;
});
</script>

<style scoped>
.todo-card {
  cursor: pointer;
  transition: all 0.3s ease;
  border-radius: 8px;
}

.todo-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.todo-card--high {
  border-left: 4px solid #ff4d4f;
}

.todo-card--high .card-icon {
  background: #fff1f0;
  color: #ff4d4f;
}

.todo-card--warning {
  border-left: 4px solid #faad14;
}

.todo-card--warning .card-icon {
  background: #fffbe6;
  color: #faad14;
}

.todo-card--info {
  border-left: 4px solid #1890ff;
}

.todo-card--info .card-icon {
  background: #e6f7ff;
  color: #1890ff;
}

.todo-card--success {
  border-left: 4px solid #52c41a;
}

.todo-card--success .card-icon {
  background: #f6ffed;
  color: #52c41a;
}

.card-content {
  display: flex;
  align-items: center;
  gap: 16px;
}

.card-icon {
  width: 56px;
  height: 56px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.card-info {
  flex: 1;
  min-width: 0;
}

.card-count {
  font-size: 28px;
  font-weight: 600;
  color: #303133;
  line-height: 1;
  margin-bottom: 8px;
}

.card-title {
  font-size: 14px;
  color: #606266;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

@media (max-width: 768px) {
  .card-content {
    gap: 12px;
  }

  .card-icon {
    width: 48px;
    height: 48px;
  }

  .card-icon :deep(.el-icon) {
    font-size: 24px;
  }

  .card-count {
    font-size: 24px;
  }

  .card-title {
    font-size: 13px;
  }
}
</style>
