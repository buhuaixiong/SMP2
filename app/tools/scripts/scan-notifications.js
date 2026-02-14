#!/usr/bin/env node

/**
 * 扫描代码中 ElNotification/ElMessage 的使用情况，生成迁移清单
 */

import { readdirSync, statSync, readFileSync, mkdirSync, writeFileSync } from "node:fs";
import { join, extname, relative } from "node:path";

const ROOT = process.cwd();
const TARGET_DIR = process.argv[2] ?? "apps/web/src";
const OUTPUT_DIR = join(ROOT, "var/migration");
const OUTPUT_FILE = join(OUTPUT_DIR, "notification-usage.json");

const EXTENSIONS = new Set([".ts", ".tsx", ".js", ".mjs", ".vue"]);
const PATTERN = /(ElNotification|ElMessage|ElMessageBox)/;

function collectFiles(dir) {
  const results = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.name.startsWith(".") || entry.name === "node_modules") continue;
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...collectFiles(full));
    } else if (EXTENSIONS.has(extname(entry.name))) {
      results.push(full);
    }
  }
  return results;
}

function analyzeFile(file) {
  const content = readFileSync(file, "utf8");
  const lines = content.split(/\r?\n/);
  const findings = [];
  lines.forEach((line, index) => {
    if (PATTERN.test(line)) {
      findings.push({
        line: index + 1,
        match: line.trim().slice(0, 180),
      });
    }
  });
  return findings;
}

const files = collectFiles(join(ROOT, TARGET_DIR));
const findings = [];

files.forEach((file) => {
  const matches = analyzeFile(file);
  if (matches.length > 0) {
    findings.push({
      file: relative(ROOT, file).replace(/\\/g, "/"),
      occurrences: matches.length,
      samples: matches,
    });
  }
});

mkdirSync(OUTPUT_DIR, { recursive: true });
writeFileSync(
  OUTPUT_FILE,
  JSON.stringify(
    {
      generatedAt: new Date().toISOString(),
      targetDir: TARGET_DIR,
      totalFiles: findings.length,
      totalOccurrences: findings.reduce((sum, item) => sum + item.occurrences, 0),
      findings,
    },
    null,
    2,
  ),
  "utf8",
);

console.log(`Notification scan completed. Report saved to ${relative(ROOT, OUTPUT_FILE)}`);
