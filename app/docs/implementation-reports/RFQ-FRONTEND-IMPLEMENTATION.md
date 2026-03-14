# RFQ 邮件报价系统 - 前端实施指南

## 📄 文件 1: 询价预览页面（未注册供应商）

**路径**: `src/views/RfqInvitationView.vue`

```vue
<template>
  <div class="rfq-invitation-page">
    <div class="invitation-container">
      <!-- Loading State -->
      <div v-if="loading" class="loading-box">
        <el-icon class="is-loading" :size="50" color="#e6a23c">
          <Loading />
        </el-icon>
        <p>加载中...</p>
      </div>

      <!-- Error State -->
      <div v-else-if="error" class="error-box">
        <el-icon :size="60" color="#f56c6c">
          <CircleClose />
        </el-icon>
        <h2>{{ error }}</h2>
        <el-button @click="goToLogin">返回登录页</el-button>
      </div>

      <!-- Content -->
      <div v-else class="content">
        <div class="header">
          <h1>🎯 您收到了一封询价邀请</h1>
          <p>Request for Quotation Invitation</p>
        </div>

        <!-- RFQ Preview Card -->
        <el-card shadow="hover" class="rfq-card">
          <template #header>
            <span class="card-title">项目信息</span>
          </template>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="项目名称">
              <strong>{{ rfqInfo.title }}</strong>
            </el-descriptions-item>
            <el-descriptions-item label="项目编号">
              #{{ rfqInfo.id }}
            </el-descriptions-item>
            <el-descriptions-item label="邀请人">
              {{ rfqInfo.inviterName }}
            </el-descriptions-item>
            <el-descriptions-item label="交货期" v-if="rfqInfo.deliveryPeriod">
              {{ rfqInfo.deliveryPeriod }} 天
            </el-descriptions-item>
            <el-descriptions-item label="预算金额" v-if="rfqInfo.budgetAmount">
              {{ rfqInfo.budgetAmount }} {{ rfqInfo.currency }}
            </el-descriptions-item>
            <el-descriptions-item label="截止时间" v-if="rfqInfo.validUntil">
              {{ formatDate(rfqInfo.validUntil) }}
            </el-descriptions-item>
            <el-descriptions-item label="项目描述" v-if="rfqInfo.description">
              {{ rfqInfo.description }}
            </el-descriptions-item>
          </el-descriptions>
        </el-card>

        <!-- Alert Box for Registered Suppliers -->
        <el-alert
          v-if="isRegistered"
          type="info"
          :closable="false"
          show-icon
          class="alert-box"
        >
          <template #title>
            <strong>您已是注册供应商</strong>
          </template>
          <p>{{ message }}</p>
        </el-alert>

        <!-- Alert Box for Unregistered Suppliers -->
        <el-alert
          v-else
          type="warning"
          :closable="false"
          show-icon
          class="alert-box"
        >
          <template #title>
            <strong>⚠️ 完成供应商注册后即可：</strong>
          </template>
          <ul>
            <li>✓ 查看完整询价单详情</li>
            <li>✓ 在线填写并提交报价</li>
            <li>✓ 实时跟踪报价状态</li>
          </ul>
        </el-alert>

        <!-- Action Buttons -->
        <div class="action-buttons">
          <el-button
            v-if="isRegistered"
            type="primary"
            size="large"
            @click="goToLogin"
          >
            前往登录
          </el-button>
          <el-button
            v-else
            type="warning"
            size="large"
            @click="goToRegister"
          >
            📝 立即注册并查看完整询价
          </el-button>
        </div>

        <div class="footer-hint">
          <p v-if="!isRegistered">
            已有账号？<el-link type="primary" @click="goToLogin">点击登录</el-link>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ElMessage } from 'element-plus';
import { Loading, CircleClose } from '@element-plus/icons-vue';
import axios from 'axios';

const route = useRoute();
const router = useRouter();

const token = route.params.token as string;
const loading = ref(true);
const error = ref('');
const isRegistered = ref(false);
const rfqInfo = ref<any>({});
const message = ref('');
const recipientEmail = ref('');

const formatDate = (dateStr: string) => {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleString('zh-CN');
};

const goToLogin = () => {
  router.push({
    path: '/login',
    query: { redirect: `/rfq/${rfqInfo.value.id}` }
  });
};

const goToRegister = () => {
  router.push({
    path: '/supplier-registration',
    query: {
      token: token,
      rfqId: rfqInfo.value.id
    }
  });
};

onMounted(async () => {
  try {
    const res = await axios.get(`/api/public/rfq-preview/${token}`);

    if (res.data.success) {
      isRegistered.value = res.data.isRegistered;
      rfqInfo.value = res.data.rfqPreview;
      message.value = res.data.message;
      recipientEmail.value = res.data.recipientEmail;
    }
  } catch (err: any) {
    console.error('[RFQ Invitation] Error:', err);
    if (err.response?.status === 404) {
      error.value = '无效的邀请链接';
    } else if (err.response?.status === 403) {
      error.value = '邀请链接已过期';
    } else {
      error.value = err.response?.data?.message || '加载失败，请稍后重试';
    }
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.rfq-invitation-page {
  min-height: 100vh;
  background: linear-gradient(135deg, #f5a623 0%, #f2711c 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
}

.invitation-container {
  max-width: 800px;
  width: 100%;
}

.loading-box,
.error-box {
  background: white;
  padding: 60px;
  border-radius: 16px;
  text-align: center;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.error-box h2 {
  margin: 20px 0;
  color: #f56c6c;
}

.content {
  background: white;
  border-radius: 16px;
  overflow: hidden;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.header {
  background: linear-gradient(135deg, #e6a23c 0%, #d89614 100%);
  color: white;
  padding: 40px 30px;
  text-align: center;
}

.header h1 {
  margin: 0 0 10px 0;
  font-size: 28px;
}

.header p {
  margin: 0;
  opacity: 0.9;
  font-size: 14px;
}

.rfq-card {
  margin: 30px;
}

.card-title {
  font-weight: bold;
  font-size: 16px;
}

.alert-box {
  margin: 0 30px 20px 30px;
}

.alert-box ul {
  margin: 10px 0 0 0;
  padding-left: 20px;
}

.alert-box li {
  margin: 5px 0;
}

.action-buttons {
  text-align: center;
  padding: 30px;
}

.footer-hint {
  text-align: center;
  padding: 0 30px 30px 30px;
  color: #666;
  font-size: 14px;
}
</style>
```

---

## 📄 文件 2: 自动登录页面

**路径**: `src/views/AutoLoginView.vue`

```vue
<template>
  <div class="auto-login-page">
    <div class="login-box">
      <el-icon class="is-loading" :size="60" color="#409eff">
        <Loading />
      </el-icon>
      <h2>{{ statusMessage }}</h2>
      <p>{{ detailMessage }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { ElMessage } from 'element-plus';
import { Loading } from '@element-plus/icons-vue';
import axios from 'axios';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

const statusMessage = ref('正在自动登录...');
const detailMessage = ref('请稍候，即将跳转');

onMounted(async () => {
  const token = route.query.token as string;
  const redirectPath = route.query.redirect as string;

  if (!token) {
    ElMessage.error('缺少登录凭证');
    router.push('/login');
    return;
  }

  try {
    statusMessage.value = '验证邀请链接...';

    // Call auto-login API
    const res = await axios.get(`/api/public/auto-login/${token}`);

    if (res.data.success) {
      statusMessage.value = '登录成功！';
      detailMessage.value = '正在跳转到询价详情页...';

      // Save token and user info
      localStorage.setItem('token', res.data.token);
      localStorage.setItem('user', JSON.stringify(res.data.user));

      // Update auth store
      await authStore.fetchMe();

      // Redirect after short delay
      setTimeout(() => {
        const targetPath = redirectPath || res.data.redirectTo || '/dashboard';
        router.push(targetPath);
        ElMessage.success('欢迎回来！');
      }, 800);
    }
  } catch (error: any) {
    console.error('[AutoLogin] Error:', error);
    statusMessage.value = '登录失败';
    detailMessage.value = error.response?.data?.message || '自动登录失败，请手动登录';

    ElMessage.error(detailMessage.value);

    setTimeout(() => {
      router.push({
        path: '/login',
        query: { redirect: redirectPath }
      });
    }, 2000);
  }
});
</script>

<style scoped>
.auto-login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.login-box {
  background: white;
  padding: 60px 80px;
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  text-align: center;
  min-width: 400px;
}

.login-box h2 {
  margin: 20px 0 10px;
  color: #333;
  font-size: 24px;
}

.login-box p {
  color: #666;
  font-size: 14px;
  margin: 0;
}
</style>
```

---

## 📄 文件 3: 优化供应商注册页面

**路径**: `src/views/SupplierRegistrationView.vue`

在现有文件中添加以下功能：

### 3.1 在 `<script setup>` 顶部添加：

```typescript
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

// 检查是否来自 RFQ 邀请
const invitationToken = ref(route.query.token as string);
const targetRfqId = ref(route.query.rfqId as string);
const showInvitationBanner = ref(false);

onMounted(() => {
  if (invitationToken.value && targetRfqId.value) {
    showInvitationBanner.value = true;
  }
});
```

### 3.2 在模板顶部添加提示横幅：

```vue
<template>
  <div class="supplier-registration-view">
    <!-- RFQ Invitation Banner -->
    <el-alert
      v-if="showInvitationBanner"
      type="info"
      :closable="false"
      show-icon
      class="invitation-banner"
    >
      <template #title>
        <strong>ℹ️ 完成注册后将自动跳转到询价详情页</strong>
      </template>
      <p>您正在通过 RFQ 邀请链接注册，注册成功后可立即查看完整询价单并提交报价。</p>
    </el-alert>

    <!-- 原有的注册表单内容... -->
  </div>
</template>
```

### 3.3 修改注册成功处理函数：

```typescript
const handleRegistrationSuccess = async (response: any) => {
  const { supplierCode, defaultPassword, supplierId } = response;

  // 显示账号信息
  await ElMessageBox.alert(
    `注册成功！\n\n您的供应商编码：${supplierCode}\n初始密码：${defaultPassword}\n\n请妥善保管您的账号信息。`,
    '注册成功',
    {
      confirmButtonText: targetRfqId.value ? '立即登录并查看询价' : '立即登录',
      type: 'success'
    }
  );

  try {
    // 自动登录
    await authStore.login(supplierCode, defaultPassword);

    ElMessage.success('登录成功！');

    // 跳转
    if (targetRfqId.value) {
      router.push(`/rfq/${targetRfqId.value}`);
    } else {
      router.push('/dashboard');
    }
  } catch (loginError) {
    console.error('[Registration] Auto-login failed:', loginError);
    ElMessage.warning('请使用刚才的账号密码手动登录');
    router.push({
      path: '/login',
      query: targetRfqId.value ? { redirect: `/rfq/${targetRfqId.value}` } : {}
    });
  }
};
```

### 3.4 添加样式：

```vue
<style scoped>
.invitation-banner {
  margin-bottom: 20px;
}
</style>
```

---

## 📄 文件 4: 路由配置

**路径**: `src/router/index.ts`

在 `routes` 数组中添加以下路由：

```typescript
{
  path: '/rfq-invitation/:token',
  name: 'rfq-invitation',
  component: () => import('@/views/RfqInvitationView.vue'),
  meta: {
    requiresAuth: false,  // 公开路由，无需登录
    title: '询价邀请'
  }
},
{
  path: '/auto-login',
  name: 'auto-login',
  component: () => import('@/views/AutoLoginView.vue'),
  meta: {
    requiresAuth: false,  // 公开路由，无需登录
    title: '自动登录'
  }
}
```

---

## 🎨 最终效果预览

### 场景 1：已注册供应商
```
用户点击邮件链接
  ↓
打开 /auto-login?token=xxx&redirect=/rfq/123
  ↓
显示"正在自动登录..."（带加载动画）
  ↓
0.8秒后跳转到 /rfq/123
  ↓
显示"欢迎回来！"提示
```

### 场景 2：未注册供应商
```
用户点击邮件链接
  ↓
打开 /rfq-invitation/xxx
  ↓
显示 RFQ 基本信息
  ↓
点击"立即注册并查看完整询价"
  ↓
跳转到 /supplier-registration?token=xxx&rfqId=123
  ↓
顶部显示蓝色提示条："完成注册后将自动跳转到询价详情页"
  ↓
填写表单 → 提交
  ↓
显示账号密码弹窗
  ↓
点击"立即登录并查看询价"
  ↓
自动登录成功
  ↓
跳转到 /rfq/123
```

---

## ✅ 实施检查清单

- [ ] 创建 `src/views/RfqInvitationView.vue`
- [ ] 创建 `src/views/AutoLoginView.vue`
- [ ] 修改 `src/views/SupplierRegistrationView.vue`
  - [ ] 添加 invitationToken 和 targetRfqId 状态
  - [ ] 添加提示横幅
  - [ ] 修改注册成功处理函数
- [ ] 修改 `src/router/index.ts`
  - [ ] 添加 /rfq-invitation/:token 路由
  - [ ] 添加 /auto-login 路由

---

## 🧪 测试命令

```bash
# 启动前端开发服务器
npm run dev

# 打开浏览器测试：
# 1. http://localhost:5173/rfq-invitation/test-token-123
# 2. http://localhost:5173/auto-login?token=test-token-456&redirect=/dashboard
# 3. http://localhost:5173/supplier-registration?token=test&rfqId=123
```

---

## 📌 注意事项

1. **图标导入**：确保从 `@element-plus/icons-vue` 导入 `Loading` 和 `CircleClose`
2. **Auth Store**：确保 `useAuthStore` 中有 `fetchMe()` 和 `login()` 方法
3. **API Base URL**：确保 axios 配置了正确的 baseURL
4. **类型定义**：如需 TypeScript 类型，可在 `src/types/index.ts` 中添加相关接口
