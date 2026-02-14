const http = require('http');

function login(username, password) {
  return new Promise((resolve, reject) => {
    const data = JSON.stringify({ username, password });

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
          console.log(`✓ Login successful as ${result.user?.name} (${result.user?.role})`);
          resolve({ token: result.token, user: result.user });
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

async function testUser(username, password) {
  console.log(`\n=== Testing ${username} ===`);

  try {
    const { token, user } = await login(username, password);
    const todos = await getTodos(token);

    console.log(`  Todos count: ${todos?.length || 0}`);

    if (todos && todos.length > 0) {
      console.log('  Todo items:');
      todos.forEach(todo => {
        console.log(`    - ${todo.title} (${todo.count}) [${todo.priority}]`);
      });
    } else {
      console.log('  No pending todos');
    }
  } catch (error) {
    console.error(`  ❌ Error:`, error.message);
  }
}

async function runTests() {
  console.log('=== Testing Dashboard Todos for All User Types ===');

  await testUser('tempsupp001', 'Temp#123');      // Temporary Supplier
  await testUser('formsupp001', 'Formal#123');    // Formal Supplier
  await testUser('purch001', 'Purch#123');        // Purchaser
  await testUser('pmgr001', 'ProcMgr#123');       // Procurement Manager
  await testUser('qmgr001', 'Quality#123');       // Quality Manager
  await testUser('pdir001', 'ProcDir#123');       // Procurement Director
  await testUser('acct001', 'Acct#123');          // Finance Accountant
  await testUser('fdir001', 'FinDir#123');        // Finance Director
  await testUser('admin001', 'Admin#123');        // Admin

  console.log('\n✅ All user tests completed!\n');
}

runTests();
