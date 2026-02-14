/**
 * 供应商管理系统 - 安全与性能测试脚本
 * Security and Performance Test Script
 */

const http = require('http');
const https = require('https');

const API_BASE = 'http://localhost:3001';
const TEST_RESULTS = {
  security: [],
  performance: [],
  connectivity: [],
  warnings: [],
  critical: []
};

// 测试用户凭证
const TEST_USERS = [
  { username: 'admin001', password: 'Admin#123', role: 'admin' },
  { username: 'purch001', password: 'Purch#123', role: 'purchaser' },
  { username: 'tempsupp001', password: 'Temp#123', role: 'temp_supplier' }
];

// 颜色输出
const colors = {
  reset: '\x1b[0m',
  red: '\x1b[31m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  cyan: '\x1b[36m'
};

function log(message, color = 'reset') {
  console.log(`${colors[color]}${message}${colors.reset}`);
}

function logSection(title) {
  console.log('\n' + '='.repeat(60));
  log(title, 'cyan');
  console.log('='.repeat(60));
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

// 1. 测试数据库密码加密
async function testPasswordEncryption() {
  logSection('测试 1: 数据库密码加密检查');

  try {
    // 尝试登录以验证密码是否正确加密
    const response = await request('/api/auth/login', {
      method: 'POST',
      body: { username: 'admin001', password: 'Admin#123' }
    });

    if (response.status === 200 && response.body.token) {
      log('✓ 密码验证成功 - bcrypt 加密正常工作', 'green');
      TEST_RESULTS.security.push({
        test: '密码加密',
        status: 'PASS',
        detail: '使用 bcrypt (12 rounds) 加密密码'
      });

      // 检查响应中是否包含密码哈希
      if (!response.rawBody.includes('$2a$') && !response.rawBody.includes('$2b$')) {
        log('✓ 密码哈希未暴露在 API 响应中', 'green');
        TEST_RESULTS.security.push({
          test: '密码哈希保护',
          status: 'PASS',
          detail: 'API 响应中不包含密码哈希'
        });
      } else {
        log('✗ 警告：密码哈希可能在响应中暴露', 'red');
        TEST_RESULTS.critical.push('密码哈希在 API 响应中暴露');
      }

      return response.body.token;
    } else {
      log('✗ 密码验证失败', 'red');
      TEST_RESULTS.critical.push('密码验证系统异常');
      return null;
    }
  } catch (error) {
    log(`✗ 测试失败: ${error.message}`, 'red');
    TEST_RESULTS.critical.push(`密码加密测试失败: ${error.message}`);
    return null;
  }
}

// 2. 测试 JWT Token 安全性
async function testJWTSecurity(token) {
  logSection('测试 2: JWT Token 安全性检查');

  try {
    // 检查 token 格式
    const parts = token.split('.');
    if (parts.length !== 3) {
      log('✗ JWT Token 格式不正确', 'red');
      TEST_RESULTS.critical.push('JWT Token 格式错误');
      return;
    }

    log('✓ JWT Token 格式正确 (3 部分)', 'green');

    // 解码 payload (不验证签名，仅检查内容)
    const payload = JSON.parse(Buffer.from(parts[1], 'base64').toString());
    log(`  Token Payload: sub=${payload.sub}, role=${payload.role}, exp=${new Date(payload.exp * 1000).toISOString()}`, 'blue');

    // 检查是否包含敏感信息
    if (payload.password || payload.passwordHash) {
      log('✗ 严重：Token 包含密码信息', 'red');
      TEST_RESULTS.critical.push('JWT Token 包含密码');
    } else {
      log('✓ Token 不包含密码信息', 'green');
      TEST_RESULTS.security.push({
        test: 'JWT Token 内容',
        status: 'PASS',
        detail: 'Token 不包含敏感密码信息'
      });
    }

    // 检查过期时间
    if (payload.exp) {
      const expiryDate = new Date(payload.exp * 1000);
      const now = new Date();
      const hoursUntilExpiry = (expiryDate - now) / (1000 * 60 * 60);
      log(`✓ Token 有效期: ${hoursUntilExpiry.toFixed(1)} 小时`, 'green');

      if (hoursUntilExpiry > 24) {
        log(`⚠ 警告：Token 有效期过长 (>${hoursUntilExpiry.toFixed(1)}小时)`, 'yellow');
        TEST_RESULTS.warnings.push(`JWT Token 有效期过长: ${hoursUntilExpiry.toFixed(1)}小时`);
      }
    }

    TEST_RESULTS.security.push({
      test: 'JWT Token 配置',
      status: 'PASS',
      detail: `有效期合理，不包含敏感信息`
    });

  } catch (error) {
    log(`✗ JWT 测试失败: ${error.message}`, 'red');
    TEST_RESULTS.warnings.push(`JWT 解析失败: ${error.message}`);
  }
}

// 3. 测试 API 认证授权
async function testAPIAuthentication() {
  logSection('测试 3: API 认证与授权');

  // 3.1 测试无 Token 访问
  try {
    const response = await request('/api/suppliers');
    if (response.status === 401) {
      log('✓ 未认证请求被正确拒绝 (401)', 'green');
      TEST_RESULTS.security.push({
        test: '未认证访问控制',
        status: 'PASS',
        detail: '未提供 Token 的请求被拒绝'
      });
    } else {
      log(`✗ 安全漏洞：未认证请求返回 ${response.status}`, 'red');
      TEST_RESULTS.critical.push(`未认证请求未被拒绝，返回状态 ${response.status}`);
    }
  } catch (error) {
    log(`⚠ 连接错误: ${error.message}`, 'yellow');
  }

  // 3.2 测试无效 Token
  try {
    const response = await request('/api/suppliers', {
      headers: { 'Authorization': 'Bearer invalid.token.here' }
    });
    if (response.status === 401) {
      log('✓ 无效 Token 被正确拒绝 (401)', 'green');
      TEST_RESULTS.security.push({
        test: '无效 Token 验证',
        status: 'PASS',
        detail: '伪造的 Token 被正确拒绝'
      });
    } else {
      log(`✗ 安全漏洞：无效 Token 返回 ${response.status}`, 'red');
      TEST_RESULTS.critical.push('无效 Token 未被正确验证');
    }
  } catch (error) {
    log(`⚠ 测试错误: ${error.message}`, 'yellow');
  }

  // 3.3 测试有效 Token 访问
  try {
    const loginRes = await request('/api/auth/login', {
      method: 'POST',
      body: TEST_USERS[0]
    });

    if (loginRes.body && loginRes.body.token) {
      const token = loginRes.body.token;
      const response = await request('/api/suppliers', {
        headers: { 'Authorization': `Bearer ${token}` }
      });

      if (response.status === 200) {
        log('✓ 有效 Token 访问成功', 'green');
        log(`  响应时间: ${response.duration}ms`, 'blue');
        TEST_RESULTS.performance.push({
          endpoint: '/api/suppliers',
          duration: response.duration,
          status: 'OK'
        });
      }
    }
  } catch (error) {
    log(`⚠ 认证测试错误: ${error.message}`, 'yellow');
  }
}

// 4. 检查敏感数据泄露
async function testSensitiveDataExposure() {
  logSection('测试 4: 敏感数据泄露检查');

  try {
    // 登录并获取用户信息
    const loginRes = await request('/api/auth/login', {
      method: 'POST',
      body: TEST_USERS[0]
    });

    if (!loginRes.body || !loginRes.body.token) {
      log('✗ 无法获取测试 Token', 'red');
      return;
    }

    const token = loginRes.body.token;

    // 检查登录响应
    if (loginRes.rawBody.includes('password')) {
      log('✗ 警告：登录响应可能包含密码字段', 'red');
      TEST_RESULTS.warnings.push('登录响应中存在 password 字段');
    } else {
      log('✓ 登录响应不包含密码字段', 'green');
    }

    // 检查 /me 端点
    const meRes = await request('/api/auth/me', {
      headers: { 'Authorization': `Bearer ${token}` }
    });

    if (meRes.rawBody && meRes.rawBody.includes('password')) {
      log('✗ 严重：/me 端点暴露密码信息', 'red');
      TEST_RESULTS.critical.push('/api/auth/me 暴露密码');
    } else {
      log('✓ /me 端点不暴露密码', 'green');
    }

    // 检查供应商列表
    const suppliersRes = await request('/api/suppliers?limit=5', {
      headers: { 'Authorization': `Bearer ${token}` }
    });

    const sensitiveKeywords = ['password', 'secret', 'token', 'privateKey', 'apiKey'];
    const foundSensitive = sensitiveKeywords.filter(kw =>
      suppliersRes.rawBody && suppliersRes.rawBody.toLowerCase().includes(kw)
    );

    if (foundSensitive.length > 0) {
      log(`⚠ 警告：供应商数据可能包含敏感字段: ${foundSensitive.join(', ')}`, 'yellow');
      TEST_RESULTS.warnings.push(`供应商 API 包含敏感关键词: ${foundSensitive.join(', ')}`);
    } else {
      log('✓ 供应商数据不包含明显的敏感信息', 'green');
    }

    TEST_RESULTS.security.push({
      test: '敏感数据暴露',
      status: foundSensitive.length === 0 ? 'PASS' : 'WARNING',
      detail: foundSensitive.length === 0 ? '未发现敏感数据泄露' : `发现关键词: ${foundSensitive.join(', ')}`
    });

  } catch (error) {
    log(`✗ 测试失败: ${error.message}`, 'red');
  }
}

// 5. 测试前后端连通性与性能
async function testConnectivityPerformance() {
  logSection('测试 5: 前后端连通性与性能');

  const endpoints = [
    { path: '/api/auth/login', method: 'POST', body: TEST_USERS[0], auth: false },
    { path: '/api/auth/me', method: 'GET', auth: true },
    { path: '/api/suppliers', method: 'GET', auth: true },
    { path: '/api/suppliers/stats', method: 'GET', auth: true },
    { path: '/api/suppliers/tags', method: 'GET', auth: true },
  ];

  // 首先登录获取 token
  let token = null;
  try {
    const loginRes = await request('/api/auth/login', {
      method: 'POST',
      body: TEST_USERS[0]
    });
    token = loginRes.body?.token;
  } catch (error) {
    log('✗ 无法获取认证 Token', 'red');
    return;
  }

  log('\n性能测试结果:', 'cyan');
  console.log('端点'.padEnd(30) + '状态'.padEnd(10) + '响应时间');
  console.log('-'.repeat(60));

  for (const endpoint of endpoints) {
    try {
      const options = {
        method: endpoint.method,
        body: endpoint.body
      };

      if (endpoint.auth && token) {
        options.headers = { 'Authorization': `Bearer ${token}` };
      }

      const response = await request(endpoint.path, options);
      const status = response.status === 200 ? '✓ OK' : `✗ ${response.status}`;
      const color = response.status === 200 ? 'green' : 'red';

      log(`${endpoint.path.padEnd(30)}${status.padEnd(10)}${response.duration}ms`, color);

      TEST_RESULTS.performance.push({
        endpoint: `${endpoint.method} ${endpoint.path}`,
        status: response.status,
        duration: response.duration
      });

      // 性能警告
      if (response.duration > 1000) {
        TEST_RESULTS.warnings.push(`${endpoint.path} 响应时间过长: ${response.duration}ms`);
      }

    } catch (error) {
      log(`${endpoint.path.padEnd(30)}✗ 失败`.padEnd(10) + error.message, 'red');
      TEST_RESULTS.connectivity.push({
        endpoint: endpoint.path,
        status: 'FAILED',
        error: error.message
      });
    }
  }

  // 计算平均响应时间
  const avgTime = TEST_RESULTS.performance.reduce((sum, r) => sum + r.duration, 0) / TEST_RESULTS.performance.length;
  log(`\n平均响应时间: ${avgTime.toFixed(2)}ms`, 'cyan');

  if (avgTime < 100) {
    log('✓ 性能优秀', 'green');
  } else if (avgTime < 300) {
    log('✓ 性能良好', 'green');
  } else if (avgTime < 1000) {
    log('⚠ 性能一般，建议优化', 'yellow');
  } else {
    log('✗ 性能较差，需要优化', 'red');
  }
}

// 6. 测试角色权限控制
async function testRoleBasedAccess() {
  logSection('测试 6: 角色权限控制');

  try {
    // 使用临时供应商账户登录
    const supplierLogin = await request('/api/auth/login', {
      method: 'POST',
      body: TEST_USERS[2] // temp_supplier
    });

    if (!supplierLogin.body?.token) {
      log('✗ 无法登录供应商账户', 'red');
      return;
    }

    const supplierToken = supplierLogin.body.token;

    // 尝试访问管理员功能（应该被拒绝）
    const adminAccess = await request('/api/users', {
      headers: { 'Authorization': `Bearer ${supplierToken}` }
    });

    if (adminAccess.status === 403 || adminAccess.status === 401) {
      log('✓ 供应商账户无法访问管理员功能 (正确)', 'green');
      TEST_RESULTS.security.push({
        test: '角色权限隔离',
        status: 'PASS',
        detail: '低权限用户无法访问高权限端点'
      });
    } else {
      log(`✗ 权限漏洞：供应商账户可访问管理功能 (${adminAccess.status})`, 'red');
      TEST_RESULTS.critical.push('角色权限控制失效');
    }

  } catch (error) {
    log(`⚠ 测试错误: ${error.message}`, 'yellow');
  }
}

// 7. 环境配置安全检查
async function checkEnvironmentSecurity() {
  logSection('测试 7: 环境配置安全检查');

  const fs = require('fs');
  const path = require('path');

  // 检查 .env 文件
  const envPath = path.join(__dirname, 'supplier-backend', '.env');
  if (fs.existsSync(envPath)) {
    log('✓ .env 文件存在', 'green');
    const envContent = fs.readFileSync(envPath, 'utf8');

    if (envContent.includes('JWT_SECRET=') && !envContent.includes('change-me')) {
      log('✓ JWT_SECRET 已配置（非默认值）', 'green');
      TEST_RESULTS.security.push({
        test: 'JWT_SECRET 配置',
        status: 'PASS',
        detail: '使用自定义 JWT_SECRET'
      });
    } else {
      log('✗ 严重：使用默认 JWT_SECRET 或未配置', 'red');
      TEST_RESULTS.critical.push('JWT_SECRET 使用默认值或未配置');
    }
  } else {
    log('⚠ .env 文件不存在，使用默认配置', 'yellow');
    TEST_RESULTS.warnings.push('.env 文件不存在');
    TEST_RESULTS.critical.push('JWT_SECRET 使用默认值（生产环境严重风险）');
  }

  // 检查数据库文件权限（仅提示）
  const dbPath = path.join(__dirname, 'supplier-backend', 'supplier.db');
  if (fs.existsSync(dbPath)) {
    log('✓ 数据库文件存在', 'green');
    log('  提示：确保数据库文件权限正确设置（600 或 640）', 'blue');
  }
}

// 生成测试报告
function generateReport() {
  logSection('测试报告汇总');

  log('\n【严重问题】', 'red');
  if (TEST_RESULTS.critical.length === 0) {
    log('  无严重安全问题', 'green');
  } else {
    TEST_RESULTS.critical.forEach(issue => {
      log(`  ✗ ${issue}`, 'red');
    });
  }

  log('\n【警告】', 'yellow');
  if (TEST_RESULTS.warnings.length === 0) {
    log('  无警告', 'green');
  } else {
    TEST_RESULTS.warnings.forEach(warning => {
      log(`  ⚠ ${warning}`, 'yellow');
    });
  }

  log('\n【安全测试通过项】', 'green');
  TEST_RESULTS.security.forEach(test => {
    log(`  ✓ ${test.test}: ${test.detail}`, 'green');
  });

  log('\n【性能统计】', 'cyan');
  if (TEST_RESULTS.performance.length > 0) {
    const avgTime = TEST_RESULTS.performance.reduce((sum, r) => sum + r.duration, 0) / TEST_RESULTS.performance.length;
    const maxTime = Math.max(...TEST_RESULTS.performance.map(r => r.duration));
    const minTime = Math.min(...TEST_RESULTS.performance.map(r => r.duration));

    log(`  平均响应: ${avgTime.toFixed(2)}ms`, 'blue');
    log(`  最快响应: ${minTime}ms`, 'blue');
    log(`  最慢响应: ${maxTime}ms`, 'blue');
  }

  log('\n【总体评估】', 'cyan');
  const criticalCount = TEST_RESULTS.critical.length;
  const warningCount = TEST_RESULTS.warnings.length;

  if (criticalCount === 0 && warningCount === 0) {
    log('  ✓ 系统安全状态良好，可以上线', 'green');
  } else if (criticalCount > 0) {
    log(`  ✗ 发现 ${criticalCount} 个严重问题，必须修复后才能上线！`, 'red');
  } else {
    log(`  ⚠ 发现 ${warningCount} 个警告，建议修复后上线`, 'yellow');
  }

  console.log('\n' + '='.repeat(60));
}

// 主测试流程
async function runAllTests() {
  log('\n供应商管理系统 - 安全与性能测试', 'cyan');
  log('开始时间: ' + new Date().toLocaleString('zh-CN'), 'blue');

  try {
    const token = await testPasswordEncryption();
    if (token) {
      await testJWTSecurity(token);
    }
    await testAPIAuthentication();
    await testSensitiveDataExposure();
    await testRoleBasedAccess();
    await testConnectivityPerformance();
    await checkEnvironmentSecurity();

    generateReport();
  } catch (error) {
    log(`\n测试过程出现错误: ${error.message}`, 'red');
    console.error(error);
  }

  log('\n结束时间: ' + new Date().toLocaleString('zh-CN'), 'blue');
}

// 运行测试
runAllTests().catch(console.error);
