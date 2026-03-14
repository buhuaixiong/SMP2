import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { chromium } from "@playwright/test";

const DEFAULTS = {
  url: "http://127.0.0.1:4173",
  runs: 5,
  waitMs: 4000,
  outDir: "artifacts/perf-baseline",
  headless: true,
};

function printHelp() {
  console.log(`
Collect frontend performance baseline metrics.

Usage:
  node scripts/collect-web-metrics.mjs [options]

Options:
  --url <url>         Target URL (default: ${DEFAULTS.url})
  --runs <number>     Number of samples (default: ${DEFAULTS.runs})
  --wait-ms <number>  Extra wait time per run in ms (default: ${DEFAULTS.waitMs})
  --out-dir <path>    Output directory (default: ${DEFAULTS.outDir})
  --headed            Run browser in headed mode
  --help              Show this help

Examples:
  node scripts/collect-web-metrics.mjs --url http://127.0.0.1:5173 --runs 7
  npm run perf:baseline -- --url http://127.0.0.1:4173 --runs 5
`.trim());
}

function parseArgs(argv) {
  const options = { ...DEFAULTS };
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--help") {
      options.help = true;
      continue;
    }
    if (arg === "--headed") {
      options.headless = false;
      continue;
    }
    if (arg.startsWith("--")) {
      const key = arg.slice(2);
      const value = argv[index + 1];
      if (value === undefined || value.startsWith("--")) {
        throw new Error(`Missing value for argument ${arg}`);
      }
      index += 1;
      if (key === "url") options.url = value;
      else if (key === "runs") options.runs = Number(value);
      else if (key === "wait-ms") options.waitMs = Number(value);
      else if (key === "out-dir") options.outDir = value;
      else throw new Error(`Unknown argument: ${arg}`);
    }
  }

  if (!Number.isInteger(options.runs) || options.runs < 1) {
    throw new Error("--runs must be a positive integer");
  }
  if (!Number.isFinite(options.waitMs) || options.waitMs < 0) {
    throw new Error("--wait-ms must be >= 0");
  }

  return options;
}

function percentile(sortedValues, p) {
  if (!sortedValues.length) return null;
  const rank = (p / 100) * (sortedValues.length - 1);
  const lower = Math.floor(rank);
  const upper = Math.ceil(rank);
  if (lower === upper) return sortedValues[lower];
  const weight = rank - lower;
  return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
}

function aggregateNumeric(values) {
  const numeric = values.filter((value) => typeof value === "number" && Number.isFinite(value));
  if (!numeric.length) return null;
  const sorted = [...numeric].sort((a, b) => a - b);
  return {
    min: sorted[0],
    p50: percentile(sorted, 50),
    p75: percentile(sorted, 75),
    p95: percentile(sorted, 95),
    max: sorted[sorted.length - 1],
    avg: sorted.reduce((sum, value) => sum + value, 0) / sorted.length,
  };
}

async function collectSingleRun(browser, options, runIndex) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();

  let requestCount = 0;
  let failedRequestCount = 0;
  let response4xx5xxCount = 0;
  let transferredBytes = 0;

  page.on("request", () => {
    requestCount += 1;
  });

  page.on("requestfailed", () => {
    failedRequestCount += 1;
  });

  page.on("response", async (response) => {
    if (response.status() >= 400) {
      response4xx5xxCount += 1;
    }
    const headers = await response.allHeaders();
    const contentLength = Number(headers["content-length"]);
    if (Number.isFinite(contentLength) && contentLength > 0) {
      transferredBytes += contentLength;
    }
  });

  await page.addInitScript(() => {
    window.__perfCollector = {
      lcp: null,
      maxEventDuration: null,
      longTaskCount: 0,
      longTaskDuration: 0,
    };

    const supports = (type) =>
      typeof PerformanceObserver !== "undefined" &&
      PerformanceObserver.supportedEntryTypes?.includes(type);

    if (supports("largest-contentful-paint")) {
      const lcpObserver = new PerformanceObserver((entryList) => {
        const entries = entryList.getEntries();
        const last = entries[entries.length - 1];
        if (last?.startTime != null) {
          window.__perfCollector.lcp = last.startTime;
        }
      });
      lcpObserver.observe({ type: "largest-contentful-paint", buffered: true });
    }

    if (supports("event")) {
      const eventObserver = new PerformanceObserver((entryList) => {
        for (const entry of entryList.getEntries()) {
          const interactionId = entry.interactionId ?? 0;
          if (interactionId > 0) {
            const duration = entry.duration ?? 0;
            if (
              window.__perfCollector.maxEventDuration === null ||
              duration > window.__perfCollector.maxEventDuration
            ) {
              window.__perfCollector.maxEventDuration = duration;
            }
          }
        }
      });
      eventObserver.observe({ type: "event", buffered: true, durationThreshold: 16 });
    }

    if (supports("longtask")) {
      const longTaskObserver = new PerformanceObserver((entryList) => {
        for (const entry of entryList.getEntries()) {
          window.__perfCollector.longTaskCount += 1;
          window.__perfCollector.longTaskDuration += entry.duration ?? 0;
        }
      });
      longTaskObserver.observe({ type: "longtask", buffered: true });
    }
  });

  const startedAt = Date.now();
  await page.goto(options.url, { waitUntil: "domcontentloaded", timeout: 45000 });
  await page.waitForLoadState("networkidle", { timeout: 45000 }).catch(() => undefined);

  await page.mouse.move(500, 300).catch(() => undefined);
  await page.mouse.wheel(0, 600).catch(() => undefined);
  await page.mouse.click(500, 300).catch(() => undefined);
  await page.waitForTimeout(options.waitMs);

  const payload = await page.evaluate(() => {
    const nav = performance.getEntriesByType("navigation")[0];
    const paints = performance.getEntriesByType("paint");
    const fcp = paints.find((entry) => entry.name === "first-contentful-paint")?.startTime ?? null;
    const lcpFromCollector = window.__perfCollector?.lcp ?? null;

    return {
      fcp,
      lcp: lcpFromCollector,
      inpApprox: window.__perfCollector?.maxEventDuration ?? null,
      longTaskCount: window.__perfCollector?.longTaskCount ?? 0,
      longTaskDuration: window.__perfCollector?.longTaskDuration ?? 0,
      domContentLoaded: nav?.domContentLoadedEventEnd ?? null,
      loadEventEnd: nav?.loadEventEnd ?? null,
      navigationDuration: nav?.duration ?? null,
    };
  });

  await context.close();
  return {
    run: runIndex,
    ...payload,
    requestCount,
    failedRequestCount,
    response4xx5xxCount,
    transferredBytes,
    wallTimeMs: Date.now() - startedAt,
  };
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) {
    printHelp();
    return;
  }

  const browser = await chromium.launch({ headless: options.headless });
  const runs = [];

  try {
    for (let runIndex = 1; runIndex <= options.runs; runIndex += 1) {
      const result = await collectSingleRun(browser, options, runIndex);
      runs.push(result);
      console.log(
        `[run ${runIndex}/${options.runs}] LCP=${result.lcp ?? "n/a"}ms, INP≈${result.inpApprox ?? "n/a"}ms, LongTasks=${result.longTaskCount}, Requests=${result.requestCount}`,
      );
    }
  } finally {
    await browser.close();
  }

  const summary = {
    lcp: aggregateNumeric(runs.map((run) => run.lcp)),
    inpApprox: aggregateNumeric(runs.map((run) => run.inpApprox)),
    longTaskCount: aggregateNumeric(runs.map((run) => run.longTaskCount)),
    longTaskDuration: aggregateNumeric(runs.map((run) => run.longTaskDuration)),
    requestCount: aggregateNumeric(runs.map((run) => run.requestCount)),
    wallTimeMs: aggregateNumeric(runs.map((run) => run.wallTimeMs)),
  };

  const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
  const output = {
    generatedAt: new Date().toISOString(),
    tool: "collect-web-metrics.mjs",
    options,
    summary,
    runs,
  };

  const outputDir = path.resolve(process.cwd(), options.outDir);
  await mkdir(outputDir, { recursive: true });
  const outputPath = path.join(outputDir, `${timestamp}.json`);
  await writeFile(outputPath, `${JSON.stringify(output, null, 2)}\n`, "utf8");

  console.log(`Saved baseline report: ${outputPath}`);
}

main().catch((error) => {
  const hint =
    String(error?.message || "").includes("Executable doesn't exist") ||
    String(error?.message || "").includes("browserType.launch")
      ? "\nHint: run `npx playwright install chromium` first."
      : "";
  console.error(`collect-web-metrics failed: ${error instanceof Error ? error.message : String(error)}${hint}`);
  process.exitCode = 1;
});

