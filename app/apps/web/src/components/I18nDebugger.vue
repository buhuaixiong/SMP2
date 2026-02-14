<template>
  <el-card class="i18n-debugger" v-if="visible">
    <template #header>
      <div class="debugger-header">
        <h3>🔧 I18N 调试工具</h3>
        <el-button size="small" @click="visible = false">关闭</el-button>
      </div>
    </template>

    <el-tabs v-model="activeTab">
      <!-- 状态检查 -->
      <el-tab-pane label="状态检查" name="status">
        <div class="debug-section">
          <h4>当前配置</h4>
          <el-descriptions :column="1" border size="small">
            <el-descriptions-item label="当前语言">
              <el-tag :type="currentLocale === 'zh' ? 'success' : 'warning'">
                {{ currentLocale }} ({{ getLocaleName(currentLocale) }})
              </el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="回退语言">
              {{ fallbackLocale }}
            </el-descriptions-item>
            <el-descriptions-item label="可用语言">
              {{ availableLocales.join(', ') }}
            </el-descriptions-item>
            <el-descriptions-item label="localStorage">
              {{ storedLocale || '(未设置)' }}
            </el-descriptions-item>
            <el-descriptions-item label="浏览器语言">
              {{ navigatorLanguage }}
            </el-descriptions-item>
          </el-descriptions>
        </div>

        <div class="debug-section">
          <h4>翻译测试</h4>
          <el-space direction="vertical" style="width: 100%">
            <div v-for="key in testKeys" :key="key" class="test-item">
              <div class="test-key">{{ key }}</div>
              <div class="test-value">
                <el-tag :type="getTranslationStatus(key).type" size="small">
                  {{ getTranslationStatus(key).status }}
                </el-tag>
                <span class="translation-text">{{ t(key) }}</span>
              </div>
            </div>
          </el-space>
        </div>
      </el-tab-pane>

      <!-- 快速修复 -->
      <el-tab-pane label="快速修复" name="fix">
        <div class="debug-section">
          <h4>语言设置</h4>
          <el-space direction="vertical" style="width: 100%">
            <el-button
              type="primary"
              :icon="Check"
              @click="fixLanguage('zh')"
              :disabled="currentLocale === 'zh'"
            >
              设置为中文 (推荐)
            </el-button>
            <el-button
              @click="fixLanguage('en')"
              :disabled="currentLocale === 'en'"
            >
              设置为英文
            </el-button>
            <el-button
              @click="fixLanguage('th')"
              :disabled="currentLocale === 'th'"
            >
              设置为泰文
            </el-button>
          </el-space>
        </div>

        <div class="debug-section">
          <h4>缓存清理</h4>
          <el-space direction="vertical" style="width: 100%">
            <el-button type="warning" :icon="Delete" @click="clearCache">
              清除浏览器缓存
            </el-button>
            <el-button type="danger" @click="resetAll">
              重置所有设置
            </el-button>
          </el-space>
        </div>

        <div class="debug-section">
          <h4>重新加载</h4>
          <el-space direction="vertical" style="width: 100%">
            <el-button type="success" :icon="Refresh" @click="reloadPage">
              刷新页面
            </el-button>
            <el-button @click="hardReload">
              硬刷新 (Ctrl+Shift+R)
            </el-button>
          </el-space>
        </div>
      </el-tab-pane>

      <!-- 诊断日志 -->
      <el-tab-pane label="诊断日志" name="log">
        <div class="debug-section">
          <el-button size="small" @click="runDiagnostics" type="primary">
            运行完整诊断
          </el-button>
          <el-button size="small" @click="logs = []">清除日志</el-button>
        </div>
        <div class="log-container">
          <div
            v-for="(log, index) in logs"
            :key="index"
            :class="['log-entry', `log-${log.type}`]"
          >
            <span class="log-time">{{ log.time }}</span>
            <span class="log-message">{{ log.message }}</span>
          </div>
          <div v-if="logs.length === 0" class="log-empty">
            暂无日志，点击"运行完整诊断"开始
          </div>
        </div>
      </el-tab-pane>
    </el-tabs>
  </el-card>

  <!-- 悬浮按钮 -->
  <el-button
    v-if="!visible"
    class="debugger-fab"
    circle
    type="primary"
    :icon="Tools"
    @click="visible = true"
    title="打开 I18N 调试工具"
  />
</template>

<script setup lang="ts">




import { ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';

import { Check, Delete, Refresh, Tools } from '@element-plus/icons-vue';
import { persistLocale, setI18nLocale, getActiveLocale } from '@/i18n';


import { useNotification } from "@/composables";

const notification = useNotification();
const { t, locale, fallbackLocale, availableLocales } = useI18n();

const visible = ref(false);
const activeTab = ref('status');
const logs = ref<Array<{ type: string; message: string; time: string }>>([]);

// 测试用的翻译key
const testKeys = [
  'rfq.management.title',
  'rfq.management.createRfq',
  'rfq.management.supplierInvitations.title',
  'rfq.management.pendingRequisitions.title',
];

// 当前配置
const currentLocale = computed(() => locale.value as string);
const storedLocale = ref<string | null>(null);
const navigatorLanguage = ref<string>('');

// 初始化
onMounted(() => {
  storedLocale.value = localStorage.getItem('supplier-system.locale');
  navigatorLanguage.value = navigator.language;

  // 检查URL参数，如果有 debug=i18n 则自动打开
  const urlParams = new URLSearchParams(window.location.search);
  if (urlParams.get('debug') === 'i18n') {
    visible.value = true;
    runDiagnostics();
  }
});

// 获取语言名称
const getLocaleName = (code: string): string => {
  const names: Record<string, string> = {
    en: 'English',
    zh: '中文',
    th: 'ไทย',
  };
  return names[code] || code;
};

// 获取翻译状态
const getTranslationStatus = (key: string) => {
  const translation = t(key);

  if (translation === key) {
    return { type: 'danger', status: '缺失' };
  }

  // 检查是否是中文翻译
  if (currentLocale.value === 'zh') {
    if (/[\u4e00-\u9fa5]/.test(translation)) {
      return { type: 'success', status: '正常' };
    } else {
      return { type: 'warning', status: '英文回退' };
    }
  }

  return { type: 'info', status: '已加载' };
};

// 添加日志
const addLog = (type: string, message: string) => {
  const now = new Date();
  const time = `${now.getHours().toString().padStart(2, '0')}:${now.getMinutes().toString().padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')}`;
  logs.value.unshift({ type, message, time });

  // 限制日志数量
  if (logs.value.length > 50) {
    logs.value = logs.value.slice(0, 50);
  }
};

// 修复语言设置
const fixLanguage = async (targetLocale: string) => {
  try {
    addLog('info', `正在切换语言到: ${getLocaleName(targetLocale)}`);

    // 设置 localStorage
    persistLocale(targetLocale as any);
    storedLocale.value = targetLocale;

    // 切换 i18n 语言
    await setI18nLocale(targetLocale as any);

    addLog('success', `✓ 语言已切换到: ${getLocaleName(targetLocale)}`);
    notification.success(`语言已设置为${getLocaleName(targetLocale)}`);

    // 刷新页面以确保所有组件使用新语言
    setTimeout(() => {
      notification.confirm(
        '语言已更改，建议刷新页面以确保所有内容正确显示',
        '提示',
        {
          confirmButtonText: '刷新页面',
          cancelButtonText: '稍后刷新',
          type: 'info',
        }
      )
        .then(() => {
          window.location.reload();
        })
        .catch(() => {
          addLog('info', '用户选择稍后刷新');
        });
    }, 500);
  } catch (error: any) {
    addLog('error', `✗ 语言切换失败: ${error.message}`);
    notification.error('语言切换失败');
  }
};

// 清除缓存
const clearCache = async () => {
  try {
    addLog('info', '正在清除浏览器缓存...');

    if ('caches' in window) {
      const cacheNames = await caches.keys();
      addLog('info', `找到 ${cacheNames.length} 个缓存`);

      for (const cacheName of cacheNames) {
        await caches.delete(cacheName);
        addLog('success', `✓ 已删除缓存: ${cacheName}`);
      }
    }

    addLog('success', '✓ 缓存清除完成');
    notification.success('缓存已清除，建议刷新页面');
  } catch (error: any) {
    addLog('error', `✗ 缓存清除失败: ${error.message}`);
    notification.error('缓存清除失败');
  }
};

// 重置所有设置
const resetAll = async () => {
  try {
    await notification.confirm(
      '这将清除所有I18N相关设置并重新加载页面。是否继续？',
      '警告',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning',
      }
    );

    addLog('info', '正在重置所有设置...');

    // 清除 storage
    localStorage.removeItem('supplier-system.locale');
    sessionStorage.removeItem('supplier-system.locale');
    addLog('success', '✓ localStorage 已清除');

    // 清除缓存
    await clearCache();

    addLog('success', '✓ 重置完成，即将刷新页面');

    setTimeout(() => {
      window.location.reload();
    }, 1000);
  } catch (error) {
    addLog('info', '用户取消了重置操作');
  }
};

// 刷新页面
const reloadPage = () => {
  addLog('info', '正在刷新页面...');
  window.location.reload();
};

// 硬刷新
const hardReload = () => {
  addLog('info', '正在执行硬刷新...');
  window.location.reload();
};

// 运行完整诊断
const runDiagnostics = () => {
  addLog('info', '========== 开始完整诊断 ==========');

  // 1. 检查语言设置
  addLog('info', `当前语言: ${currentLocale.value}`);
  addLog('info', `localStorage: ${storedLocale.value || '(未设置)'}`);
  addLog('info', `浏览器语言: ${navigatorLanguage.value}`);

  if (currentLocale.value === 'zh') {
    addLog('success', '✓ 语言设置正确 (中文)');
  } else {
    addLog('warning', `⚠ 当前语言不是中文: ${currentLocale.value}`);
  }

  // 2. 测试翻译
  addLog('info', '开始测试翻译key...');
  testKeys.forEach((key) => {
    const translation = t(key);
    const status = getTranslationStatus(key);

    if (status.type === 'success') {
      addLog('success', `✓ ${key}: "${translation}"`);
    } else if (status.type === 'warning') {
      addLog('warning', `⚠ ${key}: "${translation}" (英文回退)`);
    } else {
      addLog('error', `✗ ${key}: 翻译缺失`);
    }
  });

  // 3. 检查可用语言
  addLog('info', `可用语言: ${availableLocales.join(', ')}`);

  // 4. 检查浏览器支持
  const hasLocalStorage = typeof localStorage !== 'undefined';
  const hasCacheAPI = 'caches' in window;

  addLog(
    hasLocalStorage ? 'success' : 'error',
    `${hasLocalStorage ? '✓' : '✗'} localStorage 支持`
  );
  addLog(
    hasCacheAPI ? 'success' : 'info',
    `${hasCacheAPI ? '✓' : '-'} Cache API 支持`
  );

  addLog('info', '========== 诊断完成 ==========');
};




</script>

<style scoped>
.i18n-debugger {
  position: fixed;
  bottom: 20px;
  right: 20px;
  width: 500px;
  max-height: 80vh;
  overflow-y: auto;
  z-index: 9999;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.debugger-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.debugger-header h3 {
  margin: 0;
  font-size: 16px;
}

.debugger-fab {
  position: fixed;
  bottom: 20px;
  right: 20px;
  z-index: 9998;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}

.debug-section {
  margin-bottom: 20px;
}

.debug-section h4 {
  font-size: 14px;
  color: #606266;
  margin-bottom: 12px;
}

.test-item {
  padding: 8px;
  border: 1px solid #ebeef5;
  border-radius: 4px;
  margin-bottom: 8px;
}

.test-key {
  font-family: monospace;
  font-size: 12px;
  color: #909399;
  margin-bottom: 4px;
}

.test-value {
  display: flex;
  align-items: center;
  gap: 8px;
}

.translation-text {
  font-size: 14px;
  color: #303133;
}

.log-container {
  max-height: 300px;
  overflow-y: auto;
  background: #f5f7fa;
  border-radius: 4px;
  padding: 12px;
  font-family: monospace;
  font-size: 12px;
}

.log-entry {
  padding: 4px 0;
  border-bottom: 1px solid #e4e7ed;
}

.log-entry:last-child {
  border-bottom: none;
}

.log-time {
  color: #909399;
  margin-right: 8px;
}

.log-message {
  color: #303133;
}

.log-success .log-message {
  color: #67c23a;
}

.log-error .log-message {
  color: #f56c6c;
}

.log-warning .log-message {
  color: #e6a23c;
}

.log-info .log-message {
  color: #409eff;
}

.log-empty {
  text-align: center;
  color: #909399;
  padding: 20px;
}
</style>
