/**
 * Manual test script for Auth Schema Versioning and Auto-Repair
 *
 * This script tests the complete flow of auth schema versioning:
 * 1. Prints current schema version
 * 2. Queries test user
 * 3. Generates JWT token
 * 4. Calls pending approvals API
 * 5. Shows monitoring statistics
 */

const axios = require('axios');
const jwt = require('jsonwebtoken');
const fs = require('fs');

const API_BASE = 'http://localhost:3001';

// Read JWT_SECRET from .env
const envContent = fs.readFileSync('./apps/api/.env', 'utf8');
const secretMatch = envContent.match(/JWT_SECRET=(.+)/);
const JWT_SECRET = secretMatch ? secretMatch[1].trim() : '';

if (!JWT_SECRET) {
  console.error('❌ JWT_SECRET not found in .env file');
  process.exit(1);
}

// Color output helpers
const colors = {
  reset: '\x1b[0m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  red: '\x1b[31m',
  blue: '\x1b[34m',
  cyan: '\x1b[36m',
};

function printStep(emoji, message) {
  console.log(`\n${emoji}  ${colors.cyan}${message}${colors.reset}`);
}

function printSuccess(message) {
  console.log(`${colors.green}✓ ${message}${colors.reset}`);
}

function printError(message) {
  console.log(`${colors.red}✗ ${message}${colors.reset}`);
}

function printWarning(message) {
  console.log(`${colors.yellow}⚠ ${message}${colors.reset}`);
}

function printJSON(obj) {
  console.log(JSON.stringify(obj, null, 2));
}

async function testAuthSchemaRepair() {
  try {
    printStep('🔍', 'Step 1: Checking current auth schema version');

    const healthResponse = await axios.get(`${API_BASE}/api/health/auth-schema`);
    printSuccess('Health check endpoint accessible');
    console.log('Current Schema Version:', healthResponse.data.currentSchemaVersion);
    console.log('Statistics:');
    printJSON(healthResponse.data.stats);

    printStep('👤', 'Step 2: Querying test user (dept001)');

    // We'll use dept001 which should exist
    const userId = 'dept001';
    printSuccess(`Using test user: ${userId}`);

    printStep('🔑', 'Step 3: Generating JWT token');

    const token = jwt.sign(
      { sub: userId },
      JWT_SECRET,
      { expiresIn: '1h' }
    );
    printSuccess('JWT token generated');

    printStep('📡', 'Step 4: Calling /api/rfq/line-items/pending-approvals');

    const apiResponse = await axios.get(
      `${API_BASE}/api/rfq/line-items/pending-approvals`,
      {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      }
    );

    printSuccess(`API call successful (HTTP ${apiResponse.status})`);
    console.log('Response structure:');
    console.log({
      success: apiResponse.data.success,
      dataLength: apiResponse.data.data?.length,
      hasData: !!apiResponse.data.data
    });

    if (apiResponse.data.data && apiResponse.data.data.length > 0) {
      console.log('\nSample pending item:');
      const first = apiResponse.data.data[0];
      printJSON({
        id: first.id,
        item_name: first.item_name,
        status: first.status,
        rfq_title: first.rfq_title,
        requesting_department: first.requesting_department
      });
    } else {
      printWarning('No pending items found (this is okay if none exist)');
    }

    printStep('📊', 'Step 5: Checking updated statistics');

    const finalHealthResponse = await axios.get(`${API_BASE}/api/health/auth-schema`);
    console.log('Final Statistics:');
    printJSON(finalHealthResponse.data.stats);

    if (finalHealthResponse.data.stats.repairCount > 0) {
      printWarning(`Auto-repair was triggered ${finalHealthResponse.data.stats.repairCount} time(s)`);
    } else {
      printSuccess('No auto-repairs needed (all data is up-to-date)');
    }

    console.log(`\n${colors.green}════════════════════════════════════════${colors.reset}`);
    console.log(`${colors.green}✓ All tests completed successfully!${colors.reset}`);
    console.log(`${colors.green}════════════════════════════════════════${colors.reset}\n`);

  } catch (error) {
    printError('Test failed!');
    console.error('\nError details:');
    if (error.response) {
      console.error('HTTP Status:', error.response.status);
      console.error('Response data:');
      printJSON(error.response.data);
    } else {
      console.error(error.message);
    }
    process.exit(1);
  }
}

console.log('\n' + colors.blue + '╔════════════════════════════════════════════╗' + colors.reset);
console.log(colors.blue + '║  Auth Schema Repair Test Script           ║' + colors.reset);
console.log(colors.blue + '╚════════════════════════════════════════════╝' + colors.reset + '\n');

testAuthSchemaRepair();
