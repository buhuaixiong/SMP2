<template>
  <div class="email-settings-container">
    <PageHeader>
      <template #title>
        <div class="header-content">
          <el-icon class="header-icon"><Message /></el-icon>
          <span class="header-title">邮件服务配置</span>
        </div>
      </template>
      <template #actions>
        <el-button link class="back-button" @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回
        </el-button>
      </template>
    </PageHeader>

    <el-card class="settings-card" shadow="never">
      <template #header>
        <div class="card-header">
          <span>SMTP 服务配置</span>
          <el-tag v-if="config.configured" type="success" effect="dark">已配置</el-tag>
          <el-tag v-else type="warning" effect="dark">未配置</el-tag>
        </div>
      </template>

      <el-alert
        v-if="!config.configured"
        title="邮件服务未配置"
        type="warning"
        description="系统需要配置SMTP服务才能发送邮件通知。请选择邮件服务提供商并填写相关配置。"
        :closable="false"
        show-icon
        class="alert-box"
      />

      <el-form
        ref="formRef"
        :model="formData"
        :rules="rules"
        label-width="140px"
        class="settings-form"
      >
        <!-- Provider Selection -->
        <el-form-item label="邮件服务商" prop="provider">
          <el-select
            v-model="formData.provider"
            placeholder="请选择邮件服务商"
            @change="handleProviderChange"
            class="full-width"
          >
            <el-option
              v-for="provider in providers"
              :key="provider.id"
              :label="provider.name"
              :value="provider.id"
            >
              <div class="provider-option">
                <span>{{ provider.name }}</span>
                <el-tag
                  v-if="provider.id === 'sendgrid' || provider.id === 'aws-ses'"
                  type="success"
                  size="small"
                  >企业推荐</el-tag
                >
              </div>
            </el-option>
          </el-select>
        </el-form-item>

        <!-- Provider Info -->
        <el-alert
          v-if="selectedProvider && selectedProvider.note"
          :title="selectedProvider.note"
          type="info"
          :closable="false"
          show-icon
          class="provider-note"
        />

        <!-- SMTP Host -->
        <el-form-item label="SMTP 服务器" prop="host">
          <el-input
            v-model="formData.host"
            placeholder="例如: smtp.sendgrid.net"
            :disabled="isProviderPreset && formData.provider !== 'custom'"
          >
            <template #prepend>
              <el-icon><Platform /></el-icon>
            </template>
          </el-input>
        </el-form-item>

        <!-- SMTP Port -->
        <el-form-item label="端口" prop="port">
          <el-input-number
            v-model="formData.port"
            :min="1"
            :max="65535"
            :disabled="isProviderPreset && formData.provider !== 'custom'"
            class="full-width"
          />
        </el-form-item>

        <!-- Security -->
        <el-form-item label="SSL/TLS">
          <el-switch
            v-model="formData.secure"
            :disabled="isProviderPreset && formData.provider !== 'custom'"
            active-text="启用 (端口 465)"
            inactive-text="STARTTLS (端口 587)"
          />
        </el-form-item>

        <!-- Username -->
        <el-form-item label="用户名" prop="user">
          <el-input
            v-model="formData.user"
            :placeholder="selectedProvider?.userLabel || '请输入SMTP用户名'"
          >
            <template #prepend>
              <el-icon><User /></el-icon>
            </template>
          </el-input>
        </el-form-item>

        <!-- Password -->
        <el-form-item label="密码" prop="password">
          <el-input
            v-model="formData.password"
            type="password"
            show-password
            :placeholder="
              config.hasPassword
                ? '留空则保持不变'
                : selectedProvider?.passwordLabel || '请输入SMTP密码'
            "
          >
            <template #prepend>
              <el-icon><Lock /></el-icon>
            </template>
          </el-input>
          <div v-if="selectedProvider?.docs" class="help-text">
            <el-link
              :href="selectedProvider.docs"
              type="primary"
              target="_blank"
              :underline="false"
            >
              <el-icon><Document /></el-icon>
              查看配置文档
            </el-link>
          </div>
        </el-form-item>

        <!-- From Email -->
        <el-form-item label="发件人邮箱" prop="from">
          <el-input v-model="formData.from" placeholder="例如: noreply@company.com">
            <template #prepend>
              <el-icon><Message /></el-icon>
            </template>
          </el-input>
        </el-form-item>

        <!-- From Name -->
        <el-form-item label="发件人名称" prop="fromName">
          <el-input v-model="formData.fromName" placeholder="例如: 供应商管理系统" />
        </el-form-item>

        <!-- Test Mode -->
        <el-form-item label="测试模式">
          <el-switch
            v-model="formData.testMode"
            active-text="启用 (邮件不会真实发送)"
            inactive-text="关闭 (生产模式)"
          />
        </el-form-item>

        <!-- Actions -->
        <el-form-item>
          <el-space>
            <el-button type="primary" :loading="saving" @click="handleSave">
              <el-icon><Check /></el-icon>
              保存配置
            </el-button>
            <el-button
              :loading="testing"
              @click="handleTest"
              :disabled="!config.configured && !hasFormData"
            >
              <el-icon><CircleCheck /></el-icon>
              发送测试邮件
            </el-button>
            <el-button v-if="config.configured" type="danger" plain @click="handleDelete">
              <el-icon><Delete /></el-icon>
              删除配置
            </el-button>
          </el-space>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- Email Templates -->
    <el-card class="templates-card" shadow="never">
      <template #header>
        <div class="card-header">
          <span>邮件模板列表</span>
          <el-tag>{{ templates.length }} 个模板</el-tag>
        </div>
      </template>

      <el-table :data="templates" stripe>
        <el-table-column prop="name" label="模板名称" width="200" />
        <el-table-column prop="description" label="说明" min-width="300" />
        <el-table-column prop="category" label="类别" width="100">
          <template #default="{ row }">
            <el-tag v-if="row.category === 'rfq'" type="primary" size="small">询价</el-tag>
            <el-tag v-else-if="row.category === 'contract'" type="warning" size="small"
              >合同</el-tag
            >
            <el-tag v-else-if="row.category === 'document'" type="info" size="small">文档</el-tag>
            <el-tag v-else-if="row.category === 'registration'" type="success" size="small"
              >Registration</el-tag
            >
            <el-tag v-else size="small">其他</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="变量" min-width="200">
          <template #default="{ row }">
            <el-tag
              v-for="variable in row.variables.slice(0, 3)"
              :key="variable"
              size="small"
              class="variable-tag"
            >
              {{ variable }}
            </el-tag>
            <el-tag v-if="row.variables.length > 3" size="small" type="info">
              +{{ row.variables.length - 3 }}
            </el-tag>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Test Email Dialog -->
    <el-dialog v-model="showTestDialog" title="发送测试邮件" width="500px">
      <el-form>
        <el-form-item label="收件人邮箱">
          <el-input v-model="testEmail" placeholder="请输入接收测试邮件的邮箱地址" type="email">
            <template #prepend>
              <el-icon><Message /></el-icon>
            </template>
          </el-input>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showTestDialog = false">取消</el-button>
        <el-button type="primary" :loading="testing" @click="sendTestEmail">
          <el-icon><Promotion /></el-icon>
          发送测试邮件
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { type FormInstance, type FormRules } from "element-plus";
import PageHeader from "@/components/layout/PageHeader.vue";
import {
  Message,
  Platform,
  User,
  Lock,
  Document,
  Check,
  CircleCheck,
  Delete,
  Promotion,
  ArrowLeft,
} from "@element-plus/icons-vue";
import { useNotification } from "@/composables";
import {
  getSmtpConfig,
  updateSmtpConfig,
  testSmtpConnection,
  deleteSmtpConfig,
  getSmtpProviders,
  getEmailTemplates,
  type SmtpConfig,
  type SmtpProvider,
  type EmailTemplate,
} from "@/api/emailSettings";

const notification = useNotification();

const router = useRouter();
const formRef = ref<FormInstance>();

// State
const loading = ref(false);
const saving = ref(false);
const testing = ref(false);
const showTestDialog = ref(false);
const testEmail = ref("");

const config = ref<SmtpConfig>({
  configured: false,
  provider: null,
  host: "",
  port: 587,
  secure: false,
  user: "",
  from: "",
  fromName: "Supplier Management System",
  testMode: false,
  hasPassword: false,
});

const formData = ref({
  provider: "sendgrid",
  host: "",
  port: 587,
  secure: false,
  user: "",
  password: "",
  from: "",
  fromName: "Supplier Management System",
  testMode: false,
});

const providers = ref<SmtpProvider[]>([]);
const templates = ref<EmailTemplate[]>([]);

// Computed
const selectedProvider = computed(() => {
  return providers.value.find((p) => p.id === formData.value.provider);
});

const isProviderPreset = computed(() => {
  return formData.value.provider !== "custom";
});

const hasFormData = computed(() => {
  return formData.value.host && formData.value.user && formData.value.password;
});

// Validation rules
const rules: FormRules = {
  provider: [{ required: true, message: "请选择邮件服务商", trigger: "change" }],
  host: [{ required: true, message: "请输入SMTP服务器地址", trigger: "blur" }],
  port: [{ required: true, message: "请输入端口号", trigger: "blur" }],
  user: [{ required: true, message: "请输入用户名", trigger: "blur" }],
  from: [
    { required: true, message: "请输入发件人邮箱", trigger: "blur" },
    { type: "email", message: "请输入有效的邮箱地址", trigger: "blur" },
  ],
  fromName: [{ required: true, message: "请输入发件人名称", trigger: "blur" }],
};

// Methods
const handleBack = () => {
  router.back();
};

const handleProviderChange = () => {
  const provider = selectedProvider.value;
  if (provider && provider.id !== "custom") {
    formData.value.host = provider.host;
    formData.value.port = provider.port;
    formData.value.secure = provider.secure;
  }
};

const handleSave = async () => {
  if (!formRef.value) return;

  await formRef.value.validate(async (valid) => {
    if (!valid) return;

    try {
      saving.value = true;

      await updateSmtpConfig(formData.value);

      notification.success("SMTP配置保存成功");
      await loadConfig();
    } catch (error: any) {
      console.error("Failed to save SMTP config:", error);
      notification.error(error.message || "保存配置失败");
    } finally {
      saving.value = false;
    }
  });
};

const handleTest = () => {
  showTestDialog.value = true;
};

const sendTestEmail = async () => {
  if (!testEmail.value) {
    notification.warning("请输入测试邮箱地址");
    return;
  }

  // Validate email
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(testEmail.value)) {
    notification.error("请输入有效的邮箱地址");
    return;
  }

  // If form has unsaved data, save first
  if (hasFormData.value && !config.value.configured) {
    notification.warning("请先保存配置");
    return;
  }

  try {
    testing.value = true;

    const result = await testSmtpConnection(testEmail.value);

    if (result.success) {
      notification.success("测试邮件发送成功！请检查收件箱。");
      showTestDialog.value = false;
      testEmail.value = "";
    } else {
      notification.error(`测试失败: ${result.error || result.message}`);
    }
  } catch (error: any) {
    console.error("Test email failed:", error);
    notification.error(error.message || "发送测试邮件失败");
  } finally {
    testing.value = false;
  }
};

const handleDelete = async () => {
  try {
    await notification.confirm("删除配置后，系统将无法发送邮件通知。确定要删除吗？", "确认删除", {
      confirmButtonText: "确定",
      cancelButtonText: "取消",
      type: "warning",
    });

    await deleteSmtpConfig();
    notification.success("配置已删除");
    await loadConfig();
  } catch (error: any) {
    if (error !== "cancel") {
      console.error("Failed to delete config:", error);
      notification.error(error.message || "删除配置失败");
    }
  }
};

const loadConfig = async () => {
  try {
    loading.value = true;
    config.value = await getSmtpConfig();

    if (config.value.configured) {
      formData.value = {
        provider: config.value.provider || "custom",
        host: config.value.host,
        port: config.value.port,
        secure: config.value.secure,
        user: config.value.user,
        password: "", // Don't populate password
        from: config.value.from,
        fromName: config.value.fromName,
        testMode: config.value.testMode,
      };
    }
  } catch (error) {
    console.error("Failed to load SMTP config:", error);
    notification.error("加载配置失败");
  } finally {
    loading.value = false;
  }
};

const loadProviders = async () => {
  try {
    providers.value = await getSmtpProviders();
  } catch (error) {
    console.error("Failed to load providers:", error);
  }
};

const loadTemplates = async () => {
  try {
    const list = await getEmailTemplates();
    templates.value = list.map((template) => ({
      ...template,
      variables: Array.isArray(template.variables) ? template.variables : [],
    }));
  } catch (error) {
    console.error("Failed to load templates:", error);
  }
};

onMounted(() => {
  loadConfig();
  loadProviders();
  loadTemplates();
});



</script>

<style scoped>
.email-settings-container {
  padding: 20px;
  max-width: 1200px;
  margin: 0 auto;
}

.header-content {
  display: flex;
  align-items: center;
  gap: 8px;
}

.header-icon {
  font-size: 20px;
  color: #409eff;
}

.header-title {
  font-size: 18px;
  font-weight: 600;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.back-button {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: #409eff;
  font-size: 14px;
  padding: 4px 8px;
}

.back-button:hover {
  color: #66b1ff;
}

.settings-card,
.templates-card {
  margin-top: 20px;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-weight: 600;
}

.alert-box {
  margin-bottom: 20px;
}

.settings-form {
  max-width: 800px;
}

.full-width {
  width: 100%;
}

.provider-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.provider-note {
  margin-bottom: 16px;
}

.help-text {
  margin-top: 8px;
  font-size: 12px;
  color: #909399;
}

.variable-tag {
  margin-right: 4px;
  margin-bottom: 4px;
}

.email-settings-container :deep(.page-header) {
  margin-bottom: 20px;
}
</style>
