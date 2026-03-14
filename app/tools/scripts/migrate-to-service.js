#!/usr/bin/env node

/**
 * Automatically migrates Element Plus notification APIs to the notification service.
 *
 * Usage:
 *   node tools/scripts/migrate-to-service.js path/to/file.vue --write
 *   node tools/scripts/migrate-to-service.js apps/web/src/views --write
 */

const { readFileSync, writeFileSync, readdirSync, statSync } = require("node:fs");
const { join, extname } = require("node:path");

const ROOT = process.cwd();
const args = process.argv.slice(2);
const write = args.includes("--write");
const targets = args.filter((arg) => !arg.startsWith("--"));

if (targets.length === 0) {
  console.log("Usage: node tools/scripts/migrate-to-service.js <fileOrDir> [--write]");
  process.exit(0);
}

const fileExtensions = new Set([".ts", ".tsx", ".js", ".vue"]);
const skipFiles = new Set(["apps/web/src/services/notification.ts"]);
const ELEMENT_PLUS_TARGETS = new Set(["ElMessage", "ElNotification", "ElMessageBox"]);

function collectFiles(target) {
  const full = join(ROOT, target);
  const stats = statSync(full);
  if (stats.isDirectory()) {
    const files = [];
    for (const entry of readdirSync(full)) {
      if (entry.startsWith(".") || entry === "node_modules") continue;
      files.push(...collectFiles(join(target, entry)));
    }
    return files;
  }
  if (fileExtensions.has(extname(full))) {
    return [target];
  }
  return [];
}

function ensureImport(code, statement) {
  if (code.includes(statement)) {
    return code;
  }
  const lines = code.split(/\r?\n/);
  let insertIndex = 0;
  for (let i = 0; i < lines.length; i += 1) {
    if (lines[i].trim().startsWith("import")) {
      insertIndex = i + 1;
    }
  }
  lines.splice(insertIndex, 0, statement);
  return lines.join("\n");
}

function ensureNotificationVariable(code) {
  const snippet = "const notification = useNotification();";
  if (code.includes(snippet)) {
    return code;
  }

  const lines = code.split(/\r?\n/);
  let insertIndex = 0;
  for (let i = 0; i < lines.length; i += 1) {
    if (lines[i].trim().startsWith("import")) {
      insertIndex = i + 1;
    }
  }
  lines.splice(insertIndex, 0, snippet);
  return lines.join("\n");
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function cleanupElementPlusImports(code) {
  const importRegex = /import\s+{([^}]+)}\s+from\s+["']element-plus["'];?/g;
  let match;
  let result = code;
  let changed = false;

  while ((match = importRegex.exec(result))) {
    const full = match[0];
    const specifiers = match[1]
      .split(",")
      .map((item) => item.trim())
      .filter(Boolean);

    if (specifiers.length === 0) continue;

    const withoutImport = result.slice(0, match.index) + result.slice(match.index + full.length);
    const kept = specifiers.filter((specifier) => {
      const normalized = specifier.replace(/^type\s+/, "").split(/\s+as\s+/)[0].trim();
      if (!ELEMENT_PLUS_TARGETS.has(normalized)) return true;
      const pattern = new RegExp(`\\b${escapeRegExp(normalized)}\\b`);
      return pattern.test(withoutImport);
    });

    if (kept.length === specifiers.length) continue;

    changed = true;
    const replacement = kept.length > 0 ? `import { ${kept.join(", ")} } from "element-plus";` : "";
    result = `${result.slice(0, match.index)}${replacement}${result.slice(match.index + full.length)}`;
    importRegex.lastIndex = match.index + replacement.length;
  }

  return { code: result, changed };
}

function cleanupNotificationTypeImports(code) {
  const importRegex = /import\s+type\s+{([^}]+)}\s+from\s+["']@\/services["'];?/g;
  let match;
  let result = code;
  let changed = false;

  while ((match = importRegex.exec(result))) {
    const full = match[0];
    const specifiers = match[1]
      .split(",")
      .map((item) => item.trim())
      .filter(Boolean);
    if (specifiers.length === 0) continue;

    const withoutImport = result.slice(0, match.index) + result.slice(match.index + full.length);
    const kept = specifiers.filter((specifier) => {
      const normalized = specifier.replace(/^type\s+/, "").split(/\s+as\s+/)[0].trim();
      if (normalized !== "NotificationService") {
        return true;
      }
      const pattern = new RegExp(`\\b${escapeRegExp(normalized)}\\b`);
      return pattern.test(withoutImport);
    });

    if (kept.length === specifiers.length) continue;

    changed = true;
    const replacement = kept.length > 0 ? `import type { ${kept.join(", ")} } from "@/services";` : "";
    result = `${result.slice(0, match.index)}${replacement}${result.slice(match.index + full.length)}`;
    importRegex.lastIndex = match.index + replacement.length;
  }

  return { code: result, changed };
}

function upgradeLegacyNotificationHook(code) {
  const legacySnippet = 'const notification = useService<NotificationService>("notification");';
  if (!code.includes(legacySnippet)) {
    return { code, changed: false };
  }
  return { code: code.replaceAll(legacySnippet, "const notification = useNotification();"), changed: true };
}

function cleanupUseServiceImports(code) {
  const importRegex = /import\s+{([^}]+)}\s+from\s+["']@\/core\/hooks["'];?/g;
  let match;
  let result = code;
  let changed = false;

  while ((match = importRegex.exec(result))) {
    const full = match[0];
    const specifiers = match[1]
      .split(",")
      .map((item) => item.trim())
      .filter(Boolean);
    if (specifiers.length === 0) continue;

    const withoutImport = result.slice(0, match.index) + result.slice(match.index + full.length);
    const kept = specifiers.filter((specifier) => {
      if (specifier !== "useService") {
        return true;
      }
      const pattern = new RegExp(`\\b${escapeRegExp(specifier)}\\b`);
      return pattern.test(withoutImport);
    });

    if (kept.length === specifiers.length) continue;

    changed = true;
    const replacement = kept.length > 0 ? `import { ${kept.join(", ")} } from "@/core/hooks";` : "";
    result = `${result.slice(0, match.index)}${replacement}${result.slice(match.index + full.length)}`;
    importRegex.lastIndex = match.index + replacement.length;
  }

  return { code: result, changed };
}

function transformScript(script) {
  let code = script;
  let replaced = false;

  const replacements = [
    { regex: /ElNotification\.success/g, replacement: "notification.success" },
    { regex: /ElNotification\.warning/g, replacement: "notification.warning" },
    { regex: /ElNotification\.info/g, replacement: "notification.info" },
    { regex: /ElNotification\.error/g, replacement: "notification.error" },
    { regex: /ElMessageBox\.confirm/g, replacement: "notification.confirm" },
    { regex: /ElMessageBox\.prompt/g, replacement: "notification.prompt" },
    { regex: /ElMessageBox\.alert/g, replacement: "notification.alert" },
    { regex: /ElMessage\.success/g, replacement: "notification.success" },
    { regex: /ElMessage\.error/g, replacement: "notification.error" },
    { regex: /ElMessage\.warning/g, replacement: "notification.warning" },
    { regex: /ElMessage\.info/g, replacement: "notification.info" },
  ];

  replacements.forEach(({ regex, replacement }) => {
    if (regex.test(code)) {
      code = code.replace(regex, replacement);
      replaced = true;
    }
  });

  const { code: upgradedCode, changed: legacyChanged } = upgradeLegacyNotificationHook(code);
  code = upgradedCode;
  if (legacyChanged) {
    replaced = true;
  }

  if (replaced) {
    code = ensureImport(code, 'import { useNotification } from "@/composables";');
    code = ensureNotificationVariable(code);
  }

  const { code: cleanedCode, changed } = cleanupElementPlusImports(code);
  const { code: typeCleanedCode, changed: typeChanged } = cleanupNotificationTypeImports(cleanedCode);
  const { code: hookCleanedCode, changed: hookChanged } = cleanupUseServiceImports(typeCleanedCode);
  const finalChanged = replaced || changed || typeChanged || hookChanged;
  return { code: hookCleanedCode, changed: finalChanged };
}

function transformVue(content) {
  const match = content.match(/<script\s+setup[^>]*>([\s\S]*?)<\/script>/);
  if (!match) return { code: content, changed: false };
  const script = match[1];
  const { code: newScript, changed } = transformScript(script);
  if (!changed) return { code: content, changed: false };
  return {
    code: content.replace(match[0], `${match[0].split(">")[0]}>\n${newScript}\n</script>`),
    changed: true,
  };
}

function transformFile(file) {
  const fullPath = join(ROOT, file);
  const source = readFileSync(fullPath, "utf8");
  const normalized = file.replace(/\\/g, "/");
  if (skipFiles.has(normalized)) {
    return { code: source, changed: false };
  }
  const ext = extname(file);
  if (ext === ".vue") {
    return transformVue(source);
  }
  return transformScript(source);
}

const filesToProcess = targets.flatMap((target) => collectFiles(target));
const results = [];

filesToProcess.forEach((file) => {
  const { code, changed } = transformFile(file);
  if (changed) {
    if (write) {
      writeFileSync(join(ROOT, file), code, "utf8");
    }
    results.push(file);
  }
});

if (results.length === 0) {
  console.log("No Element Plus notification usages found.");
} else if (write) {
  console.log(`Updated ${results.length} file(s):`);
  results.forEach((file) => console.log(`  - ${file}`));
} else {
  console.log("Files that would be updated (rerun with --write to apply):");
  results.forEach((file) => console.log(`  - ${file}`));
}
