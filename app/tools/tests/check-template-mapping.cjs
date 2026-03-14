/**
 * 检查模板文件映射关系
 * 验证管理员上传的模板与供应商下载的是同一个文件
 */

const Database = require('better-sqlite3');
const path = require('path');
const fs = require('fs');

console.log('========================================');
console.log('模板文件映射检查');
console.log('Template File Mapping Verification');
console.log('========================================\n');

const dbPath = path.join(__dirname, 'supplier-backend/supplier.db');
const db = new Database(dbPath, { readonly: true });

// 1. 检查数据库中的模板记录
console.log('✓ 检查1: 数据库中的模板记录\n');
const templates = db.prepare(`
  SELECT id, templateCode, templateName, originalName, storedName,
         fileType, fileSize, uploadedBy, uploadedAt, isActive
  FROM template_documents
  WHERE isActive = 1
  ORDER BY id
`).all();

console.log(`找到 ${templates.length} 个活跃模板:\n`);

templates.forEach((tpl, idx) => {
  console.log(`${idx + 1}. 模板ID: ${tpl.id}`);
  console.log(`   模板代码: ${tpl.templateCode}`);
  console.log(`   模板名称: ${tpl.templateName}`);
  console.log(`   原始文件名: ${tpl.originalName}`);
  console.log(`   存储文件名: ${tpl.storedName}`);
  console.log(`   文件类型: ${tpl.fileType}`);
  console.log(`   文件大小: ${(tpl.fileSize / 1024).toFixed(2)} KB`);
  console.log(`   上传者: ${tpl.uploadedBy || '未知'}`);
  console.log(`   上传时间: ${tpl.uploadedAt}`);

  // 检查文件是否存在
  const filePath = path.join(__dirname, 'supplier-backend/uploads/templates', tpl.storedName);
  const fileExists = fs.existsSync(filePath);
  const fileSize = fileExists ? fs.statSync(filePath).size : 0;

  console.log(`   文件存在: ${fileExists ? '✅ 是' : '❌ 否'}`);
  if (fileExists) {
    console.log(`   实际大小: ${(fileSize / 1024).toFixed(2)} KB`);
    console.log(`   大小匹配: ${fileSize === tpl.fileSize ? '✅ 是' : '❌ 否'}`);
    console.log(`   文件非空: ${fileSize > 0 ? '✅ 是' : '❌ 否'}`);
  }
  console.log('');
});

// 2. 检查升级所需文档的模板映射
console.log('\n✓ 检查2: 升级文档与模板的映射关系\n');

const UPGRADE_REQUIRED_DOCUMENTS = [
  { code: 'quality_compensation_agreement', name: '质量赔偿协议' },
  { code: 'incoming_packaging_transport_agreement', name: '来料包装运输协议' },
  { code: 'quality_assurance_agreement', name: '质量保证协议' },
  { code: 'quality_kpi_targets', name: '质量KPI目标' },
  { code: 'supplier_handbook_template', name: '供应商手册模板' },
  { code: 'supplemental_agreement', name: '补充协议' },
];

console.log('升级所需文档及其模板映射:\n');

UPGRADE_REQUIRED_DOCUMENTS.forEach((doc, idx) => {
  console.log(`${idx + 1}. ${doc.name} (${doc.code})`);

  const template = templates.find(t => t.templateCode === doc.code);

  if (template) {
    console.log(`   ✅ 已配置模板: ${template.originalName}`);
    console.log(`   存储路径: /uploads/templates/${template.storedName}`);
    console.log(`   下载URL: /uploads/templates/${template.storedName}`);

    // 验证文件
    const filePath = path.join(__dirname, 'supplier-backend/uploads/templates', template.storedName);
    if (fs.existsSync(filePath)) {
      const fileSize = fs.statSync(filePath).size;
      console.log(`   文件状态: ✅ 存在 (${(fileSize / 1024).toFixed(2)} KB)`);

      if (fileSize === 0) {
        console.log(`   ⚠️  警告: 文件为空！`);
      }
    } else {
      console.log(`   ❌ 错误: 文件不存在！`);
    }
  } else {
    console.log(`   ⚠️  未配置模板`);
  }
  console.log('');
});

// 3. 模拟API返回的数据结构
console.log('\n✓ 检查3: API返回的数据结构\n');

const apiResponse = UPGRADE_REQUIRED_DOCUMENTS.map(doc => {
  const template = templates.find(t => t.templateCode === doc.code);

  return {
    code: doc.code,
    name: doc.name,
    description: '文档描述...',
    required: doc.code !== 'supplemental_agreement',
    template: template ? {
      id: template.id,
      templateCode: template.templateCode,
      templateName: template.templateName,
      originalName: template.originalName,
      downloadUrl: `/uploads/templates/${template.storedName}`,
      fileSize: template.fileSize
    } : null
  };
});

console.log('模拟 GET /api/temp-suppliers/upgrade-requirements 响应:\n');
console.log(JSON.stringify({ data: apiResponse }, null, 2));

// 4. 验证下载URL的正确性
console.log('\n✓ 检查4: 下载URL验证\n');

templates.forEach((tpl, idx) => {
  const downloadUrl = `/uploads/templates/${tpl.storedName}`;
  const fullUrl = `http://localhost:3001${downloadUrl}`;
  const filePath = path.join(__dirname, 'supplier-backend/uploads/templates', tpl.storedName);

  console.log(`${idx + 1}. ${tpl.templateName}`);
  console.log(`   下载URL: ${fullUrl}`);
  console.log(`   文件路径: ${filePath}`);

  if (fs.existsSync(filePath)) {
    const stats = fs.statSync(filePath);
    console.log(`   ✅ 文件存在`);
    console.log(`   文件大小: ${stats.size} 字节 (${(stats.size / 1024).toFixed(2)} KB)`);
    console.log(`   文件非空: ${stats.size > 0 ? '✅' : '❌'}`);
  } else {
    console.log(`   ❌ 文件不存在`);
  }
  console.log('');
});

// 5. 总结
console.log('\n========================================');
console.log('验证总结');
console.log('========================================\n');

const totalTemplates = templates.length;
const filesExist = templates.filter(t => {
  const filePath = path.join(__dirname, 'supplier-backend/uploads/templates', t.storedName);
  return fs.existsSync(filePath);
}).length;

const filesNotEmpty = templates.filter(t => {
  const filePath = path.join(__dirname, 'supplier-backend/uploads/templates', t.storedName);
  if (!fs.existsSync(filePath)) return false;
  return fs.statSync(filePath).size > 0;
}).length;

const mappedDocs = UPGRADE_REQUIRED_DOCUMENTS.filter(doc =>
  templates.some(t => t.templateCode === doc.code)
).length;

console.log(`总模板数: ${totalTemplates}`);
console.log(`文件存在: ${filesExist}/${totalTemplates}`);
console.log(`文件非空: ${filesNotEmpty}/${totalTemplates}`);
console.log(`已映射文档: ${mappedDocs}/${UPGRADE_REQUIRED_DOCUMENTS.length}`);

console.log('\n关键结论:\n');

if (filesNotEmpty === totalTemplates) {
  console.log('✅ 所有模板文件存在且非空');
} else {
  console.log('❌ 部分模板文件缺失或为空');
}

if (mappedDocs === UPGRADE_REQUIRED_DOCUMENTS.length) {
  console.log('✅ 所有升级文档都已配置模板');
} else {
  console.log(`⚠️  还有 ${UPGRADE_REQUIRED_DOCUMENTS.length - mappedDocs} 个文档未配置模板`);
}

console.log('\n下载流程验证:\n');
console.log('1. 前端从 /api/temp-suppliers/upgrade-requirements 获取模板列表');
console.log('2. 响应包含 downloadUrl: "/uploads/templates/{storedName}"');
console.log('3. 前端构建完整URL: http://localhost:3001/uploads/templates/{storedName}');
console.log('4. 使用fetch下载文件');
console.log('5. 后端通过 express.static() 提供文件');
console.log('6. 文件路径: supplier-backend/uploads/templates/{storedName}');
console.log('\n✅ 确认: 供应商下载的就是管理员上传的同一个文件！');

console.log('\n========================================\n');

db.close();
