<template>
  <el-button
    v-bind="$attrs"
    :type="isConfirming ? confirmType : type"
    :loading="loading"
    :disabled="disabled"
    @click="handleClick"
    :class="{ 'is-confirming': isConfirming }"
  >
    <template v-if="isConfirming">
      <el-icon><Warning /></el-icon>
      <span>{{ confirmText || $t('common.clickAgainToConfirm') }}</span>
    </template>
    <template v-else>
      <slot>{{ text }}</slot>
    </template>
  </el-button>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Warning } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'

interface Props {
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'text' | 'default'
  confirmType?: 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'text' | 'default'
  text?: string
  confirmText?: string
  loading?: boolean
  disabled?: boolean
  timeout?: number // 自动取消确认状态的超时时间（毫秒）
}

const props = withDefaults(defineProps<Props>(), {
  type: 'primary',
  confirmType: 'danger',
  timeout: 3000,
})

const emit = defineEmits<{
  confirm: []
}>()

const { t } = useI18n()

const isConfirming = ref(false)
let timeoutId: ReturnType<typeof setTimeout> | null = null

const handleClick = () => {
  if (props.disabled || props.loading) return

  if (!isConfirming.value) {
    // 第一次点击：进入确认状态
    isConfirming.value = true

    // 设置超时自动取消
    if (timeoutId) clearTimeout(timeoutId)
    timeoutId = setTimeout(() => {
      isConfirming.value = false
    }, props.timeout)
  } else {
    // 第二次点击：执行操作
    if (timeoutId) clearTimeout(timeoutId)
    isConfirming.value = false
    emit('confirm')
  }
}

// 暴露重置方法给父组件
defineExpose({
  reset: () => {
    if (timeoutId) clearTimeout(timeoutId)
    isConfirming.value = false
  }
})
</script>

<style scoped lang="scss">
.is-confirming {
  animation: pulse 0.5s ease-in-out;
}

@keyframes pulse {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.05);
  }
}
</style>
