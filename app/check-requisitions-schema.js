const Database = require('better-sqlite3');
const db = new Database('./apps/api/supplier.db', { readonly: true });

console.log('=== material_requisitions表结构 ===');
const schema = db.prepare('PRAGMA table_info(material_requisitions)').all();
schema.forEach(col => {
  console.log(`${col.name}: ${col.type}`);
});

console.log('\n=== 检查requesting_department字段 ===');
const hasDept = schema.find(col => col.name === 'requesting_department');
console.log('有requesting_department字段:', !!hasDept);

console.log('\n=== 用户/部门相关字段 ===');
const userFields = schema.filter(col =>
  col.name.includes('user') ||
  col.name.includes('creator') ||
  col.name.includes('person') ||
  col.name.includes('department')
);
userFields.forEach(col => console.log(`  ${col.name}: ${col.type}`));

db.close();
