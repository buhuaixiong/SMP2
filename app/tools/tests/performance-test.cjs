/**
 * 供应商管理系统 - 性能测试脚本
 * 测试目标：
 * - 页面加载时间 ≤ 3秒
 * - 接口响应时间 ≤ 500毫秒
 * - 并发用户承载能力测试
 */

const http = require('http');
const https = require('https');

const BASE_URL = 'http://localhost:3001';
const COLORS = {
  reset: '\x1b[0m',
  red: '\x1b[31m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  cyan: '\x1b[36m',
  gray: '\x1b[90m'
};

// 性能测试结果收集
const results = {
  tests: [],
  passed: 0,
  failed: 0,
  totalTime: 0
};

// HTTP 请求封装
function request(options, data = null) {
  return new Promise((resolve, reject) => {
    const startTime = Date.now();
    const protocol = options.protocol === 'https:' ? https : http;

    const req = protocol.request(options, (res) => {
      let body = '';
      res.on('data', chunk => body += chunk);
      res.on('end', () => {
        const duration = Date.now() - startTime;
        resolve({
          statusCode: res.statusCode,
          headers: res.headers,
          body: body,
          duration: duration
        });
      });
    });

    req.on('error', (error) => {
      const duration = Date.now() - startTime;
      reject({ error, duration });
    });

    if (data) {
      req.write(typeof data === 'string' ? data : JSON.stringify(data));
    }

    req.end();
  });
}

// 记录测试结果
function logResult(testName, duration, threshold, passed, details = '') {
  const status = passed ?
    `${COLORS.green}✓ PASS${COLORS.reset}` :
    `${COLORS.red}✗ FAIL${COLORS.reset}`;

  const timeColor = duration < threshold ? COLORS.green :
                    duration < threshold * 1.5 ? COLORS.yellow : COLORS.red;

  console.log(`${status} ${testName}`);
  console.log(`  ${COLORS.gray}→ 响应时间: ${timeColor}${duration}ms${COLORS.reset} (阈值: ${threshold}ms)`);

  if (details) {
    console.log(`  ${COLORS.gray}→ ${details}${COLORS.reset}`);
  }

  results.tests.push({ testName, duration, threshold, passed, details });
  if (passed) results.passed++;
  else results.failed++;
  results.totalTime += duration;
}

// ==================== 测试用例 ====================

// 1. 登录接口测试
async function testLogin() {
  console.log(`\n${COLORS.cyan}【1/8】测试登录接口${COLORS.reset}`);

  const options = {
    hostname: 'localhost',
    port: 3001,
    path: '/api/auth/login',
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    }
  };

  const loginData = {
    username: 'admin001',
    password: 'Admin#123'
  };

  try {
    const res = await request(options, loginData);
    const passed = res.statusCode === 200 && res.duration < 500;

    let token = null;
    try {
      const parsed = JSON.parse(res.body);
      token = parsed.token;
    } catch (e) {}

    logResult(
      '登录接口 (POST /api/auth/login)',
      res.duration,
      500,
      passed,
      `状态码: ${res.statusCode}, Token: ${token ? '✓ 已获取' : '✗ 未获取'}`
    );

    return token;
  } catch (error) {
    logResult('登录接口 (POST /api/auth/login)', error.duration || 0, 500, false, `错误: ${error.error?.message}`);
    return null;
  }
}

// 2. 获取当前用户信息
async function testGetMe(token) {
  console.log(`\n${COLORS.cyan}【2/8】测试获取用户信息接口${COLORS.reset}`);

  const options = {
    hostname: 'localhost',
    port: 3001,
    path: '/api/auth/me',
    method: 'GET',
    headers: {
      'Authorization': `Bearer ${token}`
    }
  };

  try {
    const res = await request(options);
    const passed = res.statusCode === 200 && res.duration < 300;

    logResult(
      '获取用户信息 (GET /api/auth/me)',
      res.duration,
      300,
      passed,
      `状态码: ${res.statusCode}`
    );
  } catch (error) {
    logResult('获取用户信息 (GET /api/auth/me)', error.duration || 0, 300, false, `错误: ${error.error?.message}`);
  }
}

// 3. 获取供应商列表
async function testGetSuppliers(token) {
  console.log(`\n${COLORS.cyan}【3/8】测试获取供应商列表接口${COLORS.reset}`);

  const options = {
    hostname: 'localhost',
    port: 3001,
    path: '/api/suppliers?page=1&limit=20',
    method: 'GET',
    headers: {
      'Authorization': `Bearer ${token}`
    }
  };

  try {
    const res = await request(options);
    const passed = res.statusCode === 200 && res.duration < 500;

    let count = 0;
    try {
      const parsed = JSON.parse(res.body);
      count = parsed.data?.length || 0;
    } catch (e) {}

    logResult(
      '获取供应商列表 (GET /api/suppliers)',
      res.duration,
      500,
      passed,
      `状态码: ${res.statusCode}, 返回记录数: ${count}`
    );
  } catch (error) {
    logResult('获取供应商列表', error.duration || 0, 500, false, `错误: ${error.error?.message}`);
  }
}

// 4. 创建供应商
async function testCreateSupplier(token) {
  console.log(`\n${COLORS.cyan}【4/8】测试创建供应商接口${COLORS.reset}`);

  const supplierData = {
    companyName: `性能测试供应商_${Date.now()}`,
    companyId: `PERF_TEST_${Date.now()}`,           // ✅ 添加必填字段
    contactPerson: '张三',
    contactPhone: '13800138000',
    contactEmail: `perftest${Date.now()}@example.com`,
    category: 'electronics',
    address: '北京市朝阳区测试大街123号',            // ✅ 添加必填字段
    stage: 'temporary'
  };

  const options = {
    hostname: 'localhost',
    port: 3001,
    path: '/api/suppliers',
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    }
  };

  try {
    const res = await request(options, supplierData);
    const passed = res.statusCode === 200 && res.duration < 500;

    let supplierId = null;
    try {
      const parsed = JSON.parse(res.body);
      supplierId = parsed.id;
    } catch (e) {}

    logResult(
      '创建供应商 (POST /api/suppliers)',
      res.duration,
      500,
      passed,
      `状态码: ${res.statusCode}, ID: ${supplierId || 'N/A'}`
    );

    return supplierId;
  } catch (error) {
    logResult('创建供应商', error.duration || 0, 500, false, `错误: ${error.error?.message}`);
    return null;
  }
}

// 5. 获取单个供应商详情
async function testGetSupplierDetail(token, supplierId) {
  console.log(`\n${COLORS.cyan}【5/8】测试获取供应商详情接口${COLORS.reset}`);

  if (!supplierId) {
    console.log(`  ${COLORS.yellow}⚠ 跳过：未获取到供应商ID${COLORS.reset}`);
    return;
  }

  const options = {
    hostname: 'localhost',
    port: 3001,
    path: `/api/suppliers/${supplierId}`,
    method: 'GET',
    headers: {
      'Authorization': `Bearer ${token}`
    }
  };

  try {
    const res = await request(options);
    const passed = res.statusCode === 200 && res.duration < 400;

    logResult(
      '获取供应商详情 (GET /api/suppliers/:id)',
      res.duration,
      400,
      passed,
      `状态码: ${res.statusCode}`
    );
  } catch (error) {
    logResult('获取供应商详情', error.duration || 0, 400, false, `错误: ${error.error?.message}`);
  }
}

// 6. 搜索供应商
async function testSearchSuppliers(token) {
  console.log(`\n${COLORS.cyan}【6/8】测试搜索供应商接口${COLORS.reset}`);

  const options = {
    hostname: 'localhost',
    port: 3001,
    path: '/api/suppliers?stage=temporary&page=1&limit=10',  // ✅ 修正参数名称
    method: 'GET',
    headers: {
      'Authorization': `Bearer ${token}`
    }
  };

  try {
    const res = await request(options);
    const passed = res.statusCode === 200 && res.duration < 600;

    logResult(
      '搜索供应商 (GET /api/suppliers with filters)',
      res.duration,
      600,
      passed,
      `状态码: ${res.statusCode}`
    );
  } catch (error) {
    logResult('搜索供应商', error.duration || 0, 600, false, `错误: ${error.error?.message}`);
  }
}

// 7. 获取统计数据
async function testGetStatistics(token) {
  console.log(`\n${COLORS.cyan}【7/8】测试获取统计数据接口${COLORS.reset}`);

  const options = {
    hostname: 'localhost',
    port: 3001,
    path: '/api/suppliers/stats',  // ✅ 修正API路径
    method: 'GET',
    headers: {
      'Authorization': `Bearer ${token}`
    }
  };

  try {
    const res = await request(options);
    const passed = res.statusCode === 200 && res.duration < 800;

    logResult(
      '获取统计数据 (GET /api/suppliers/stats)',
      res.duration,
      800,
      passed,
      `状态码: ${res.statusCode}`
    );
  } catch (error) {
    logResult('获取统计数据', error.duration || 0, 800, false, `错误: ${error.error?.message}`);
  }
}

// 8. 并发测试
async function testConcurrentRequests(token) {
  console.log(`\n${COLORS.cyan}【8/8】测试并发请求能力 (10个并发)${COLORS.reset}`);

  const concurrentCount = 10;
  const requests = [];

  const options = {
    hostname: 'localhost',
    port: 3001,
    path: '/api/suppliers?page=1&limit=10',
    method: 'GET',
    headers: {
      'Authorization': `Bearer ${token}`
    }
  };

  const startTime = Date.now();

  for (let i = 0; i < concurrentCount; i++) {
    requests.push(request(options).catch(err => ({ error: true, ...err })));
  }

  try {
    const responses = await Promise.all(requests);
    const totalDuration = Date.now() - startTime;
    const avgDuration = totalDuration / concurrentCount;
    const successCount = responses.filter(r => !r.error && r.statusCode === 200).length;
    const passed = successCount === concurrentCount && avgDuration < 1000;

    logResult(
      `并发测试 (${concurrentCount}个并发请求)`,
      totalDuration,
      concurrentCount * 500,
      passed,
      `成功: ${successCount}/${concurrentCount}, 平均响应: ${avgDuration.toFixed(0)}ms`
    );
  } catch (error) {
    logResult('并发测试', 0, concurrentCount * 500, false, `错误: ${error.message}`);
  }
}

// ==================== 生成测试报告 ====================

function generateReport() {
  console.log(`\n${'='.repeat(60)}`);
  console.log(`${COLORS.blue}性能测试报告${COLORS.reset}`);
  console.log(`${'='.repeat(60)}`);

  const passRate = ((results.passed / results.tests.length) * 100).toFixed(1);
  const avgTime = (results.totalTime / results.tests.length).toFixed(0);

  console.log(`\n📊 测试概览:`);
  console.log(`  总测试数: ${results.tests.length}`);
  console.log(`  ${COLORS.green}通过: ${results.passed}${COLORS.reset}`);
  console.log(`  ${results.failed > 0 ? COLORS.red : COLORS.green}失败: ${results.failed}${COLORS.reset}`);
  console.log(`  通过率: ${passRate >= 80 ? COLORS.green : COLORS.red}${passRate}%${COLORS.reset}`);
  console.log(`  平均响应时间: ${avgTime}ms`);
  console.log(`  总耗时: ${results.totalTime}ms`);

  // 性能等级评估
  console.log(`\n⭐ 性能评级:`);
  let rating = 'A+';
  let ratingColor = COLORS.green;

  if (passRate < 60 || avgTime > 800) {
    rating = 'D';
    ratingColor = COLORS.red;
  } else if (passRate < 75 || avgTime > 600) {
    rating = 'C';
    ratingColor = COLORS.yellow;
  } else if (passRate < 90 || avgTime > 400) {
    rating = 'B';
    ratingColor = COLORS.green;
  } else if (passRate < 100 || avgTime > 300) {
    rating = 'A';
    ratingColor = COLORS.green;
  }

  console.log(`  ${ratingColor}${rating}${COLORS.reset}`);

  // 详细结果
  console.log(`\n📋 详细结果:`);
  results.tests.forEach((test, idx) => {
    const icon = test.passed ? '✓' : '✗';
    const color = test.passed ? COLORS.green : COLORS.red;
    console.log(`  ${idx + 1}. ${color}${icon}${COLORS.reset} ${test.testName}`);
    console.log(`     ${test.duration}ms / ${test.threshold}ms${test.details ? ' - ' + test.details : ''}`);
  });

  // 建议
  console.log(`\n💡 性能建议:`);

  const slowTests = results.tests.filter(t => t.duration > t.threshold);
  if (slowTests.length > 0) {
    console.log(`  ${COLORS.yellow}• 以下接口响应较慢，建议优化:${COLORS.reset}`);
    slowTests.forEach(t => {
      console.log(`    - ${t.testName}: ${t.duration}ms (超出阈值 ${t.duration - t.threshold}ms)`);
    });
  } else {
    console.log(`  ${COLORS.green}• 所有接口响应时间均在正常范围内${COLORS.reset}`);
  }

  if (passRate >= 80) {
    console.log(`  ${COLORS.green}• 系统性能良好，可以支持生产环境部署${COLORS.reset}`);
  } else {
    console.log(`  ${COLORS.red}• 建议优化失败的测试项后再上线${COLORS.reset}`);
  }

  console.log(`\n${'='.repeat(60)}\n`);
}

// ==================== 主测试流程 ====================

async function runPerformanceTest() {
  console.log(`${COLORS.blue}${'='.repeat(60)}${COLORS.reset}`);
  console.log(`${COLORS.blue}供应商管理系统 - 性能测试${COLORS.reset}`);
  console.log(`${COLORS.blue}${'='.repeat(60)}${COLORS.reset}`);
  console.log(`${COLORS.gray}测试目标: API响应时间 < 500ms, 并发能力测试${COLORS.reset}\n`);

  try {
    // 1. 登录
    const token = await testLogin();
    if (!token) {
      console.log(`\n${COLORS.red}✗ 登录失败，终止测试${COLORS.reset}`);
      return;
    }

    // 2-7. 核心接口测试
    await testGetMe(token);
    await testGetSuppliers(token);
    const supplierId = await testCreateSupplier(token);
    await testGetSupplierDetail(token, supplierId);
    await testSearchSuppliers(token);
    await testGetStatistics(token);

    // 8. 并发测试
    await testConcurrentRequests(token);

    // 生成报告
    generateReport();

  } catch (error) {
    console.error(`\n${COLORS.red}测试过程发生错误:${COLORS.reset}`, error);
  }
}

// 启动测试
console.log(`${COLORS.cyan}正在启动性能测试...${COLORS.reset}\n`);
setTimeout(() => {
  runPerformanceTest().then(() => {
    console.log(`${COLORS.green}测试完成！${COLORS.reset}`);
    process.exit(0);
  });
}, 1000);
