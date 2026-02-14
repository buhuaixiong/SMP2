#!/usr/bin/env node

/**
 * ElMessageBox.confirm 使用分析工具
 *
 * 功能：
 * 1. 扫描所有 .vue 文件中的 ElMessageBox.confirm 使用
 * 2. 提取上下文信息
 * 3. 根据关键词分类危险程度
 * 4. 生成迁移优先级报告
 */

const fs = require('fs')
const path = require('path')

// 危险操作关键词
const DANGER_KEYWORDS = {
  high: ['delete', '删除', 'remove', '移除', 'clear', '清空', 'void', '作废', 'cancel', '取消'],
  medium: ['reject', '驳回', 'refuse', '拒绝', 'disapprove', '不批准', 'close', '关闭'],
  low: ['submit', '提交', 'approve', '批准', 'confirm', '确认', 'save', '保存']
}

// 扫描目录
const SRC_DIR = path.join(__dirname, '../apps/web/src')

// 存储扫描结果
const results = {
  high: [],
  medium: [],
  low: [],
  unknown: []
}

/**
 * 递归扫描目录
 */
function scanDirectory(dir) {
  const files = fs.readdirSync(dir)

  files.forEach(file => {
    const filePath = path.join(dir, file)
    const stat = fs.statSync(filePath)

    if (stat.isDirectory()) {
      scanDirectory(filePath)
    } else if (file.endsWith('.vue')) {
      analyzeFile(filePath)
    }
  })
}

/**
 * 分析单个文件
 */
function analyzeFile(filePath) {
  const content = fs.readFileSync(filePath, 'utf-8')
  const lines = content.split('\n')

  let inMessageBox = false
  let messageBoxStart = 0
  let currentContext = []

  lines.forEach((line, index) => {
    // 检测 ElMessageBox.confirm 开始
    if (line.includes('ElMessageBox.confirm')) {
      inMessageBox = true
      messageBoxStart = index
      currentContext = []
    }

    // 收集上下文
    if (inMessageBox) {
      currentContext.push(line.trim())

      // 检测结束（找到闭合的括号）
      if (line.includes(')') && !line.includes('ElMessageBox.confirm(')) {
        const usage = {
          file: filePath.replace(SRC_DIR, '').replace(/\\/g, '/'),
          lineNumber: messageBoxStart + 1,
          context: currentContext.join('\n'),
          functionName: extractFunctionName(lines, messageBoxStart)
        }

        // 分类
        const category = categorizeUsage(usage.context, usage.functionName)
        results[category].push(usage)

        inMessageBox = false
        currentContext = []
      }
    }
  })
}

/**
 * 提取函数名
 */
function extractFunctionName(lines, startLine) {
  // 向上查找函数定义
  for (let i = startLine; i >= Math.max(0, startLine - 20); i--) {
    const line = lines[i]
    const match = line.match(/(?:async\s+)?function\s+(\w+)|const\s+(\w+)\s*=.*(?:async\s+)?\(/)
    if (match) {
      return match[1] || match[2]
    }
  }
  return 'unknown'
}

/**
 * 根据关键词分类
 */
function categorizeUsage(context, functionName) {
  const text = (context + ' ' + functionName).toLowerCase()

  // 检查高危关键词
  for (const keyword of DANGER_KEYWORDS.high) {
    if (text.includes(keyword)) {
      return 'high'
    }
  }

  // 检查中危关键词
  for (const keyword of DANGER_KEYWORDS.medium) {
    if (text.includes(keyword)) {
      return 'medium'
    }
  }

  // 检查低危关键词
  for (const keyword of DANGER_KEYWORDS.low) {
    if (text.includes(keyword)) {
      return 'low'
    }
  }

  return 'unknown'
}

/**
 * 生成报告
 */
function generateReport() {
  console.log('\n' + '='.repeat(80))
  console.log('ElMessageBox.confirm 使用分析报告')
  console.log('='.repeat(80))
  console.log()

  const total = Object.values(results).reduce((sum, arr) => sum + arr.length, 0)
  console.log(`总计发现: ${total} 处使用\n`)

  // 高危操作
  console.log(`🔴 高危操作 (${results.high.length} 处) - 建议使用 SlideConfirmButton`)
  console.log('-'.repeat(80))
  results.high.forEach(usage => {
    console.log(`  📄 ${usage.file}:${usage.lineNumber}`)
    console.log(`     函数: ${usage.functionName}`)
    console.log()
  })

  // 中危操作
  console.log(`🟡 中危操作 (${results.medium.length} 处) - 建议使用 ConfirmButton`)
  console.log('-'.repeat(80))
  results.medium.forEach(usage => {
    console.log(`  📄 ${usage.file}:${usage.lineNumber}`)
    console.log(`     函数: ${usage.functionName}`)
    console.log()
  })

  // 低危操作
  console.log(`🟢 低危操作 (${results.low.length} 处) - 建议直接执行 + ElMessage`)
  console.log('-'.repeat(80))
  results.low.forEach(usage => {
    console.log(`  📄 ${usage.file}:${usage.lineNumber}`)
    console.log(`     函数: ${usage.functionName}`)
    console.log()
  })

  // 未分类操作
  if (results.unknown.length > 0) {
    console.log(`⚪ 未分类操作 (${results.unknown.length} 处) - 需要手动判断`)
    console.log('-'.repeat(80))
    results.unknown.forEach(usage => {
      console.log(`  📄 ${usage.file}:${usage.lineNumber}`)
      console.log(`     函数: ${usage.functionName}`)
      console.log()
    })
  }

  // 按模块统计
  console.log('\n' + '='.repeat(80))
  console.log('按模块统计')
  console.log('='.repeat(80))

  const moduleStats = {}
  Object.values(results).flat().forEach(usage => {
    const parts = usage.file.split('/')
    const module = parts[1] // components 或 views
    const subModule = parts[2] || 'root'
    const key = `${module}/${subModule}`

    if (!moduleStats[key]) {
      moduleStats[key] = 0
    }
    moduleStats[key]++
  })

  Object.entries(moduleStats)
    .sort((a, b) => b[1] - a[1])
    .forEach(([module, count]) => {
      console.log(`  ${module}: ${count} 处`)
    })

  // 导出 JSON 报告
  const jsonReport = {
    total,
    byCategory: {
      high: results.high.length,
      medium: results.medium.length,
      low: results.low.length,
      unknown: results.unknown.length
    },
    details: results,
    generatedAt: new Date().toISOString()
  }

  const reportPath = path.join(__dirname, '../MESSAGEBOX_ANALYSIS.json')
  fs.writeFileSync(reportPath, JSON.stringify(jsonReport, null, 2))
  console.log(`\n详细报告已保存至: ${reportPath}`)

  console.log('\n' + '='.repeat(80))
  console.log('迁移建议')
  console.log('='.repeat(80))
  console.log(`
1. 优先迁移高危操作（${results.high.length} 处），使用 SlideConfirmButton
2. 其次迁移中危操作（${results.medium.length} 处），使用 ConfirmButton
3. 最后迁移低危操作（${results.low.length} 处），直接执行 + ElMessage

参考文档: ELMESSAGEBOX_MIGRATION_GUIDE.md
`)
}

// 执行扫描
try {
  console.log('开始扫描 ElMessageBox.confirm 使用情况...\n')
  scanDirectory(SRC_DIR)
  generateReport()
} catch (error) {
  console.error('扫描失败:', error)
  process.exit(1)
}
