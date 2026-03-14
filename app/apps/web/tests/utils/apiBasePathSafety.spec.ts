import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const srcRoot = join(process.cwd(), "src");
const allowedExtensions = new Set([".ts", ".vue"]);
const forbiddenPatterns = [
  /fetch\(\s*`\/api\//,
  /fetch\(\s*"\/api\//,
  /fetch\(\s*'\/api\//,
];

const collectFiles = (dir: string): string[] => {
  const files: string[] = [];

  for (const entry of readdirSync(dir)) {
    const fullPath = join(dir, entry);
    const stats = statSync(fullPath);

    if (stats.isDirectory()) {
      files.push(...collectFiles(fullPath));
      continue;
    }

    if ([...allowedExtensions].some((extension) => fullPath.endsWith(extension))) {
      files.push(fullPath);
    }
  }

  return files;
};

describe("api base path safety", () => {
  it("does not use raw fetch calls against root /api paths", () => {
    const violations: string[] = [];

    for (const file of collectFiles(srcRoot)) {
      const content = readFileSync(file, "utf8");
      for (const pattern of forbiddenPatterns) {
        if (pattern.test(content)) {
          violations.push(file.replace(`${process.cwd()}\\`, ""));
          break;
        }
      }
    }

    expect(violations).toEqual([]);
  });
});
