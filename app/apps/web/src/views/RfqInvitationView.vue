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
            <el-descriptions-item label="项目编号"> #{{ rfqInfo.id }} </el-descriptions-item>
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
        <el-alert v-if="isRegistered" type="info" :closable="false" show-icon class="alert-box">
          <template #title>
            <strong>您已是注册供应商</strong>
          </template>
          <p>{{ message }}</p>
        </el-alert>

        <!-- Alert Box for Unregistered Suppliers -->
        <el-alert v-else type="warning" :closable="false" show-icon class="alert-box">
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
          <el-button v-if="isRegistered" type="primary" size="large" @click="goToLogin">
            前往登录
          </el-button>
          <el-button v-else type="warning" size="large" @click="goToRegister">
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

import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";

import { Loading, CircleClose } from "@element-plus/icons-vue";
import axios from "axios";

interface RfqPreviewInfo {
  id?: number | string;
  title?: string;
  inviterName?: string;
  deliveryPeriod?: number | string | null;
  budgetAmount?: number | string | null;
  currency?: string | null;
  validUntil?: string | null;
  description?: string | null;
}

interface PublicRfqPreviewResponse {
  success: boolean;
  isRegistered: boolean;
  rfqPreview: RfqPreviewInfo;
  message: string;
  recipientEmail: string;
}

const route = useRoute();
const router = useRouter();

const token = route.params.token as string;
const loading = ref(true);
const error = ref("");
const isRegistered = ref(false);
const rfqInfo = ref<RfqPreviewInfo>({});
const message = ref("");
const recipientEmail = ref("");

const formatDate = (dateStr: string) => {
  if (!dateStr) return "-";
  return new Date(dateStr).toLocaleString("zh-CN");
};

const goToLogin = () => {
  router.push({
    path: "/login",
    query: { redirect: `/rfq/${rfqInfo.value.id}` },
  });
};

const goToRegister = () => {
  router.push({
    path: "/supplier-registration",
    query: {
      token: token,
      rfqId: rfqInfo.value.id,
    },
  });
};

onMounted(async () => {
  try {
    const res = await axios.get<PublicRfqPreviewResponse>(`/api/public/rfq-preview/${token}`);

    if (res.data.success) {
      isRegistered.value = res.data.isRegistered;
      rfqInfo.value = res.data.rfqPreview;
      message.value = res.data.message;
      recipientEmail.value = res.data.recipientEmail;
    } else {
      error.value = res.data?.message || "Invalid invitation link";
    }
  } catch (err: unknown) {
    console.error("[RFQ Invitation] Error:", err);
    if (axios.isAxiosError(err) && err.response?.status === 404) {
      error.value = "无效的邀请链接";
    } else if (axios.isAxiosError(err) && err.response?.status === 403) {
      error.value = "邀请链接已过期";
    } else {
      error.value =
        (axios.isAxiosError(err) ? err.response?.data?.message : null) || "加载失败，请稍后重试";
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
