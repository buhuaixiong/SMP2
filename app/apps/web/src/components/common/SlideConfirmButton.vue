<template>
  <div class="slide-confirm-button" :class="{ 'is-confirmed': isConfirmed }">
    <div class="slide-track" :style="{ background: trackColor }">
      <div
        class="slide-button"
        :style="{ transform: `translateX(${slidePosition}px)`, background: buttonColor }"
        @mousedown="startSlide"
        @touchstart="startSlide"
      >
        <el-icon v-if="!isConfirmed"><DArrowRight /></el-icon>
        <el-icon v-else><Check /></el-icon>
      </div>
      <div class="slide-text" :class="{ 'is-sliding': isSliding }">
        {{ isConfirmed ? confirmedText : slideText }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { DArrowRight, Check } from '@element-plus/icons-vue'

interface Props {
  slideText?: string
  confirmedText?: string
  trackColor?: string
  buttonColor?: string
  confirmThreshold?: number // 滑动距离阈值（百分比）
}

const props = withDefaults(defineProps<Props>(), {
  slideText: '滑动确认',
  confirmedText: '已确认',
  trackColor: '#f56c6c',
  buttonColor: '#fff',
  confirmThreshold: 80,
})

const emit = defineEmits<{
  confirm: []
}>()

const isSliding = ref(false)
const isConfirmed = ref(false)
const slidePosition = ref(0)
const startX = ref(0)
const trackWidth = ref(0)

const maxSlideDistance = computed(() => {
  return trackWidth.value > 0 ? trackWidth.value - 50 : 200
})

const startSlide = (event: MouseEvent | TouchEvent) => {
  if (isConfirmed.value) return

  isSliding.value = true
  const clientX = event instanceof MouseEvent ? event.clientX : event.touches[0].clientX
  startX.value = clientX - slidePosition.value

  // 获取轨道宽度
  const track = (event.target as HTMLElement).closest('.slide-track')
  if (track) {
    trackWidth.value = track.clientWidth
  }

  event.preventDefault()
}

const handleSlide = (event: MouseEvent | TouchEvent) => {
  if (!isSliding.value) return

  const clientX = event instanceof MouseEvent ? event.clientX : event.touches[0].clientX
  let newPosition = clientX - startX.value

  // 限制滑动范围
  newPosition = Math.max(0, Math.min(newPosition, maxSlideDistance.value))
  slidePosition.value = newPosition
}

const endSlide = () => {
  if (!isSliding.value) return

  isSliding.value = false

  // 检查是否达到确认阈值
  const slidePercentage = (slidePosition.value / maxSlideDistance.value) * 100

  if (slidePercentage >= props.confirmThreshold) {
    // 确认成功
    isConfirmed.value = true
    slidePosition.value = maxSlideDistance.value

    // 延迟触发确认事件
    setTimeout(() => {
      emit('confirm')

      // 重置状态
      setTimeout(() => {
        reset()
      }, 1000)
    }, 300)
  } else {
    // 滑动距离不够，回弹
    slidePosition.value = 0
  }

}

const reset = () => {
  isConfirmed.value = false
  slidePosition.value = 0
}

const addGlobalListeners = () => {
  if (typeof window === 'undefined') return
  window.addEventListener('mousemove', handleSlide)
  window.addEventListener('mouseup', endSlide)
  window.addEventListener('touchmove', handleSlide)
  window.addEventListener('touchend', endSlide)
}

const removeGlobalListeners = () => {
  if (typeof window === 'undefined') return
  window.removeEventListener('mousemove', handleSlide)
  window.removeEventListener('mouseup', endSlide)
  window.removeEventListener('touchmove', handleSlide)
  window.removeEventListener('touchend', endSlide)
}

defineExpose({
  reset,
})

onMounted(async () => {
  await nextTick()
  addGlobalListeners()
})

onUnmounted(() => {
  removeGlobalListeners()
})
</script>

<style scoped lang="scss">
.slide-confirm-button {
  width: 100%;
  user-select: none;

  .slide-track {
    position: relative;
    height: 50px;
    border-radius: 25px;
    overflow: hidden;
    cursor: pointer;
    transition: background 0.3s;
  }

  .slide-button {
    position: absolute;
    left: 5px;
    top: 5px;
    width: 40px;
    height: 40px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: grab;
    transition: transform 0.2s ease-out, background 0.3s;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
    z-index: 2;

    &:active {
      cursor: grabbing;
    }

    .el-icon {
      font-size: 20px;
      color: #f56c6c;
    }
  }

  .slide-text {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #fff;
    font-weight: 500;
    font-size: 14px;
    pointer-events: none;
    transition: opacity 0.2s;
    z-index: 1;

    &.is-sliding {
      opacity: 0.6;
    }
  }

  &.is-confirmed {
    .slide-track {
      background: #67c23a !important;
    }

    .slide-button {
      .el-icon {
        color: #67c23a;
      }
    }
  }
}
</style>
