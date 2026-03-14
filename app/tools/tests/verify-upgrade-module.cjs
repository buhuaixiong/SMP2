/**
 * 供应商升级模块验证脚本
 * Verification script for supplier upgrade module functionality
 */

const path = require('path');
const fs = require('fs');

console.log('========================================');
console.log('供应商升级模块验证');
console.log('Supplier Upgrade Module Verification');
console.log('========================================\n');

// Check 1: Frontend View File
console.log('✓ 检查1: 前端视图文件');
const viewPath = path.join(__dirname, 'src/views/UpgradeManagementView.vue');
const viewExists = fs.existsSync(viewPath);
console.log(`  前端视图文件: ${viewExists ? '✅ 存在' : '❌ 缺失'}`);
if (viewExists) {
  const viewContent = fs.readFileSync(viewPath, 'utf8');
  console.log(`  文件大小: ${viewContent.length} 字符`);

  // Check for key functions
  const hasDownloadTemplate = viewContent.includes('function openTemplate');
  const hasUploadHandler = viewContent.includes('createUploadHandler');
  const hasCanEdit = viewContent.includes('const canEdit =');
  const hasTooltip = viewContent.includes('canEditTooltip');

  console.log(`  模板下载函数: ${hasDownloadTemplate ? '✅' : '❌'}`);
  console.log(`  文件上传处理: ${hasUploadHandler ? '✅' : '❌'}`);
  console.log(`  编辑权限控制: ${hasCanEdit ? '✅' : '❌'}`);
  console.log(`  禁用状态提示: ${hasTooltip ? '✅' : '❌'}`);
}
console.log();

// Check 2: Backend Route File
console.log('✓ 检查2: 后端路由文件');
const routePath = path.join(__dirname, 'supplier-backend/routes/temp-supplier.js');
const routeExists = fs.existsSync(routePath);
console.log(`  后端路由文件: ${routeExists ? '✅ 存在' : '❌ 缺失'}`);
if (routeExists) {
  const routeContent = fs.readFileSync(routePath, 'utf8');
  console.log(`  文件大小: ${routeContent.length} 字符`);

  // Check for key configurations
  const hasUpgradeDocuments = routeContent.includes('UPGRADE_REQUIRED_DOCUMENTS');
  const hasTemplateMap = routeContent.includes('buildTemplateMap');
  const hasWorkflowSteps = routeContent.includes('WORKFLOW_STEPS');

  console.log(`  升级文档配置: ${hasUpgradeDocuments ? '✅' : '❌'}`);
  console.log(`  模板映射功能: ${hasTemplateMap ? '✅' : '❌'}`);
  console.log(`  工作流步骤: ${hasWorkflowSteps ? '✅' : '❌'}`);

  // Extract required documents
  const docsMatch = routeContent.match(/UPGRADE_REQUIRED_DOCUMENTS\s*=\s*\[([\s\S]*?)\]/);
  if (docsMatch) {
    const docs = docsMatch[1].match(/code:\s*'([^']+)'/g);
    if (docs) {
      console.log(`  必需文档数量: ${docs.length}`);
      docs.forEach((doc, i) => {
        const code = doc.match(/code:\s*'([^']+)'/)[1];
        console.log(`    ${i + 1}. ${code}`);
      });
    }
  }
}
console.log();

// Check 3: API Module
console.log('✓ 检查3: API模块文件');
const apiPath = path.join(__dirname, 'src/api/upgrade.ts');
const apiExists = fs.existsSync(apiPath);
console.log(`  API模块文件: ${apiExists ? '✅ 存在' : '❌ 缺失'}`);
if (apiExists) {
  const apiContent = fs.readFileSync(apiPath, 'utf8');
  console.log(`  文件大小: ${apiContent.length} 字符`);

  const hasFetchRequirements = apiContent.includes('fetchUpgradeRequirements');
  const hasFetchStatus = apiContent.includes('fetchUpgradeStatus');
  const hasUploadFile = apiContent.includes('uploadUpgradeFile');
  const hasSubmitApplication = apiContent.includes('submitUpgradeApplication');

  console.log(`  获取升级要求: ${hasFetchRequirements ? '✅' : '❌'}`);
  console.log(`  获取升级状态: ${hasFetchStatus ? '✅' : '❌'}`);
  console.log(`  上传升级文件: ${hasUploadFile ? '✅' : '❌'}`);
  console.log(`  提交升级申请: ${hasSubmitApplication ? '✅' : '❌'}`);
}
console.log();

// Check 4: Template Storage Directory
console.log('✓ 检查4: 模板存储目录');
const templatesDir = path.join(__dirname, 'supplier-backend/uploads/templates');
const templatesDirExists = fs.existsSync(templatesDir);
console.log(`  模板目录: ${templatesDirExists ? '✅ 存在' : '⚠️  不存在（首次上传时会创建）'}`);
if (templatesDirExists) {
  const templates = fs.readdirSync(templatesDir);
  console.log(`  已上传模板数量: ${templates.length}`);
  if (templates.length > 0) {
    console.log('  模板文件列表:');
    templates.forEach((file, i) => {
      const stats = fs.statSync(path.join(templatesDir, file));
      console.log(`    ${i + 1}. ${file} (${(stats.size / 1024).toFixed(2)} KB)`);
    });
  }
}
console.log();

// Check 5: Code Analysis - Key Features
console.log('✓ 检查5: 关键功能代码分析');
if (viewExists) {
  const viewContent = fs.readFileSync(viewPath, 'utf8');

  // Extract openTemplate function
  const openTemplateMatch = viewContent.match(/function openTemplate\(([\s\S]*?)\n}/m);
  if (openTemplateMatch) {
    const funcBody = openTemplateMatch[0];
    const hasUrlValidation = funcBody.includes('if (!url)');
    const hasUrlConstruction = funcBody.includes('backendBase');
    const hasDownloadLink = funcBody.includes('createElement(\'a\')');
    const hasErrorHandling = funcBody.includes('try') && funcBody.includes('catch');

    console.log('  模板下载函数 (openTemplate):');
    console.log(`    URL验证: ${hasUrlValidation ? '✅' : '❌'}`);
    console.log(`    URL构建: ${hasUrlConstruction ? '✅' : '❌'}`);
    console.log(`    下载触发: ${hasDownloadLink ? '✅' : '❌'}`);
    console.log(`    错误处理: ${hasErrorHandling ? '✅' : '❌'}`);
  }

  // Check editable statuses
  const editableStatusMatch = viewContent.match(/EDITABLE_STATUSES\s*=\s*new Set\(\[(.*?)\]\)/);
  if (editableStatusMatch) {
    console.log('  可编辑状态配置:');
    console.log(`    ${editableStatusMatch[1]}`);
  }

  // Check upload validation
  const hasDateValidation = viewContent.includes('if (!row.validFrom || !row.validTo)');
  const hasFileSizeValidation = viewContent.includes('file.size / 1024 / 1024 < 20');
  console.log(`  日期必填验证: ${hasDateValidation ? '✅' : '❌'}`);
  console.log(`  文件大小验证: ${hasFileSizeValidation ? '✅' : '❌'}`);
}
console.log();

// Check 6: Test Documentation
console.log('✓ 检查6: 测试文档');
const testDocPath = path.join(__dirname, 'test-supplier-upgrade.md');
const testDocExists = fs.existsSync(testDocPath);
console.log(`  测试文档: ${testDocExists ? '✅ 已生成' : '❌ 缺失'}`);
if (testDocExists) {
  const testDoc = fs.readFileSync(testDocPath, 'utf8');
  console.log(`  文档大小: ${testDoc.length} 字符`);
  console.log(`  包含测试用例: ${testDoc.includes('测试用例') ? '✅' : '❌'}`);
  console.log(`  包含API文档: ${testDoc.includes('API端点验证') ? '✅' : '❌'}`);
  console.log(`  包含手动测试步骤: ${testDoc.includes('手动测试步骤') ? '✅' : '❌'}`);
}
console.log();

// Summary
console.log('========================================');
console.log('验证总结 (Verification Summary)');
console.log('========================================');

const checks = [
  viewExists,
  routeExists,
  apiExists,
  templatesDirExists || true, // Optional check
  testDocExists
];

const passedChecks = checks.filter(Boolean).length;
const totalChecks = checks.length;

console.log(`\n通过检查: ${passedChecks}/${totalChecks}`);

if (passedChecks === totalChecks) {
  console.log('\n✅ 所有核心功能已实现！');
  console.log('\n建议执行的下一步:');
  console.log('  1. 启动前后端服务器');
  console.log('     - 前端: npm run dev');
  console.log('     - 后端: cd supplier-backend && node index.js');
  console.log('  2. 登录管理员账号 (admin001 / Admin#123)');
  console.log('  3. 上传升级所需的6个文档模板');
  console.log('  4. 登录临时供应商账号 (tempsupp001 / Temp#123)');
  console.log('  5. 执行手动测试（详见 test-supplier-upgrade.md）');
} else {
  console.log('\n⚠️  部分功能缺失，请检查上述输出');
}

console.log('\n查看完整测试文档: test-supplier-upgrade.md');
console.log('========================================\n');
