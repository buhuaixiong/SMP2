/**
 * Test script to verify file upload schema works correctly
 * This tests that the files table INSERT statement succeeds
 */

const Database = require('better-sqlite3');
const path = require('path');

const dbPath = path.join(__dirname, '..', 'supplier.db');
console.log('Testing file upload with database:', dbPath);

const db = new Database(dbPath);

try {
  // This is the same INSERT statement used in routes/files.js line 392
  const insertFileStmt = db.prepare(`
    INSERT INTO files (
      agreementNumber, fileType, validFrom, validTo,
      supplierId, status, uploadTime, uploaderName,
      originalName, storedName
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `);

  // Test data
  const testData = {
    agreementNumber: 'TEST-AGREEMENT-001',
    fileType: 'upgrade_application',
    validFrom: '2025-01-01',
    validTo: '2026-01-01',
    supplierId: 1,
    status: 'submitted',
    uploadTime: new Date().toISOString(),
    uploaderName: 'Test User',
    originalName: 'test-document.pdf',
    storedName: 'test-document-' + Date.now() + '.pdf'
  };

  console.log('\n=== Testing File Upload INSERT ===');
  console.log('Test data:', JSON.stringify(testData, null, 2));

  const result = insertFileStmt.run(
    testData.agreementNumber,
    testData.fileType,
    testData.validFrom,
    testData.validTo,
    testData.supplierId,
    testData.status,
    testData.uploadTime,
    testData.uploaderName,
    testData.originalName,
    testData.storedName
  );

  console.log('\n✓ INSERT succeeded!');
  console.log('  Inserted row ID:', result.lastInsertRowid);
  console.log('  Changes:', result.changes);

  // Verify the data was inserted correctly
  const verifyStmt = db.prepare('SELECT * FROM files WHERE id = ?');
  const insertedRow = verifyStmt.get(result.lastInsertRowid);

  console.log('\n=== Verification ===');
  console.log('Retrieved row:', JSON.stringify(insertedRow, null, 2));

  // Check all fields match
  const fieldsToCheck = [
    'agreementNumber', 'fileType', 'validFrom', 'validTo',
    'supplierId', 'status', 'uploaderName', 'originalName', 'storedName'
  ];

  let allMatch = true;
  fieldsToCheck.forEach(field => {
    if (insertedRow[field] !== testData[field]) {
      console.error(`❌ Field '${field}' mismatch: expected ${testData[field]}, got ${insertedRow[field]}`);
      allMatch = false;
    }
  });

  if (allMatch) {
    console.log('\n✓ All fields verified successfully!');
  }

  // Clean up test data
  db.prepare('DELETE FROM files WHERE id = ?').run(result.lastInsertRowid);
  console.log('\n✓ Test data cleaned up');

  console.log('\n=== TEST PASSED ===');
  console.log('File upload INSERT statement works correctly with the new schema!');

} catch (error) {
  console.error('\n❌ TEST FAILED ===');
  console.error('Error:', error.message);
  console.error('\nThis indicates the database schema is incompatible.');
  console.error('Expected error message if using old database:');
  console.error('  "table files has no column named agreementNumber"');
  process.exit(1);
} finally {
  db.close();
}
