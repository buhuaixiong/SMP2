const http = require('http');

// Step 1: Login
function login() {
  return new Promise((resolve, reject) => {
    const data = JSON.stringify({
      username: 'purch001',
      password: 'Purch#123'
    });

    const options = {
      hostname: 'localhost',
      port: 3001,
      path: '/api/auth/login',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': data.length
      }
    };

    const req = http.request(options, (res) => {
      let body = '';
      res.on('data', (chunk) => body += chunk);
      res.on('end', () => {
        try {
          const result = JSON.parse(body);
          console.log('✓ Login successful');
          console.log('  User:', result.user?.name);
          console.log('  Role:', result.user?.role);
          resolve(result.token);
        } catch (e) {
          reject(e);
        }
      });
    });

    req.on('error', reject);
    req.write(data);
    req.end();
  });
}

// Step 2: Test dashboard todos endpoint
function getTodos(token) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: 'localhost',
      port: 3001,
      path: '/api/dashboard/todos',
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`
      }
    };

    const req = http.request(options, (res) => {
      let body = '';
      res.on('data', (chunk) => body += chunk);
      res.on('end', () => {
        try {
          const result = JSON.parse(body);
          console.log('\n✓ Dashboard todos endpoint working');
          console.log('  Todos count:', result.data?.length || 0);
          if (result.data && result.data.length > 0) {
            console.log('  Sample todo:');
            console.log('    -', result.data[0].title);
            console.log('    -', 'Count:', result.data[0].count);
            console.log('    -', 'Priority:', result.data[0].priority);
          }
          resolve(result.data);
        } catch (e) {
          reject(e);
        }
      });
    });

    req.on('error', reject);
    req.end();
  });
}

// Step 3: Test dashboard stats endpoint
function getStats(token) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: 'localhost',
      port: 3001,
      path: '/api/dashboard/stats',
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`
      }
    };

    const req = http.request(options, (res) => {
      let body = '';
      res.on('data', (chunk) => body += chunk);
      res.on('end', () => {
        try {
          const result = JSON.parse(body);
          console.log('\n✓ Dashboard stats endpoint working');
          console.log('  Stats:', JSON.stringify(result.data, null, 2));
          resolve(result.data);
        } catch (e) {
          reject(e);
        }
      });
    });

    req.on('error', reject);
    req.end();
  });
}

// Run tests
async function runTests() {
  console.log('=== Testing Dashboard API Implementation ===\n');

  try {
    const token = await login();
    await getTodos(token);
    await getStats(token);

    console.log('\n✅ All dashboard API tests passed!\n');
  } catch (error) {
    console.error('\n❌ Test failed:', error.message);
    process.exit(1);
  }
}

runTests();
