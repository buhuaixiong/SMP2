import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright 浏览器兼容性测试配置
 * 测试 Chrome, Edge, Firefox, Safari (WebKit)
 */

export default defineConfig({
  testDir: './tests/e2e',

  // 完全并行运行测试
  fullyParallel: true,

  // CI环境下禁止test.only
  forbidOnly: !!process.env.CI,

  // 重试策略
  retries: process.env.CI ? 2 : 0,

  // Worker数量
  workers: process.env.CI ? 1 : undefined,

  // 测试报告
  reporter: [
    ['html', { outputFolder: 'playwright-report' }],
    ['json', { outputFile: 'test-results/results.json' }],
    ['list'],
  ],

  // 全局设置
  use: {
    // 基础URL
    baseURL: 'http://localhost:5173',

    // 追踪：首次重试时启用
    trace: 'on-first-retry',

    // 截图：仅失败时
    screenshot: 'only-on-failure',

    // 视频：仅失败时
    video: 'retain-on-failure',

    // 操作超时
    actionTimeout: 10000,

    // 导航超时
    navigationTimeout: 30000,
  },

  // 浏览器测试项目
  projects: [
    // ========================================
    // 桌面浏览器
    // ========================================

    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1920, height: 1080 },
      },
    },

    {
      name: 'firefox',
      use: {
        ...devices['Desktop Firefox'],
        viewport: { width: 1920, height: 1080 },
      },
    },

    {
      name: 'webkit',
      use: {
        ...devices['Desktop Safari'],
        viewport: { width: 1920, height: 1080 },
      },
    },

    {
      name: 'edge',
      use: {
        ...devices['Desktop Edge'],
        channel: 'msedge',
        viewport: { width: 1920, height: 1080 },
      },
    },

    // ========================================
    // 移动设备（响应式测试）
    // ========================================

    {
      name: 'Mobile Chrome',
      use: {
        ...devices['Pixel 5'],
      },
    },

    {
      name: 'Mobile Safari',
      use: {
        ...devices['iPhone 12'],
      },
    },

    {
      name: 'iPad',
      use: {
        ...devices['iPad Pro'],
      },
    },

    // ========================================
    // 不同分辨率测试
    // ========================================

    {
      name: 'Desktop 1366x768',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1366, height: 768 },
      },
    },

    {
      name: 'Desktop 1440x900',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1440, height: 900 },
      },
    },
  ],

  // 开发服务器配置
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
    timeout: 120000, // 2分钟启动超时
  },

  // 输出目录
  outputDir: 'test-results',
});
