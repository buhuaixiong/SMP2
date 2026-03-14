import fs from "node:fs";
import path from "node:path";

const LOCALES_DIR = path.resolve("src/locales");
const SUPPORTED_LOCALES = ["en", "zh", "th"];
const BASE_LOCALE = "en";

const readJsonFile = (filePath) => {
  const content = fs.readFileSync(filePath, "utf8").replace(/^\uFEFF/, "");
  return JSON.parse(content);
};

const loadLocaleModules = (locale) => {
  const localeDir = path.join(LOCALES_DIR, locale);
  if (!fs.existsSync(localeDir)) {
    throw new Error(`Locale directory missing: ${localeDir}`);
  }
  const files = fs.readdirSync(localeDir).filter((file) => file.endsWith(".json"));

  return Object.fromEntries(
    files.map((file) => [path.basename(file, ".json"), readJsonFile(path.join(localeDir, file))]),
  );
};

const collectKeyPaths = (node, prefix = [], paths = new Set()) => {
  if (node === null || node === undefined) {
    return paths;
  }
  if (Array.isArray(node)) {
    for (const item of node) {
      collectKeyPaths(item, [...prefix, "[]"], paths);
    }
    return paths;
  }
  if (typeof node === "object") {
    for (const [key, value] of Object.entries(node)) {
      const next = [...prefix, key];
      paths.add(next.join("."));
      collectKeyPaths(value, next, paths);
    }
  }
  return paths;
};

const localeModules = new Map();
for (const locale of SUPPORTED_LOCALES) {
  localeModules.set(locale, loadLocaleModules(locale));
}

const baseModules = localeModules.get(BASE_LOCALE);
const moduleNames = new Set(Object.keys(baseModules));

let hasIssues = false;

const report = (message) => {
  hasIssues = true;
  console.warn(message);
};

for (const moduleName of moduleNames) {
  const baseModule = baseModules[moduleName];

  for (const locale of SUPPORTED_LOCALES) {
    const modules = localeModules.get(locale);
    if (!(moduleName in modules)) {
      report(`[${locale}] missing module: ${moduleName}`);
      continue;
    }

    const target = modules[moduleName];
    const baseKeys = collectKeyPaths(baseModule);
    const targetKeys = collectKeyPaths(target);

    const missingKeys = [...baseKeys].filter((key) => !targetKeys.has(key));

    if (missingKeys.length > 0) {
      report(`[${locale}] ${moduleName} missing keys: ${missingKeys.join(", ")}`);
    }
  }
}

if (!hasIssues) {
  console.log("All locale keys are aligned.");
}

process.exitCode = hasIssues ? 1 : 0;
