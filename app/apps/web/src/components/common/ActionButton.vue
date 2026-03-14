<template>
  <div class="action-button-wrapper">
    <el-button
      v-bind="$attrs"
      :type="type"
      :loading="loading"
      :disabled="disabled"
      @click="handleClick"
    >
      <slot>{{ text }}</slot>
    </el-button>

    <!-- 操作提示消息 -->
    <el-alert
      v-if="showMessage && message"
      :type="messageType"
      :closable="true"
      show-icon
      class="action-message"
      @close="showMessage = false"
    >
      {{ message }}
    </el-alert>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

interface Props {
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'text' | 'default'
  text?: string
  loading?: boolean
  disabled?: boolean
  successMessage?: string
  errorMessage?: string
  autoExecute?: boolean // 是否直接执行而不需要二次确认
}

const props = withDefaults(defineProps<Props>(), {
  type: 'primary',
  autoExecute: true,
})

const emit = defineEmits<{
  click: []
  success: []
  error: [error: Error]
}>()

const loading = ref(false)
const showMessage = ref(false)
const message = ref('')
const messageType = ref<'success' | 'error' | 'warning' | 'info'>('success')

const handleClick = async () => {
  if (props.disabled || loading.value) return

  showMessage.value = false
  emit('click')
}

// 暴露方法供父组件调用
const showSuccess = (msg?: string) => {
  message.value = msg || props.successMessage || '操作成功'
  messageType.value = 'success'
  showMessage.value = true

  // 3秒后自动隐藏
  setTimeout(() => {
    showMessage.value = false
  }, 3000)
}

const showError = (msg?: string) => {
  message.value = msg || props.errorMessage || '操作失败'
  messageType.value = 'error'
  showMessage.value = true
}

defineExpose({
  showSuccess,
  showError,
})
</script>

<style scoped lang="scss">
.action-button-wrapper {
  display: inline-block;
  position: relative;

  .action-message {
    position: absolute;
    top: 100%;
    left: 0;
    margin-top: 8px;
    min-width: 200px;
    z-index: 10;
    animation: slideDown 0.3s ease-out;
  }
}

@keyframes slideDown {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
