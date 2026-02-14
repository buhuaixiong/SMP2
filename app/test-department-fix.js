/**
 * 测试department字段修复是否生效
 */

const axios = require('axios');
const jwt = require('jsonwebtoken');
const fs = require('fs');

const API_BASE = 'http://localhost:3001';

// 读取JWT_SECRET
const envContent = fs.readFileSync('./apps/api/.env', 'utf8');
const secretMatch = envContent.match(/JWT_SECRET=(.+)/);
const JWT_SECRET = secretMatch ? secretMatch[1].trim() : 'your-secret-key-here-CHANGE-IN-PRODUCTION';

console.log('=== 测试department字段修复 ===\n');

// 模拟登录获取token
async function testDepartmentFix() {
  try {
    // 直接生成token（模拟登录）
    const token = jwt.sign(
      {
        sub: 'dept001',  // David的用户ID
      },
      JWT_SECRET,
      { expiresIn: '1h' }
    );

    console.log('1. 使用David用户token调用API...');

    // 调用待确认列表API
    const response = await axios.get(
      `${API_BASE}/api/rfq/line-items/pending-approvals`,
      {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      }
    );

    console.log('2. API响应状态:', response.status);
    console.log('3. 响应数据结构:', {
      success: response.data.success,
      dataLength: response.data.data?.length,
      hasData: !!response.data.data
    });

    if (response.data.data && response.data.data.length > 0) {
      console.log('\n✅ 修复成功！');
      console.log('4. 返回的待确认项数量:', response.data.data.length);
      console.log('5. 第一项数据:');
      const first = response.data.data[0];
      console.log({
        id: first.id,
        item_name: first.item_name,
        status: first.status,
        rfq_title: first.rfq_title,
        requesting_department: first.requesting_department
      });
    } else {
      console.log('\n⚠️  修复可能未生效或需要重启服务器');
      console.log('返回的数据为空');
    }

  } catch (error) {
    console.error('❌ 测试失败:', error.response?.data || error.message);
    if (error.response?.status === 401) {
      console.log('\n提示: 认证失败，可能是因为：');
      console.log('1. 用户record查询返回null（department字段缺失导致）');
      console.log('2. 或者缓存还未清除');
      console.log('\n建议: 重启API服务器');
    }
  }
}

testDepartmentFix();
