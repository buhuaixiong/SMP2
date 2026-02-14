const fs = require('fs');
const path = require('path');

function deepMerge(target, source) {
  const output = Object.assign({}, target);

  if (isObject(target) && isObject(source)) {
    Object.keys(source).forEach(key => {
      if (isObject(source[key])) {
        if (!(key in target)) {
          Object.assign(output, { [key]: source[key] });
        } else {
          output[key] = deepMerge(target[key], source[key]);
        }
      } else {
        Object.assign(output, { [key]: source[key] });
      }
    });
  }

  return output;
}

function isObject(item) {
  return item && typeof item === 'object' && !Array.isArray(item);
}

function mergeLocale(locale) {
  const localePath = path.join(__dirname, 'src', 'locales', `${locale}.json`);
  const patchPath = path.join(__dirname, 'i18n-patches', `${locale}-patch.json`);

  if (!fs.existsSync(patchPath)) {
    console.log(`⚠️  No patch file found for ${locale}, skipping...`);
    return false;
  }

  const original = JSON.parse(fs.readFileSync(localePath, 'utf8'));
  const patch = JSON.parse(fs.readFileSync(patchPath, 'utf8'));

  const merged = deepMerge(original, patch);

  fs.writeFileSync(localePath, JSON.stringify(merged, null, 2), 'utf8');
  console.log(`✅ ${locale.toUpperCase()} translations merged successfully!`);
  return true;
}

// 处理所有语言
const locales = ['en', 'zh', 'th'];
let processed = 0;

console.log('🌍 Merging i18n patches...\n');

locales.forEach(locale => {
  if (mergeLocale(locale)) {
    processed++;
  }
});

console.log('');
console.log(`✨ Processed ${processed} locale(s)`);
console.log('');
console.log('New translation keys added:');
console.log('  - common.select');
console.log('  - supplier.form (complete form translations)');
console.log('  - table (complete table translations)');
