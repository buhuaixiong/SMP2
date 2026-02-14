/**
 * I18N 加载验证脚本
 * 用于验证翻译文件是否能被正确识别和加载
 */

import { readdir, readFile } from 'fs/promises';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const localesPath = join(__dirname, 'src', 'locales');

console.log('🔍 验证 I18N 翻译文件加载情况\n');
console.log('📁 翻译文件目录:', localesPath);
console.log('─'.repeat(60));

async function validateLocales() {
  const languages = ['en', 'zh', 'th'];
  const results = {
    success: [],
    errors: []
  };

  for (const lang of languages) {
    const langPath = join(localesPath, lang);

    try {
      // 检查语言目录是否存在
      const files = await readdir(langPath);
      console.log(`\n✓ ${lang.toUpperCase()} 目录存在 (${files.length} 个文件)`);

      // 检查 rfq.json
      const rfqFile = files.find(f => f === 'rfq.json');
      if (!rfqFile) {
        results.errors.push(`${lang}/rfq.json 不存在`);
        console.log(`  ✗ rfq.json 不存在`);
        continue;
      }

      // 读取并解析 rfq.json
      const rfqPath = join(langPath, 'rfq.json');
      const content = await readFile(rfqPath, 'utf-8');
      const json = JSON.parse(content);

      // 验证 management 节点
      if (!json.management) {
        results.errors.push(`${lang}/rfq.json 缺少 'management' 节点`);
        console.log(`  ✗ 缺少 'management' 节点`);
        continue;
      }

      // 验证必要的翻译key
      const requiredKeys = [
        'title',
        'createRfq',
        'loadError',
        'supplierInvitations',
        'pendingRequisitions'
      ];

      const missingKeys = requiredKeys.filter(key => !json.management[key]);

      if (missingKeys.length > 0) {
        results.errors.push(`${lang}/rfq.json management 缺少keys: ${missingKeys.join(', ')}`);
        console.log(`  ✗ management 缺少keys: ${missingKeys.join(', ')}`);
      } else {
        results.success.push(lang);
        console.log(`  ✓ rfq.json 存在且完整`);
        console.log(`  ✓ management.title = "${json.management.title}"`);
        console.log(`  ✓ management.createRfq = "${json.management.createRfq}"`);

        // 检查子节点
        if (json.management.supplierInvitations) {
          console.log(`  ✓ management.supplierInvitations 完整`);
        }
        if (json.management.pendingRequisitions) {
          console.log(`  ✓ management.pendingRequisitions 完整`);
        }
      }

    } catch (error) {
      results.errors.push(`${lang}: ${error.message}`);
      console.log(`\n✗ ${lang.toUpperCase()} 验证失败:`, error.message);
    }
  }

  // 总结
  console.log('\n' + '='.repeat(60));
  console.log('📊 验证结果总结\n');
  console.log(`✓ 成功: ${results.success.length} 个语言 (${results.success.join(', ')})`);
  console.log(`✗ 错误: ${results.errors.length} 个`);

  if (results.errors.length > 0) {
    console.log('\n错误详情:');
    results.errors.forEach(err => console.log(`  - ${err}`));
    process.exit(1);
  } else {
    console.log('\n✅ 所有翻译文件验证通过！');
    console.log('\n💡 如果页面仍然显示英文，请检查:');
    console.log('  1. 浏览器 localStorage 中的 "supplier-system.locale" 值');
    console.log('  2. 浏览器控制台是否有报错');
    console.log('  3. 清除浏览器缓存后重试');
    console.log('  4. 确保开发服务器已重启');
  }
}

validateLocales().catch(console.error);
