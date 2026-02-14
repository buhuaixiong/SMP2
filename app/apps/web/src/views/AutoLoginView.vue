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




import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAuthStore } from "@/stores/auth";

import { Loading } from "@element-plus/icons-vue";
import axios from "axios";


import { useNotification } from "@/composables";

const notification = useNotification();
const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

const statusMessage = ref("正在自动登录...");
const detailMessage = ref("请稍候，即将跳转");

onMounted(async () => {
  const token = route.query.token as string;
  const redirectPath = route.query.redirect as string;

  if (!token) {
    notification.error("缺少登录凭证");
    router.push("/login");
    return;
  }

  try {
    statusMessage.value = "验证邀请链接...";

    // Call auto-login API
    const res = await axios.get(`/api/public/auto-login/${token}`);

    if (res.data.success) {
      statusMessage.value = "登录成功！";
      detailMessage.value = "正在跳转到询价详情页...";

      // Save token and user info
      localStorage.setItem("token", res.data.token);
      localStorage.setItem("user", JSON.stringify(res.data.user));

      // Update auth store
      await authStore.fetchMe();

      // Redirect after short delay
      setTimeout(() => {
        const targetPath = redirectPath || res.data.redirectTo || "/dashboard";
        router.push(targetPath);
        notification.success("欢迎回来！");
      }, 800);
    } else {
      throw new Error(res.data?.message || "Auto login failed");
    }
  } catch (error: any) {
    console.error("[AutoLogin] Error:", error);
    statusMessage.value = "登录失败";
    detailMessage.value = error.response?.data?.message || "自动登录失败，请手动登录";

    if (!error.response?.data?.message && error?.message) {
      detailMessage.value = error.message;
    }
    notification.error(detailMessage.value);

    setTimeout(() => {
      router.push({
        path: "/login",
        query: { redirect: redirectPath },
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
