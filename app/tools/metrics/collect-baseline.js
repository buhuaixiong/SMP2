#!/usr/bin/env node

import { mkdirSync, readFileSync, writeFileSync, readdirSync, statSync, rmSync, existsSync } from "node:fs";
import { join, extname } from "node:path";
import { Octokit } from "@octokit/rest";
import { execSync } from "node:child_process";

const ROOT = process.cwd();
const SRC_RELATIVE = "apps/web/src";
const SRC_ABSOLUTE = join(ROOT, SRC_RELATIVE);
const METRIC_DIR = join(ROOT, "var/metrics");

const EXTENSIONS = new Set([".ts", ".tsx", ".js", ".vue"]);

function collectFiles(dir, entries = []) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.name.startsWith(".") || entry.name === "node_modules") continue;
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      collectFiles(full, entries);
    } else if (EXTENSIONS.has(extname(entry.name))) {
      entries.push(full);
    }
  }
  return entries;
}

function countMatches(patterns) {
  const files = collectFiles(SRC_ABSOLUTE);
  let count = 0;
  files.forEach((file) => {
    const content = readFileSync(file, "utf8");
    content.split(/\r?\n/).forEach((line) => {
      if (patterns.some((regex) => regex.test(line))) {
        count += 1;
      }
    });
  });
  return count;
}

function collectDuplication() {
  const bin = process.platform === "win32" ? "jscpd.cmd" : "jscpd";
  const executable = join(ROOT, "node_modules", ".bin", bin);
  execSync(`"${executable}" "${SRC_RELATIVE}" --reporters json --output "${METRIC_DIR}" --silent`, {
    cwd: ROOT,
    stdio: "ignore",
  });
  const reportFile = join(METRIC_DIR, "jscpd-report.json");
  if (!existsSync(reportFile)) {
    throw new Error(`jscpd report not found at ${reportFile}`);
  }
  const report = JSON.parse(readFileSync(reportFile, "utf8"));
  rmSync(reportFile, { force: true });
  return report.statistics.total.percentage;
}

async function collectGithubMetrics() {
  const token = process.env.GITHUB_TOKEN;
  const repoSlug = process.env.GITHUB_REPOSITORY;
  if (!token || !repoSlug || !repoSlug.includes("/")) {
    return null;
  }
  const [owner, repo] = repoSlug.split("/");
  const octokit = new Octokit({ auth: token });
  const pulls = await octokit.paginate(octokit.pulls.list, {
    owner,
    repo,
    state: "closed",
    per_page: 100,
  });
  const since = Date.now() - 30 * 24 * 60 * 60 * 1000;
  const recent = pulls.filter((pr) => new Date(pr.updated_at).getTime() >= since);
  const durations = recent.map((pr) => new Date(pr.closed_at ?? pr.updated_at).getTime() - new Date(pr.created_at).getTime());
  return {
    pullRequests: recent.length,
    averageReviewTimeHours: durations.length
      ? Number((durations.reduce((a, b) => a + b, 0) / durations.length / 3_600_000).toFixed(2))
      : 0,
  };
}

async function main() {
  mkdirSync(METRIC_DIR, { recursive: true });
  const duplicateRate = collectDuplication();
  const notificationCalls = countMatches([/ElNotification/, /ElMessage/, /ElMessageBox/]);
  const approvalCalls = countMatches([/approval/i, /workflow/i]);
  const github = await collectGithubMetrics();

  const data = {
    capturedAt: new Date().toISOString(),
    duplicateCodeRate: duplicateRate,
    notificationCalls,
    approvalCalls,
    github,
  };

  const dateTag = data.capturedAt.slice(0, 10);
  const output = join(METRIC_DIR, `baseline-${dateTag}.json`);
  writeFileSync(output, JSON.stringify(data, null, 2), "utf8");
  console.log(`Baseline metrics saved to ${output}`);
}

main().catch((error) => {
  console.error("Failed to collect baseline metrics", error);
  process.exit(1);
});
