// Script to check which routes exist in router
const fs = require('fs');
const path = require('path');

const routerFile = fs.readFileSync(
  path.join(__dirname, 'src', 'router', 'index.ts'),
  'utf-8'
);

// Extract all route paths
const pathMatches = routerFile.match(/path:\s*["']([^"']+)["']/g) || [];
const paths = pathMatches.map(m => m.match(/["']([^"']+)["']/)[1]);

console.log('=== All Routes in router/index.ts ===\n');
paths.forEach(p => console.log(`  ${p}`));

// Check Sidebar paths
const sidebarPaths = [
  // Suppliers
  '/supplier-profile',
  '/supplier-documents',
  '/supplier-file-uploads',
  '/supplier-upgrade',
  '/contracts',

  // Purchaser
  '/rfq-projects',
  '/file-upload-approval',
  '/file-expiry-management',
  '/ratings',
  '/change-approvals',

  // Other
  '/quality-approvals',
  '/director-approvals',
  '/finance-approvals',
  '/settlements'
];

console.log('\n=== Sidebar Paths Not Found in Router ===\n');
sidebarPaths.forEach(sp => {
  const exists = paths.some(p => {
    // Exact match or starts with (for dynamic routes)
    return p === sp || sp.startsWith(p.replace(/:\w+/g, ''));
  });

  if (!exists) {
    console.log(`  ❌ ${sp}`);
  }
});
