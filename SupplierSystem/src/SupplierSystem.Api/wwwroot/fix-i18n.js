/**
 * I18N 自动修复脚本
 * 在浏览器控制台运行此脚本以修复常见的国际化问题
 *
 * 使用方法：
 * 1. 打开浏览器开发者工具 (F12)
 * 2. 复制此脚本到控制台
 * 3. 按回车执行
 */

(function() {
  console.clear();
  console.log('%c🔧 I18N 自动修复工具', 'font-size: 20px; font-weight: bold; color: #007bff;');
  console.log('='.repeat(60));

  const STORAGE_KEY = 'supplier-system.locale';
  const TARGET_LOCALE = 'zh'; // 中文

  // 修复步骤计数
  let step = 1;
  const fixes = [];

  // 步骤 1: 检查并修复 localStorage
  console.log(`\n📝 步骤 ${step++}: 检查 localStorage 语言设置`);
  console.log('─'.repeat(60));

  const currentLocale = localStorage.getItem(STORAGE_KEY);
  console.log('当前设置:', currentLocale || '(未设置)');

  if (currentLocale !== TARGET_LOCALE) {
    localStorage.setItem(STORAGE_KEY, TARGET_LOCALE);
    console.log('%c✓ 已修复: 语言设置为中文 (zh)', 'color: #28a745; font-weight: bold;');
    fixes.push('localStorage 语言设置');
  } else {
    console.log('%c✓ 正确: 语言已设置为中文', 'color: #28a745;');
  }

  // 步骤 2: 检查浏览器语言偏好
  console.log(`\n🌐 步骤 ${step++}: 检查浏览器语言偏好`);
  console.log('─'.repeat(60));
  console.log('浏览器主语言:', navigator.language);
  console.log('语言偏好列表:', navigator.languages);

  const hasChineseInPreferences = navigator.languages.some(lang =>
    lang.toLowerCase().startsWith('zh')
  );

  if (!hasChineseInPreferences) {
    console.log('%c⚠ 建议: 浏览器语言偏好中没有中文', 'color: #ffc107; font-weight: bold;');
    console.log('请在浏览器设置中添加中文作为首选语言');
  } else {
    console.log('%c✓ 正确: 浏览器语言偏好包含中文', 'color: #28a745;');
  }

  // 步骤 3: 清除可能的缓存
  console.log(`\n🗑️ 步骤 ${step++}: 清除应用缓存`);
  console.log('─'.repeat(60));

  if ('caches' in window) {
    caches.keys().then(cacheNames => {
      if (cacheNames.length > 0) {
        console.log('找到缓存:', cacheNames);
        return Promise.all(
          cacheNames.map(cacheName => {
            console.log('正在删除缓存:', cacheName);
            return caches.delete(cacheName);
          })
        ).then(() => {
          console.log('%c✓ 已修复: 所有缓存已清除', 'color: #28a745; font-weight: bold;');
          fixes.push('清除应用缓存');
        });
      } else {
        console.log('✓ 无需清除: 未发现应用缓存');
      }
    }).catch(err => {
      console.log('⚠ 缓存清除失败:', err.message);
    });
  } else {
    console.log('✓ 此浏览器不支持 Cache API');
  }

  // 步骤 4: 检查 sessionStorage
  console.log(`\n📦 步骤 ${step++}: 检查 sessionStorage`);
  console.log('─'.repeat(60));

  const sessionLocale = sessionStorage.getItem(STORAGE_KEY);
  if (sessionLocale && sessionLocale !== TARGET_LOCALE) {
    sessionStorage.setItem(STORAGE_KEY, TARGET_LOCALE);
    console.log('%c✓ 已修复: sessionStorage 语言设置', 'color: #28a745; font-weight: bold;');
    fixes.push('sessionStorage 语言设置');
  } else {
    console.log('✓ sessionStorage 正常或未使用');
  }

  // 步骤 5: 验证翻译key（如果在应用页面中）
  console.log(`\n🔍 步骤 ${step++}: 验证 I18N 实例`);
  console.log('─'.repeat(60));

  // 尝试访问 Vue I18N 实例
  setTimeout(() => {
    try {
      // 查找 Vue 应用实例
      const vueApp = document.querySelector('#app')?.__vue_app__;

      if (vueApp) {
        const i18n = vueApp.config.globalProperties.$i18n;

        if (i18n) {
          console.log('✓ 找到 I18N 实例');
          console.log('当前语言:', i18n.global.locale.value);
          console.log('回退语言:', i18n.global.fallbackLocale.value);
          console.log('可用语言:', i18n.global.availableLocales);

          // 测试翻译
          const testKey = 'rfq.management.title';
          const translation = i18n.global.t(testKey);
          console.log(`\n测试翻译 "${testKey}":`);
          console.log('结果:', translation);

          if (translation === testKey) {
            console.log('%c⚠ 警告: 翻译key未被解析，可能需要重新加载页面', 'color: #ffc107; font-weight: bold;');
          } else if (translation === 'RFQ Management') {
            console.log('%c⚠ 注意: 当前显示英文翻译', 'color: #ffc107; font-weight: bold;');

            // 尝试切换到中文
            if (i18n.global.locale.value !== TARGET_LOCALE) {
              i18n.global.locale.value = TARGET_LOCALE;
              console.log('%c✓ 已修复: 切换到中文', 'color: #28a745; font-weight: bold;');
              fixes.push('I18N 语言切换');

              // 再次测试
              const newTranslation = i18n.global.t(testKey);
              console.log('切换后的翻译:', newTranslation);
            }
          } else if (translation === 'RFQ报价管理') {
            console.log('%c✓ 完美: 中文翻译正常工作', 'color: #28a745; font-weight: bold;');
          }
        } else {
          console.log('⚠ 未找到 I18N 实例（可能不在 Vue 应用页面中）');
        }
      } else {
        console.log('⚠ 未找到 Vue 应用实例');
        console.log('提示: 请在应用页面中运行此脚本');
      }
    } catch (error) {
      console.log('⚠ 无法访问 I18N 实例:', error.message);
      console.log('提示: 如果在应用外部运行，这是正常的');
    }
  }, 500);

  // 总结
  console.log('\n' + '='.repeat(60));
  console.log('%c📊 修复总结', 'font-size: 16px; font-weight: bold; color: #007bff;');
  console.log('='.repeat(60));

  setTimeout(() => {
    if (fixes.length > 0) {
      console.log('\n%c✓ 已执行的修复:', 'color: #28a745; font-weight: bold;');
      fixes.forEach((fix, index) => {
        console.log(`  ${index + 1}. ${fix}`);
      });

      console.log('\n%c🔄 下一步操作:', 'color: #007bff; font-weight: bold;');
      console.log('1. 刷新页面以应用更改 (F5)');
      console.log('2. 如果问题仍存在，进行硬刷新 (Ctrl+Shift+R)');
      console.log('3. 检查页面是否显示中文');

      // 询问是否自动刷新
      console.log('\n%c💡 提示: 执行以下命令可立即刷新页面:', 'color: #17a2b8;');
      console.log('location.reload()');

    } else {
      console.log('\n%c✓ 所有检查都通过，无需修复', 'color: #28a745; font-weight: bold;');
      console.log('\n如果页面仍显示英文翻译，可能的原因:');
      console.log('1. 需要刷新页面 (F5)');
      console.log('2. 开发服务器需要重启');
      console.log('3. 翻译文件未正确加载（检查 Network 标签）');
    }
  }, 1000);

  // 返回修复函数供手动调用
  window.fixI18n = {
    setLanguage: (locale) => {
      localStorage.setItem(STORAGE_KEY, locale);
      sessionStorage.setItem(STORAGE_KEY, locale);
      console.log(`✓ 语言已设置为: ${locale}`);
      console.log('执行 location.reload() 以应用更改');
    },

    setChinese: () => {
      window.fixI18n.setLanguage('zh');
    },

    setEnglish: () => {
      window.fixI18n.setLanguage('en');
    },

    setThai: () => {
      window.fixI18n.setLanguage('th');
    },

    clearAll: () => {
      localStorage.removeItem(STORAGE_KEY);
      sessionStorage.removeItem(STORAGE_KEY);
      if ('caches' in window) {
        caches.keys().then(names => {
          names.forEach(name => caches.delete(name));
        });
      }
      console.log('✓ 所有I18N相关存储已清除');
      console.log('执行 location.reload() 以重新初始化');
    },

    reload: () => {
      location.reload();
    },

    hardReload: () => {
      location.reload(true);
    },

    getCurrentLocale: () => {
      const stored = localStorage.getItem(STORAGE_KEY);
      console.log('localStorage:', stored);
      console.log('sessionStorage:', sessionStorage.getItem(STORAGE_KEY));
      console.log('浏览器语言:', navigator.language);
      return stored;
    },

    testTranslation: (key) => {
      try {
        const vueApp = document.querySelector('#app')?.__vue_app__;
        const i18n = vueApp?.config.globalProperties.$i18n;
        if (i18n) {
          const result = i18n.global.t(key);
          console.log(`翻译 "${key}":`, result);
          return result;
        } else {
          console.log('⚠ I18N 实例未找到');
          return null;
        }
      } catch (error) {
        console.log('⚠ 错误:', error.message);
        return null;
      }
    }
  };

  console.log('\n%c💡 可用的修复命令:', 'color: #17a2b8; font-weight: bold;');
  console.log('fixI18n.setChinese()      - 设置为中文');
  console.log('fixI18n.setEnglish()      - 设置为英文');
  console.log('fixI18n.setThai()         - 设置为泰文');
  console.log('fixI18n.getCurrentLocale() - 查看当前语言');
  console.log('fixI18n.testTranslation("rfq.management.title") - 测试翻译');
  console.log('fixI18n.clearAll()        - 清除所有设置');
  console.log('fixI18n.reload()          - 刷新页面');

})();
