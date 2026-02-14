const Database = require('better-sqlite3');
const db = new Database('./apps/api/supplier.db', { readonly: true });

// Get David's user record
const user = db.prepare('SELECT * FROM users WHERE id = ?').get('dept001');

console.log('=== User Object (what backend sees in req.user) ===');
const reqUser = {
  id: user.id,
  name: user.name,
  role: user.role,
  department: user.department,
  supplierId: user.supplierId
};
console.log(JSON.stringify(reqUser, null, 2));

// Simulate getPendingApprovals service call
console.log('\n=== Service Call Parameters ===');
console.log('role:', reqUser.role);
console.log('user.department:', reqUser.department);

let statusFilter;
switch (reqUser.role) {
  case 'procurement_manager':
    statusFilter = 'pending_manager';
    break;
  case 'procurement_director':
    statusFilter = 'pending_director';
    break;
  case 'department_user':
    statusFilter = 'pending_dept_confirm';
    break;
  default:
    statusFilter = null;
}

console.log('statusFilter:', statusFilter);

if (statusFilter === 'pending_dept_confirm') {
  console.log('\n=== Running department_user query ===');
  const stmt = db.prepare(`
    SELECT
      li.*,
      rfqs.title as rfq_title,
      rfqs.created_by as rfq_created_by,
      rfqs.requesting_department,
      quotes.supplier_id,
      quotes.total_amount as quote_amount,
      suppliers.companyName as supplier_name
    FROM rfq_line_items li
    JOIN rfqs ON rfqs.id = li.rfq_id
    LEFT JOIN quotes ON quotes.id = li.selected_quote_id
    LEFT JOIN suppliers ON suppliers.id = quotes.supplier_id
    WHERE li.status = ? AND rfqs.requesting_department = ?
    ORDER BY li.updated_at ASC
  `);

  const results = stmt.all(statusFilter, reqUser.department);

  console.log('\n=== Results ===');
  console.log('Count:', results.length);

  if (results.length > 0) {
    console.log('\nFirst item:');
    console.log({
      id: results[0].id,
      item_name: results[0].item_name,
      status: results[0].status,
      rfq_title: results[0].rfq_title,
      requesting_department: results[0].requesting_department
    });
  } else {
    console.log('\n⚠️  NO RESULTS! Debugging...');
    console.log('\nAll pending_dept_confirm items:');
    const all = db.prepare(`
      SELECT li.id, li.item_name, rfqs.requesting_department
      FROM rfq_line_items li
      JOIN rfqs ON rfqs.id = li.rfq_id
      WHERE li.status = ?
    `).all(statusFilter);
    console.log(all);
  }

  // Create final API response
  const apiResponse = {
    success: true,
    data: results
  };

  console.log('\n=== Final API Response ===');
  console.log(JSON.stringify(apiResponse, null, 2));
}

db.close();
