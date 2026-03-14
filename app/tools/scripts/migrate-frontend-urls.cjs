#!/usr/bin/env node

/**
 * 前端 /uploads/ URL 引用扫描工具
 *
 * 扫描前端代码中所有硬编码的 /uploads/ URL 引用
 * 生成迁移报告,用于安全修复 P0 阶段
 *
 * @version 1.0
 * @date 2025-10-29
 */

const fs = require('fs');
const path = require('path');

// 递归搜索文件
function findFiles(dir, extensions, results = []) {
  if (!fs.existsSync(dir)) {
    return results;
  }

  const files = fs.readdirSync(dir);

  files.forEach(file => {
    const filePath = path.join(dir, file);
    const stat = fs.statSync(filePath);

    if (stat.isDirectory()) {
      // 跳过 node_modules 和其他不需要的目录
      if (!['node_modules', '.git', 'dist', 'build', '.claude'].includes(file)) {
        findFiles(filePath, extensions, results);
      }
    } else {
      const ext = path.extname(file);
      if (extensions.includes(ext)) {
        results.push(filePath);
      }
    }
  });

  return results;
}

// 搜索模式
const patterns = [
  // 模式1: /uploads/ 硬编码URL
  {
    regex: /['"`]\/uploads\/([^'"`]+)['"`]/g,
    description: '硬编码 /uploads/ URL'
  },
  // 模式2: getFileUrl() 调用
  {
    regex: /getFileUrl\s*\(/g,
    description: 'getFileUrl() 调用(已废弃)'
  },
  // 模式3: 模板字符串中的 uploads
  {
    regex: /`[^`]*\/uploads\/[^`]*`/g,
    description: '模板字符串中的 /uploads/'
  },
  // 模式4: downloadUrl 属性赋值
  {
    regex: /downloadUrl\s*[:=]\s*['"`]?\/uploads\//g,
    description: 'downloadUrl 属性使用 /uploads/'
  },
  // 模式5: src/href 属性中的 uploads
  {
    regex: /(src|href)\s*=\s*['"`][^'"`]*\/uploads\//g,
    description: 'src/href 属性使用 /uploads/'
  }
];

console.log('='.repeat(70));
console.log('前端 /uploads/ URL 引用扫描报告');
console.log('='.repeat(70));
console.log('');
console.log('扫描目标: Vue/TS/JS 文件');
console.log('扫描时间:', new Date().toLocaleString('zh-CN'));
console.log('');

// 查找所有前端文件
const srcDir = path.join(__dirname, '..', 'src');
const files = findFiles(srcDir, ['.vue', '.ts', '.tsx', '.js', '.jsx']);

console.log(`找到 ${files.length} 个文件待扫描`);
console.log('');

let totalMatches = 0;
const matchesByFile = [];

files.forEach(file => {
  const content = fs.readFileSync(file, 'utf-8');
  const lines = content.split('\n');
  const relativePath = path.relative(path.join(__dirname, '..'), file).replace(/\\/g, '/');

  let fileMatches = [];

  patterns.forEach(({ regex, description }) => {
    lines.forEach((line, index) => {
      // 重置正则 lastIndex
      regex.lastIndex = 0;
      if (regex.test(line)) {
        fileMatches.push({
          line: index + 1,
          content: line.trim(),
          pattern: description
        });
      }
    });
  });

  if (fileMatches.length > 0) {
    matchesByFile.push({
      file: relativePath,
      matches: fileMatches
    });
    totalMatches += fileMatches.length;
  }
});

// 输出报告
if (matchesByFile.length === 0) {
  console.log('✅ 未发现任何 /uploads/ URL 引用!');
  console.log('');
} else {
  console.log(`⚠️  发现 ${totalMatches} 处引用需要迁移`);
  console.log('');

  matchesByFile.forEach(({ file, matches }) => {
    console.log(`📄 ${file} (${matches.length} 处)`);
    matches.forEach(({ line, content, pattern }) => {
      console.log(`   Line ${line}: ${pattern}`);
      console.log(`   ${content.substring(0, 100)}${content.length > 100 ? '...' : ''}`);
      console.log('');
    });
  });
}

console.log('='.repeat(70));
console.log(`总计: ${totalMatches} 处引用需要迁移`);
console.log('涉及文件: ' + matchesByFile.length);
console.log('='.repeat(70));

// 生成迁移清单(JSON)
const reportPath = path.join(__dirname, '..', 'migration-report.json');
const report = {
  scanDate: new Date().toISOString(),
  totalMatches,
  filesAffected: matchesByFile.length,
  matches: matchesByFile
};

fs.writeFileSync(reportPath, JSON.stringify(report, null, 2));

console.log('');
console.log('✅ 详细报告已保存到: migration-report.json');
console.log('');

// 生成迁移优先级建议
console.log('迁移优先级建议:');
console.log('');
console.log('🔴 P0 - 紧急 (后端API响应):');
console.log('   - 搜索 routes/*.js 中返回 downloadUrl 的位置');
console.log('   - 修改为返回 token-based URL');
console.log('');
console.log('🟠 P1 - 高优先级 (工具函数):');
console.log('   - src/utils/fileDownload.ts - 创建 getSecureDownloadUrl()');
console.log('   - 废弃旧的 getFileUrl() 函数');
console.log('');
console.log('🟡 P2 - 中优先级 (组件引用):');
console.log('   - 批量替换组件中的 /uploads/ 引用');
console.log('   - 使用新的工具函数');
console.log('');
console.log('🟢 P3 - 低优先级 (兼容层):');
console.log('   - 实现 /uploads 路由兼容层');
console.log('   - 30天过渡期后完全移除');
console.log('');

// 返回退出代码
process.exit(totalMatches > 0 ? 1 : 0);
