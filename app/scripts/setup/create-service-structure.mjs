#!/usr/bin/env node

/**
 * create-service-structure.mjs
 *
 * Creates the directory skeleton required by the service-layer plan.
 * Safe to run multiple times; existing files are left untouched.
 */

import { mkdirSync, writeFileSync, existsSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();

const targets = [
  "apps/web/src/core/registry",
  "apps/web/src/core/services",
  "apps/web/src/core/hooks",
  "apps/web/src/services",
  "apps/web/src/directives",
  "apps/web/tests/setup",
  "apps/web/tests/utils",
  "apps/web/tests/templates",
];

targets.forEach((relative) => {
  const dir = join(root, relative);
  mkdirSync(dir, { recursive: true });
});

const placeholder = (file, comment) => {
  const fullPath = join(root, file);
  if (existsSync(fullPath)) {
    return;
  }
  writeFileSync(fullPath, `${comment}\n`, "utf8");
};

placeholder("apps/web/src/core/registry/index.ts", "// registry entry point");
placeholder("apps/web/src/core/services/index.ts", "// service manager entry point");
placeholder("apps/web/src/services/index.ts", "// business services entry point");
placeholder("apps/web/tests/setup/README.md", "# Test Setup Helpers\n");

console.log("Service-layer directory structure ensured.");
