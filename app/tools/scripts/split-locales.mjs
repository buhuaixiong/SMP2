import fs from "node:fs";
import path from "node:path";

const LOCALES_DIR = path.resolve("src/locales");
const BACKUP_DIR = path.join(LOCALES_DIR, "backups");
const SOURCE_LOCALES = ["en", "th", "zh"];

const writeJsonFile = (filePath, data) => {
  const content = `${JSON.stringify(data, null, 2)}\n`;
  fs.writeFileSync(filePath, content, "utf8");
};

for (const locale of SOURCE_LOCALES) {
  const primarySource = path.join(LOCALES_DIR, `${locale}.json`);
  const backupSource = path.join(BACKUP_DIR, `${locale}.json.backup`);
  const sourcePath = fs.existsSync(primarySource) ? primarySource : backupSource;

  if (!fs.existsSync(sourcePath)) {
    throw new Error(
      `Missing source locale file for ${locale}. Checked: ${primarySource}, ${backupSource}`,
    );
  }

  const destinationDir = path.join(LOCALES_DIR, locale);
  fs.mkdirSync(destinationDir, { recursive: true });

  const raw = fs.readFileSync(sourcePath, "utf8");
  const sourceData = JSON.parse(raw);

  Object.entries(sourceData).forEach(([moduleKey, moduleValue]) => {
    const targetPath = path.join(destinationDir, `${moduleKey}.json`);
    writeJsonFile(targetPath, moduleValue);
  });
}
