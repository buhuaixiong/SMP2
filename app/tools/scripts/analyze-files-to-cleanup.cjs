#!/usr/bin/env node

/**
 * 仓库清理分析工具（改进版）
 *
 * 功能：
 * - 递归扫描整个项目（跳过 node_modules/.git 等）
 * - 基于路径前缀过滤排除项
 * - 检查 .gitignore 覆盖情况
 * - 生成带时间戳的报告
 * - 明确标注需人工确认的高风险项
 */

const fs = require('fs');
const path = require('path');

const PROJECT_ROOT = path.resolve(__dirname, '..');
const BACKEND_ROOT = path.join(PROJECT_ROOT, 'supplier-backend');

// 跳过的目录（递归扫描时忽略）
const SKIP_DIRS = [
  'node_modules',
  '.git',
  'dist',
  'build',
  '.vscode',
  '.idea',
  'coverage',
  'playwright-report',
  'test-results'
];

// 文件分类定义（改进版）
const FILE_CATEGORIES = {
  TEMP_PYTHON: {
    name: '临时 Python 脚本',
    pattern: /^tmp_.*\.py$/,
    baseDir: PROJECT_ROOT,
    excludePaths: ['node_modules/', '.git/', 'venv/'],
    risk: 'LOW',
    action: 'DELETE',
    requiresConfirmation: false,
    gitignorePattern: 'tmp_*.py'
  },
  TEMP_VUE: {
    name: '临时 Vue 文件',
    pattern: /^tmp_.*\.vue$/,
    baseDir: PROJECT_ROOT,
    excludePaths: ['node_modules/', '.git/'],
    risk: 'LOW',
    action: 'DELETE',
    requiresConfirmation: false,
    gitignorePattern: 'tmp_*.vue'
  },
  TEST_SCRIPTS: {
    name: '一次性测试脚本（非正式测试套件）',
    pattern: /^(test|check|debug|diagnose)-.*\.js$/,
    baseDir: BACKEND_ROOT,
    // 排除正式测试目录和 scripts 目录
    excludePaths: [
      'tests/',
      'test/',
      'scripts/',
      'node_modules/',
      '.git/'
    ],
    risk: 'MEDIUM',
    action: 'REVIEW',
    requiresConfirmation: true,
    note: '部分脚本可能有复用价值，建议先移至 scripts/archive/diagnostic/',
    gitignorePattern: null // 不应添加到 gitignore，因为正式测试也用这些名称
  },
  BATCH_SCRIPTS: {
    name: '中文批处理脚本',
    pattern: /\.bat$/,
    baseDir: BACKEND_ROOT,
    excludePaths: ['node_modules/', '.git/'],
    risk: 'LOW',
    action: 'DELETE',
    requiresConfirmation: false,
    note: '建议改写为跨平台 Node 脚本后替换',
    gitignorePattern: '*.bat'
  },
  RUNTIME_LOGS: {
    name: '运行期日志文件',
    pattern: /^backend.*\.log$/,
    baseDir: BACKEND_ROOT,
    excludePaths: ['node_modules/', '.git/', 'logs/'],
    risk: 'MEDIUM',
    action: 'DELETE',
    requiresConfirmation: false,
    gitignorePattern: 'supplier-backend/backend*.log'
  },
  DB_BACKUPS: {
    name: '数据库备份文件',
    pattern: /\.(backup-\d+|\.bak)$/,
    baseDir: BACKEND_ROOT,
    excludePaths: ['node_modules/', '.git/'],
    risk: 'HIGH',
    action: 'ARCHIVE_THEN_DELETE',
    requiresConfirmation: true,
    note: '包含生产数据，删除前确保已有外部备份',
    gitignorePattern: '*.backup-*'
  },
  ROOT_DOCS: {
    name: '根目录重复文档',
    files: [
      'ADVANCED-SECURITY-TEST-REPORT.md',
      'AUDIT-LOG-ENHANCEMENT-SUMMARY.md',
      'BATCH-TAG-AND-BUYER-ASSIGNMENT-IMPLEMENTATION.md',
      'BATCH-TAG-BUYER-FEATURES-GUIDE.md',
      'BROWSER-CACHE-CLEAR-GUIDE.md',
      'BROWSER-COMPATIBILITY-GUIDE.md',
      'BROWSER-TEST-CHECKLIST.md',
      'BROWSER-TESTING-SUMMARY.md',
      'BULK-DOCUMENT-IMPORT-IMPLEMENTATION.md',
      'EMAIL-SETTINGS-GUIDE.md',
      'EMAIL-SETTINGS-IMPLEMENTATION-SUMMARY.md',
      'EMERGENCY-LOCKDOWN-IMPLEMENTATION.md',
      'FILE-UPLOAD-APPROVAL-IMPLEMENTATION.md',
      'FILE-VALIDATION-IMPLEMENTATION.md',
      'FILE-VALIDITY-AND-REMINDER-IMPLEMENTATION.md',
      'FINAL-PERFORMANCE-TEST-SUMMARY.md',
      'FRONTEND-IMPLEMENTATION-COMPLETE.md',
      'fix-template-download.md'
    ],
    baseDir: PROJECT_ROOT,
    risk: 'LOW',
    action: 'MOVE',
    requiresConfirmation: false,
    targetDir: 'docs/implementation-reports/',
    gitignorePattern: null
  },
  TEMP_DOCS: {
    name: '临时中文文档',
    files: [
      '如何查看后端日志.md',
      '修复清单.md',
      'README-问题已修复.txt',
      'RFQ-提交问题-解决方案.md',
      'SOLUTION-SUMMARY.md',
      'QUICK-START-GUIDE.md',
      'PORT-CONFLICT-FIX.md',
      'migration-report.json'
    ],
    baseDir: PROJECT_ROOT,
    risk: 'LOW',
    action: 'DELETE',
    requiresConfirmation: false,
    note: '有价值的内容应先整合到正式文档',
    gitignorePattern: null
  },
  MIGRATION_RUNNERS: {
    name: '特定迁移运行器',
    pattern: /^run-migration-\d+\.js$/,
    baseDir: BACKEND_ROOT,
    excludePaths: ['node_modules/', '.git/', 'scripts/'],
    risk: 'MEDIUM',
    action: 'ARCHIVE',
    requiresConfirmation: true,
    targetDir: 'supplier-backend/scripts/archive/migration-runners/',
    note: '迁移已应用，保留仅供历史参考',
    gitignorePattern: null
  }
};

// 目录扫描
const DIRECTORIES = {
  EXTRACTED_BACKEND: {
    name: 'extracted_backend 冗余副本',
    path: path.join(PROJECT_ROOT, 'extracted_backend'),
    risk: 'HIGH',
    action: 'REVIEW_THEN_DELETE',
    requiresConfirmation: true,
    note: '完整的 supplier-backend 副本（含 node_modules），删除前确认无独有代码',
    gitignorePattern: 'extracted_backend/'
  },
  AUDIT_ARCHIVE: {
    name: '审计日志归档',
    path: path.join(BACKEND_ROOT, 'audit-archive'),
    risk: 'HIGH',
    action: 'ARCHIVE_EXTERNALLY',
    requiresConfirmation: true,
    note: '敏感审计记录，应先备份到安全存储再删除',
    gitignorePattern: 'supplier-backend/audit-archive/'
  }
};

// 颜色输出
const colors = {
  reset: '\x1b[0m',
  red: '\x1b[31m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  blue: '\x1b[36m',
  gray: '\x1b[90m',
  magenta: '\x1b[35m'
};

function colorize(text, color) {
  return `${colors[color]}${text}${colors.reset}`;
}

function getRiskColor(risk) {
  switch (risk) {
    case 'HIGH': return 'red';
    case 'MEDIUM': return 'yellow';
    case 'LOW': return 'green';
    default: return 'gray';
  }
}

function formatSize(bytes) {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
}

/**
 * 检查路径是否应该被排除（基于路径前缀）
 */
function shouldExcludePath(filePath, excludePaths) {
  if (!excludePaths || excludePaths.length === 0) return false;

  const normalizedPath = filePath.replace(/\\/g, '/');

  return excludePaths.some(excludePattern => {
    const normalizedPattern = excludePattern.replace(/\\/g, '/');
    return normalizedPath.includes(normalizedPattern);
  });
}

/**
 * 递归扫描目录
 */
function recursiveScan(dirPath, pattern, excludePaths = [], results = []) {
  try {
    const entries = fs.readdirSync(dirPath, { withFileTypes: true });

    for (const entry of entries) {
      const fullPath = path.join(dirPath, entry.name);
      const relativePath = path.relative(PROJECT_ROOT, fullPath);

      // 跳过特定目录
      if (entry.isDirectory()) {
        if (SKIP_DIRS.includes(entry.name)) {
          continue;
        }

        // 检查路径排除规则
        if (shouldExcludePath(relativePath, excludePaths)) {
          continue;
        }

        // 递归扫描子目录
        recursiveScan(fullPath, pattern, excludePaths, results);
      } else if (entry.isFile()) {
        // 检查路径排除规则
        if (shouldExcludePath(relativePath, excludePaths)) {
          continue;
        }

        // 检查文件名模式
        if (pattern.test(entry.name)) {
          const stats = fs.statSync(fullPath);
          results.push({
            name: entry.name,
            path: fullPath,
            relativePath,
            size: stats.size
          });
        }
      }
    }
  } catch (err) {
    console.error(`扫描失败: ${dirPath}`, err.message);
  }

  return results;
}

function getFileSize(filePath) {
  try {
    const stats = fs.statSync(filePath);
    return stats.size;
  } catch (err) {
    return 0;
  }
}

function getDirSize(dirPath) {
  let totalSize = 0;
  try {
    const files = fs.readdirSync(dirPath);
    for (const file of files) {
      const filePath = path.join(dirPath, file);
      const stats = fs.statSync(filePath);
      if (stats.isDirectory()) {
        totalSize += getDirSize(filePath);
      } else {
        totalSize += stats.size;
      }
    }
  } catch (err) {
    // 忽略访问错误
  }
  return totalSize;
}

/**
 * 扫描文件（支持固定列表和模式匹配）
 */
function scanFiles(category) {
  const results = [];

  if (category.files) {
    // 固定文件列表
    for (const file of category.files) {
      const fullPath = path.join(category.baseDir, file);
      if (fs.existsSync(fullPath)) {
        results.push({
          name: file,
          path: fullPath,
          relativePath: path.relative(PROJECT_ROOT, fullPath),
          size: getFileSize(fullPath)
        });
      }
    }
  } else if (category.pattern) {
    // 递归模式匹配
    recursiveScan(
      category.baseDir,
      category.pattern,
      category.excludePaths || [],
      results
    );
  }

  return results;
}

function scanDirectories() {
  const results = [];

  for (const [key, dir] of Object.entries(DIRECTORIES)) {
    if (fs.existsSync(dir.path)) {
      const size = getDirSize(dir.path);
      results.push({
        key,
        ...dir,
        size
      });
    }
  }

  return results;
}

/**
 * 检查 .gitignore 覆盖情况
 */
function checkGitignoreCoverage() {
  const gitignorePath = path.join(PROJECT_ROOT, '.gitignore');
  let gitignoreContent = '';

  try {
    gitignoreContent = fs.readFileSync(gitignorePath, 'utf-8');
  } catch (err) {
    console.warn('无法读取 .gitignore 文件');
    return { covered: [], missing: [] };
  }

  const covered = [];
  const missing = [];

  // 检查文件类别的 gitignore 模式
  for (const [key, category] of Object.entries(FILE_CATEGORIES)) {
    if (category.gitignorePattern) {
      const pattern = category.gitignorePattern;
      const isPatternCovered = gitignoreContent.includes(pattern);

      if (isPatternCovered) {
        covered.push({ category: category.name, pattern });
      } else {
        missing.push({ category: category.name, pattern });
      }
    }
  }

  // 检查目录的 gitignore 模式
  for (const [key, dir] of Object.entries(DIRECTORIES)) {
    if (dir.gitignorePattern) {
      const pattern = dir.gitignorePattern;
      const isPatternCovered = gitignoreContent.includes(pattern);

      if (isPatternCovered) {
        covered.push({ category: dir.name, pattern });
      } else {
        missing.push({ category: dir.name, pattern });
      }
    }
  }

  return { covered, missing };
}

function generateReport() {
  console.log('\n' + colorize('='.repeat(80), 'blue'));
  console.log(colorize('          📋 仓库清理分析报告（改进版）', 'blue'));
  console.log(colorize('='.repeat(80), 'blue') + '\n');

  let totalFiles = 0;
  let totalSize = 0;
  const allFindings = [];

  // 扫描文件
  console.log(colorize('📁 文件扫描结果（递归）:', 'yellow') + '\n');

  for (const [key, category] of Object.entries(FILE_CATEGORIES)) {
    const files = scanFiles(category);

    if (files.length > 0) {
      const categorySize = files.reduce((sum, f) => sum + f.size, 0);
      totalFiles += files.length;
      totalSize += categorySize;

      console.log(colorize(`▸ ${category.name}`, 'blue'));
      console.log(colorize(`  风险级别: ${category.risk}`, getRiskColor(category.risk)));
      console.log(colorize(`  建议操作: ${category.action}`, 'magenta'));

      if (category.requiresConfirmation) {
        console.log(colorize(`  ⚠️  需人工确认`, 'yellow'));
      }

      if (category.targetDir) {
        console.log(colorize(`  目标目录: ${category.targetDir}`, 'gray'));
      }

      if (category.note) {
        console.log(colorize(`  注意事项: ${category.note}`, 'gray'));
      }

      console.log(colorize(`  文件数量: ${files.length}`, 'gray'));
      console.log(colorize(`  占用空间: ${formatSize(categorySize)}`, 'gray'));
      console.log('  文件列表:');

      files.forEach(f => {
        console.log(colorize(`    - ${f.relativePath}`, 'gray') +
                   colorize(` (${formatSize(f.size)})`, 'gray'));
      });

      console.log('');

      allFindings.push({
        category: category.name,
        risk: category.risk,
        action: category.action,
        requiresConfirmation: category.requiresConfirmation,
        targetDir: category.targetDir,
        note: category.note,
        files
      });
    }
  }

  // 扫描目录
  console.log(colorize('📂 目录扫描结果:', 'yellow') + '\n');

  const directories = scanDirectories();
  for (const dir of directories) {
    totalSize += dir.size;

    console.log(colorize(`▸ ${dir.name}`, 'blue'));
    console.log(colorize(`  路径: ${path.relative(PROJECT_ROOT, dir.path)}`, 'gray'));
    console.log(colorize(`  风险级别: ${dir.risk}`, getRiskColor(dir.risk)));
    console.log(colorize(`  建议操作: ${dir.action}`, 'magenta'));

    if (dir.requiresConfirmation) {
      console.log(colorize(`  ⚠️  需人工确认`, 'yellow'));
    }

    if (dir.note) {
      console.log(colorize(`  注意事项: ${dir.note}`, 'gray'));
    }

    console.log(colorize(`  占用空间: ${formatSize(dir.size)}`, 'gray'));
    console.log('');

    allFindings.push({
      category: dir.name,
      risk: dir.risk,
      action: dir.action,
      requiresConfirmation: dir.requiresConfirmation,
      note: dir.note,
      path: dir.path,
      relativePath: path.relative(PROJECT_ROOT, dir.path),
      size: dir.size
    });
  }

  // 检查 .gitignore 覆盖情况
  console.log(colorize('='.repeat(80), 'blue'));
  console.log(colorize('🛡️  .gitignore 覆盖情况', 'yellow') + '\n');

  const gitignoreCoverage = checkGitignoreCoverage();

  if (gitignoreCoverage.covered.length > 0) {
    console.log(colorize('✅ 已覆盖:', 'green'));
    gitignoreCoverage.covered.forEach(item => {
      console.log(`  - ${item.category}: ${colorize(item.pattern, 'gray')}`);
    });
    console.log('');
  }

  if (gitignoreCoverage.missing.length > 0) {
    console.log(colorize('❌ 缺失（建议添加）:', 'red'));
    gitignoreCoverage.missing.forEach(item => {
      console.log(`  - ${item.category}: ${colorize(item.pattern, 'yellow')}`);
    });
    console.log('');
  }

  // 总结
  console.log(colorize('='.repeat(80), 'blue'));
  console.log(colorize('📊 总结', 'yellow') + '\n');
  console.log(`  待处理文件数: ${colorize(totalFiles.toString(), 'yellow')}`);
  console.log(`  待处理目录数: ${colorize(directories.length.toString(), 'yellow')}`);
  console.log(`  总占用空间: ${colorize(formatSize(totalSize), 'yellow')}`);
  console.log('');

  // 风险分布
  const riskCounts = { HIGH: 0, MEDIUM: 0, LOW: 0 };
  allFindings.forEach(f => {
    if (f.risk in riskCounts) riskCounts[f.risk]++;
  });

  console.log(colorize('🚨 风险分布:', 'yellow'));
  console.log(`  ${colorize('HIGH', 'red')}: ${riskCounts.HIGH} 项 （需特别注意）`);
  console.log(`  ${colorize('MEDIUM', 'yellow')}: ${riskCounts.MEDIUM} 项 （需审查）`);
  console.log(`  ${colorize('LOW', 'green')}: ${riskCounts.LOW} 项 （可直接处理）`);
  console.log('');

  // 需人工确认的项目
  const needsConfirmation = allFindings.filter(f => f.requiresConfirmation);
  if (needsConfirmation.length > 0) {
    console.log(colorize('⚠️  需人工确认的项目:', 'yellow'));
    needsConfirmation.forEach(item => {
      console.log(`  - ${colorize(item.category, 'yellow')} (${item.action})`);
      if (item.note) {
        console.log(`    ${colorize(item.note, 'gray')}`);
      }
    });
    console.log('');
  }

  // 保存报告（带时间戳）
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
  const reportPath = path.join(PROJECT_ROOT, `cleanup-analysis-${timestamp}.json`);

  fs.writeFileSync(reportPath, JSON.stringify({
    timestamp: new Date().toISOString(),
    summary: {
      totalFiles,
      totalDirectories: directories.length,
      totalSize,
      riskDistribution: riskCounts,
      needsConfirmationCount: needsConfirmation.length
    },
    gitignoreCoverage,
    findings: allFindings
  }, null, 2));

  console.log(colorize('✅ 详细报告已保存:', 'green'));
  console.log(`   ${reportPath}\n`);

  console.log(colorize('='.repeat(80), 'blue'));
  console.log(colorize('📖 下一步行动', 'yellow') + '\n');
  console.log('  1. 查看详细执行计划: ' + colorize('scripts/cleanup-plan.md', 'blue'));
  console.log('  2. 更新 .gitignore: 添加上述缺失的模式');
  console.log('  3. 备份重要数据: 尤其是数据库备份和审计归档');
  console.log('  4. 审查需确认项: ' + colorize(`${needsConfirmation.length} 项需人工判断`, 'yellow'));
  console.log('  5. 执行清理: 按风险级别分阶段进行');
  console.log(colorize('='.repeat(80), 'blue') + '\n');

  // 生成 .gitignore 补丁建议
  if (gitignoreCoverage.missing.length > 0) {
    console.log(colorize('📝 建议添加到 .gitignore:', 'yellow') + '\n');
    console.log(colorize('# 临时文件和运行期资产', 'gray'));
    gitignoreCoverage.missing.forEach(item => {
      console.log(colorize(item.pattern, 'green') + colorize(`  # ${item.category}`, 'gray'));
    });
    console.log('');
  }
}

// 执行分析
if (require.main === module) {
  generateReport();
}

module.exports = {
  FILE_CATEGORIES,
  DIRECTORIES,
  scanFiles,
  scanDirectories,
  recursiveScan,
  checkGitignoreCoverage
};
