#!/usr/bin/env node

/**
 * migration-dashboard.mjs
 *
 * 每周自动生成迁移进度报告
 * 扫描所有 Vue 组件，统计新旧模式使用情况
 */

import { readdirSync, readFileSync, existsSync } from "node:fs";
import { join, relative } from "node:path";

const srcDir = join(process.cwd(), "apps/web/src");

// 统计数据
const stats = {
  totalComponents: 0,
  migratedComponents: 0,
  partiallyMigrated: 0,
  notMigrated: 0,
  oldPatternFiles: [],
  newPatternFiles: [],
};

// 旧模式标识
const OLD_PATTERNS = [
  "ElNotification",
  "ElMessage",
  "ElMessageBox",
  /import.*apiFetch.*from ['"]@\/api\/http['"]/,
];

// 新模式标识
const NEW_PATTERNS = ["useNotification", "useApprovalWorkflow", "useService", "usePermission"];

/**
 * 检查文件是否使用旧模式
 */
function hasOldPattern(content) {
  return OLD_PATTERNS.some((pattern) => {
    if (pattern instanceof RegExp) {
      return pattern.test(content);
    }
    return content.includes(pattern);
  });
}

/**
 * 检查文件是否使用新模式
 */
function hasNewPattern(content) {
  return NEW_PATTERNS.some((pattern) => content.includes(pattern));
}

/**
 * 扫描目录
 */
function scanDirectory(dir) {
  if (!existsSync(dir)) {
    console.error(`Directory not found: ${dir}`);
    return;
  }

  const files = readdirSync(dir, { withFileTypes: true });

  files.forEach((file) => {
    const fullPath = join(dir, file.name);

    if (file.isDirectory()) {
      // 跳过 node_modules, tests 等目录
      if (!["node_modules", "tests", "dist", ".backup"].includes(file.name)) {
        scanDirectory(fullPath);
      }
    } else if (file.name.endsWith(".vue")) {
      stats.totalComponents++;

      const content = readFileSync(fullPath, "utf8");
      const hasOld = hasOldPattern(content);
      const hasNew = hasNewPattern(content);
      const relativePath = relative(srcDir, fullPath);

      if (hasOld && hasNew) {
        // 部分迁移（同时使用新旧模式）
        stats.partiallyMigrated++;
        stats.oldPatternFiles.push(relativePath);
      } else if (hasOld) {
        // 未迁移（仅使用旧模式）
        stats.notMigrated++;
        stats.oldPatternFiles.push(relativePath);
      } else if (hasNew) {
        // 已迁移（仅使用新模式）
        stats.migratedComponents++;
        stats.newPatternFiles.push(relativePath);
      }
      // 否则：既不使用旧模式也不使用新模式（可能是纯展示组件）
    }
  });
}

/**
 * 生成报告
 */
function generateReport() {
  const migrationRate = ((stats.migratedComponents / stats.totalComponents) * 100).toFixed(1);
  const partialRate = ((stats.partiallyMigrated / stats.totalComponents) * 100).toFixed(1);
  const notMigratedRate = ((stats.notMigrated / stats.totalComponents) * 100).toFixed(1);

  console.log("");
  console.log("╔════════════════════════════════════════════════════════════╗");
  console.log("║         📊 服务层迁移进度仪表板 (Weekly Report)          ║");
  console.log("╚════════════════════════════════════════════════════════════╝");
  console.log("");
  console.log(`📅 生成时间: ${new Date().toLocaleString("zh-CN")}`);
  console.log("");
  console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
  console.log("  总览");
  console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
  console.log(`  总组件数:       ${stats.totalComponents}`);
  console.log(`  ✅ 已迁移:      ${stats.migratedComponents} (${migrationRate}%)`);
  console.log(`  ⚠️  部分迁移:    ${stats.partiallyMigrated} (${partialRate}%)`);
  console.log(`  ❌ 未迁移:      ${stats.notMigrated} (${notMigratedRate}%)`);
  console.log("");

  // 进度条
  const barWidth = 50;
  const migratedBars = Math.round((stats.migratedComponents / stats.totalComponents) * barWidth);
  const partialBars = Math.round((stats.partiallyMigrated / stats.totalComponents) * barWidth);
  const notMigratedBars = barWidth - migratedBars - partialBars;

  console.log("  进度:");
  console.log(
    `  [${"█".repeat(migratedBars)}${"▒".repeat(partialBars)}${"░".repeat(notMigratedBars)}] ${migrationRate}%`
  );
  console.log("");

  console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
  console.log("  状态评估");
  console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

  const rate = parseFloat(migrationRate);
  if (rate < 30) {
    console.log("  🔴 迁移进度低于 30%，请加快迁移速度！");
    console.log("  建议: 增加人手或调整计划");
  } else if (rate < 50) {
    console.log("  🟡 迁移进度低于 50%，需要关注");
    console.log("  建议: 检查是否有阻塞问题");
  } else if (rate < 80) {
    console.log("  🟢 迁移进度良好，继续保持");
  } else if (rate < 100) {
    console.log("  🎯 迁移即将完成，最后冲刺！");
    console.log(`  建议: 优先处理剩余 ${stats.notMigrated} 个未迁移组件`);
  } else {
    console.log("  🎉 恭喜！迁移已完成！");
  }

  console.log("");

  // 部分迁移警告
  if (stats.partiallyMigrated > 0) {
    console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    console.log("  ⚠️  需要清理的组件（同时使用新旧模式）");
    console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    const partialFiles = stats.oldPatternFiles
      .filter((file) => stats.newPatternFiles.includes(file))
      .slice(0, 10);

    partialFiles.forEach((file) => {
      console.log(`  - ${file}`);
    });

    if (stats.partiallyMigrated > 10) {
      console.log(`  ... 还有 ${stats.partiallyMigrated - 10} 个文件`);
    }
    console.log("");
  }

  // 未迁移组件列表（只显示前10个）
  if (stats.notMigrated > 0) {
    console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    console.log("  待迁移组件（前10个）");
    console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    const notMigratedFiles = stats.oldPatternFiles
      .filter((file) => !stats.newPatternFiles.includes(file))
      .slice(0, 10);

    notMigratedFiles.forEach((file) => {
      console.log(`  - ${file}`);
    });

    if (stats.notMigrated > 10) {
      console.log(`  ... 还有 ${stats.notMigrated - 10} 个文件`);
    }
    console.log("");
  }

  console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
  console.log("  下一步行动");
  console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

  if (stats.partiallyMigrated > 0) {
    console.log("  1. 清理部分迁移的组件，移除旧模式");
  }
  if (stats.notMigrated > 0) {
    console.log("  2. 继续迁移剩余组件");
  }
  if (rate >= 80) {
    console.log("  3. 考虑启用 ESLint 规则禁止旧模式");
  }
  if (rate >= 100) {
    console.log("  ✅ 迁移完成！可以进入性能优化和文档完善阶段");
  }

  console.log("");
  console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
  console.log("");
}

// 执行扫描
console.log("🔍 正在扫描组件...");
scanDirectory(srcDir);
generateReport();
