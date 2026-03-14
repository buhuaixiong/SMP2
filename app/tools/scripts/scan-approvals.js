#!/usr/bin/env node

/**
 * 扫描审批流程相关逻辑，生成重复点清单
 */

import { readdirSync, readFileSync, mkdirSync, writeFileSync } from "node:fs";
import { join, extname, relative } from "node:path";

const ROOT = process.cwd();
const TARGET_DIR = process.argv[2] ?? "apps/web/src";
const OUTPUT_DIR = join(ROOT, "var/migration");
const OUTPUT_FILE = join(OUTPUT_DIR, "approval-usage.json");

const EXTENSIONS = new Set([".ts", ".tsx", ".js", ".vue"]);
const KEYWORDS = [
  /approval/i,
  /approve/i,
  /workflow/i,
  /审批/,
];

function collectFiles(dir) {
  const entries = readdirSync(dir, { withFileTypes: true });
  const results = [];
  for (const entry of entries) {
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

function analyze(file) {
  const content = readFileSync(file, "utf8");
  const lines = content.split(/\r?\n/);
  const result = [];
  lines.forEach((line, idx) => {
    if (KEYWORDS.some((regex) => regex.test(line))) {
      result.push({
        line: idx + 1,
        text: line.trim().slice(0, 160),
      });
    }
  });
  return result;
}

const files = collectFiles(join(ROOT, TARGET_DIR));
const findings = [];

files.forEach((file) => {
  const matches = analyze(file);
  if (matches.length) {
    findings.push({
      file: relative(ROOT, file).replace(/\\/g, "/"),
      occurrences: matches.length,
      samples: matches.slice(0, 10),
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

console.log(`Approval scan completed. Report saved to ${relative(ROOT, OUTPUT_FILE)}`);
