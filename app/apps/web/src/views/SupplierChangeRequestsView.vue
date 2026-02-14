<template>
  <div class="change-requests-view">
    <PageHeader
      title="我的资料变更申请"
      subtitle="查看您提交的资料修改审批记录"
    />

    <div class="content-wrapper">
      <!-- 统计卡片 -->
      <el-row :gutter="20" class="stats-row">
        <el-col :span="6">
          <div class="stat-card">
            <div class="stat-icon pending">
              <el-icon><Clock /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ stats.pending }}</div>
              <div class="stat-label">审批中</div>
            </div>
          </div>
        </el-col>
        <el-col :span="6">
          <div class="stat-card">
            <div class="stat-icon approved">
              <el-icon><CircleCheck /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ stats.approved }}</div>
              <div class="stat-label">已通过</div>
            </div>
          </div>
        </el-col>
        <el-col :span="6">
          <div class="stat-card">
            <div class="stat-icon rejected">
              <el-icon><CircleClose /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ stats.rejected }}</div>
              <div class="stat-label">已拒绝</div>
            </div>
          </div>
        </el-col>
        <el-col :span="6">
          <div class="stat-card">
            <div class="stat-icon total">
              <el-icon><Document /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ stats.total }}</div>
              <div class="stat-label">总申请数</div>
            </div>
          </div>
        </el-col>
      </el-row>

      <!-- 申请列表 -->
      <el-card class="list-card" shadow="never">
        <template #header>
          <div class="card-header">
            <span>变更申请列表</span>
            <el-button type="primary" size="small" @click="refreshList">
              <el-icon><Refresh /></el-icon>
              刷新
            </el-button>
          </div>
        </template>

        <el-table v-loading="loading" :data="requests" stripe>
          <el-table-column prop="id" label="申请编号" width="100" />

          <el-table-column label="风险等级" width="100">
            <template #default="{ row }">
              <el-tag
                :type="
                  row.riskLevel === 'high'
                    ? 'danger'
                    : row.riskLevel === 'medium'
                      ? 'warning'
                      : 'info'
                "
                size="small"
              >
                {{ getRiskLevelText(row.riskLevel) }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column label="修改字段数" width="120">
            <template #default="{ row }">
              <el-tag size="small">{{ getChangedFieldsCount(row.payload) }} 个字段</el-tag>
            </template>
          </el-table-column>

          <el-table-column label="当前状态" width="150">
            <template #default="{ row }">
              <el-tag :type="getStatusType(row.status)" size="small">
                {{ getStatusText(row.status) }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column label="当前步骤" width="140">
            <template #default="{ row }">
              <span v-if="row.status.includes('pending')">
                {{ getCurrentStepText(row.currentStep) }}
              </span>
              <span v-else style="color: #909399">-</span>
            </template>
          </el-table-column>

          <el-table-column prop="submittedAt" label="提交时间" width="180">
            <template #default="{ row }">
              {{ formatDateTime(row.submittedAt) }}
            </template>
          </el-table-column>

          <el-table-column label="操作" width="120" fixed="right">
            <template #default="{ row }">
              <el-button type="primary" size="small" link @click="viewDetails(row)">
                查看详情
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <div v-if="requests.length === 0 && !loading" class="empty-state">
          <el-empty description="暂无变更申请记录">
            <el-button type="primary" @click="handleCreateRequest">提交新申请</el-button>
          </el-empty>
        </div>
      </el-card>
    </div>

    <!-- 详情对话框 -->
    <el-dialog v-model="detailsVisible" title="变更申请详情" width="900px" destroy-on-close>
      <div v-if="selectedRequest" class="details-content">
        <!-- 基本信息 -->
        <el-descriptions :column="2" border>
          <el-descriptions-item label="申请编号">
            {{ selectedRequest.id }}
          </el-descriptions-item>
          <el-descriptions-item label="风险等级">
            <el-tag
              :type="
                selectedRequest.riskLevel === 'high'
                  ? 'danger'
                  : selectedRequest.riskLevel === 'medium'
                    ? 'warning'
                    : 'info'
              "
              size="small"
            >
              {{ getRiskLevelText(selectedRequest.riskLevel) }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="提交时间">
            {{ formatDateTime(selectedRequest.submittedAt) }}
          </el-descriptions-item>
          <el-descriptions-item label="最后更新">
            {{ formatDateTime(selectedRequest.updatedAt) }}
          </el-descriptions-item>
          <el-descriptions-item label="当前状态" :span="2">
            <el-tag :type="getStatusType(selectedRequest.status)" size="small">
              {{ getStatusText(selectedRequest.status) }}
            </el-tag>
          </el-descriptions-item>
        </el-descriptions>

        <!-- 变更内容 -->
        <el-divider content-position="left">变更内容</el-divider>
        <el-table :data="getChangedFieldsArray(selectedRequest.payload)" border>
          <el-table-column prop="label" label="字段名称" width="150" />
          <el-table-column prop="newValue" label="申请修改为">
            <template #default="{ row }">
              <span style="color: #409eff; font-weight: 600">{{ row.newValue || "-" }}</span>
            </template>
          </el-table-column>
        </el-table>

        <!-- 审批进度 -->
        <el-divider content-position="left">审批进度</el-divider>
        <ChangeRequestTimeline
          v-if="requestDetails"
          :workflow="requestDetails.workflow"
          :approval-history="requestDetails.approvalHistory"
          :current-step="selectedRequest.currentStep"
          :status="selectedRequest.status"
        />
        <div v-else style="text-align: center; padding: 20px; color: #909399">
          加载审批进度中...
        </div>
      </div>
    </el-dialog>

    <!-- 创建变更申请对话框 -->
    <el-dialog
      v-model="createDialogVisible"
      title="提交资料变更申请"
      width="1000px"
      :close-on-click-modal="false"
      @close="handleCloseCreateDialog"
    >
      <div v-loading="createFormLoading" class="create-form-content">
        <!-- 审批流程说明 -->
        <el-alert type="warning" :closable="false" show-icon style="margin-bottom: 20px">
          <template #title>审批流程说明</template>
          <ul class="workflow-notice">
            <li>
              <strong>修改必填字段（⭐标记）：</strong>需要完整审批流程（采购员 → 品质 → 采购经理 →
              采购总监 → 财务总监）
            </li>
            <li><strong>仅修改可选字段：</strong>只需采购员确认即可</li>
            <li><strong>同时修改两类字段：</strong>将进入完整审批流程</li>
          </ul>
        </el-alert>

        <el-form :model="changeForm" label-width="150px" label-position="right">
          <el-collapse v-model="activeCollapsePanels" class="change-form-collapse">
            <!-- 基本信息（必填字段） -->
            <el-collapse-item title="基本信息（必填字段 ⭐）" name="basic">
              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="公司名称 ⭐">
                    <el-input v-model="changeForm.companyName" placeholder="请输入公司名称" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="供应商代码 ⭐">
                    <el-input v-model="changeForm.companyId" placeholder="请输入供应商代码" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="供应商类别 ⭐">
                    <el-input v-model="changeForm.category" placeholder="请输入供应商类别" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="地区 ⭐">
                    <el-input v-model="changeForm.region" placeholder="请输入地区" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-form-item label="注册地址 ⭐">
                <el-input v-model="changeForm.address" placeholder="请输入注册地址" />
              </el-form-item>

              <el-form-item label="营业执照号 ⭐">
                <el-input
                  v-model="changeForm.businessRegistrationNumber"
                  placeholder="请输入营业执照号"
                />
              </el-form-item>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="主联系人 ⭐">
                    <el-input v-model="changeForm.contactPerson" placeholder="请输入主联系人姓名" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="联系电话 ⭐">
                    <el-input v-model="changeForm.contactPhone" placeholder="请输入联系电话" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-form-item label="联系邮箱 ⭐">
                <el-input
                  v-model="changeForm.contactEmail"
                  type="email"
                  placeholder="请输入联系邮箱"
                />
              </el-form-item>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="付款条款 ⭐">
                    <el-input v-model="changeForm.paymentTerms" placeholder="请输入付款条款" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="付款币种 ⭐">
                    <el-select
                      v-model="changeForm.paymentCurrency"
                      placeholder="请选择付款币种"
                      style="width: 100%"
                    >
                      <el-option label="人民币 (RMB)" value="RMB" />
                      <el-option label="美元 (USD)" value="USD" />
                      <el-option label="欧元 (EUR)" value="EUR" />
                      <el-option label="英镑 (GBP)" value="GBP" />
                      <el-option label="韩元 (KRW)" value="KRW" />
                      <el-option label="泰铢 (THB)" value="THB" />
                      <el-option label="日元 (JPY)" value="JPY" />
                    </el-select>
                  </el-form-item>
                </el-col>
              </el-row>

              <el-form-item label="银行账户 ⭐">
                <el-input v-model="changeForm.bankAccount" placeholder="请输入银行账户" />
              </el-form-item>
            </el-collapse-item>

            <!-- 公司详情 -->
            <el-collapse-item title="公司详情" name="company">
              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="英文名称">
                    <el-input v-model="changeForm.englishName" placeholder="请输入英文名称" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="中文名称">
                    <el-input v-model="changeForm.chineseName" placeholder="请输入中文名称" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="公司类型">
                    <el-select
                      v-model="changeForm.companyType"
                      placeholder="请选择公司类型"
                      style="width: 100%"
                    >
                      <el-option label="有限公司" value="limited" />
                      <el-option label="合伙企业" value="partnership" />
                      <el-option label="独资企业" value="sole_proprietor" />
                      <el-option label="其他" value="other" />
                    </el-select>
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item v-if="changeForm.companyType === 'other'" label="其他公司类型">
                    <el-input
                      v-model="changeForm.companyTypeOther"
                      placeholder="请输入其他公司类型"
                    />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-row v-if="changeForm.companyType === 'limited'" :gutter="20">
                <el-col :span="12">
                  <el-form-item label="注册资本">
                    <el-input v-model="changeForm.authorizedCapital" placeholder="请输入注册资本" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="实收资本">
                    <el-input v-model="changeForm.issuedCapital" placeholder="请输入实收资本" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-form-item v-if="changeForm.companyType === 'limited'" label="董事">
                <el-input
                  v-model="changeForm.directors"
                  type="textarea"
                  :rows="2"
                  placeholder="请输入董事信息"
                />
              </el-form-item>

              <el-form-item
                v-if="
                  changeForm.companyType === 'partnership' ||
                  changeForm.companyType === 'sole_proprietor'
                "
                label="所有者"
              >
                <el-input
                  v-model="changeForm.owners"
                  type="textarea"
                  :rows="2"
                  placeholder="请输入所有者信息"
                />
              </el-form-item>
            </el-collapse-item>

            <!-- 注册信息 -->
            <el-collapse-item title="注册信息" name="registration">
              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="注册办公室">
                    <el-input
                      v-model="changeForm.registeredOffice"
                      placeholder="请输入注册办公室地址"
                    />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="营业地址">
                    <el-input v-model="changeForm.businessAddress" placeholder="请输入营业地址" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="营业电话">
                    <el-input v-model="changeForm.businessPhone" placeholder="请输入营业电话" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="传真">
                    <el-input v-model="changeForm.faxNumber" placeholder="请输入传真" />
                  </el-form-item>
                </el-col>
              </el-row>
            </el-collapse-item>

            <!-- 联系人信息 -->
            <el-collapse-item title="其他联系人" name="contacts">
              <el-divider content-position="left">销售联系人</el-divider>
              <el-row :gutter="20">
                <el-col :span="8">
                  <el-form-item label="姓名">
                    <el-input
                      v-model="changeForm.salesContactName"
                      placeholder="请输入销售联系人姓名"
                    />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="邮箱">
                    <el-input
                      v-model="changeForm.salesContactEmail"
                      type="email"
                      placeholder="请输入销售联系人邮箱"
                    />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="电话">
                    <el-input
                      v-model="changeForm.salesContactPhone"
                      placeholder="请输入销售联系人电话"
                    />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-divider content-position="left">财务联系人</el-divider>
              <el-row :gutter="20">
                <el-col :span="8">
                  <el-form-item label="姓名">
                    <el-input
                      v-model="changeForm.financeContactName"
                      placeholder="请输入财务联系人姓名"
                    />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="邮箱">
                    <el-input
                      v-model="changeForm.financeContactEmail"
                      type="email"
                      placeholder="请输入财务联系人邮箱"
                    />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="电话">
                    <el-input
                      v-model="changeForm.financeContactPhone"
                      placeholder="请输入财务联系人电话"
                    />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-divider content-position="left">客服联系人</el-divider>
              <el-row :gutter="20">
                <el-col :span="8">
                  <el-form-item label="姓名">
                    <el-input
                      v-model="changeForm.customerServiceContactName"
                      placeholder="请输入客服联系人姓名"
                    />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="邮箱">
                    <el-input
                      v-model="changeForm.customerServiceContactEmail"
                      type="email"
                      placeholder="请输入客服联系人邮箱"
                    />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="电话">
                    <el-input
                      v-model="changeForm.customerServiceContactPhone"
                      placeholder="请输入客服联系人电话"
                    />
                  </el-form-item>
                </el-col>
              </el-row>
            </el-collapse-item>

            <!-- 业务信息 -->
            <el-collapse-item title="业务信息" name="business">
              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="运营币种">
                    <el-select
                      v-model="changeForm.operatingCurrency"
                      placeholder="请选择运营币种"
                      style="width: 100%"
                    >
                      <el-option label="人民币 (RMB)" value="RMB" />
                      <el-option label="美元 (USD)" value="USD" />
                      <el-option label="欧元 (EUR)" value="EUR" />
                      <el-option label="英镑 (GBP)" value="GBP" />
                      <el-option label="韩元 (KRW)" value="KRW" />
                      <el-option label="泰铢 (THB)" value="THB" />
                      <el-option label="日元 (JPY)" value="JPY" />
                    </el-select>
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="国际贸易术语">
                    <el-select
                      v-model="changeForm.shipCode"
                      placeholder="请选择贸易术语"
                      style="width: 100%"
                    >
                      <el-option
                        v-for="option in incotermOptions"
                        :key="option.value"
                        :label="option.label"
                        :value="option.value"
                      />
                    </el-select>
                  </el-form-item>
                </el-col>
              </el-row>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="成立年份">
                    <el-input v-model="changeForm.establishedYear" placeholder="请输入成立年份" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="员工数量">
                    <el-input v-model="changeForm.employeeCount" placeholder="请输入员工数量" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="产品原产地">
                    <el-input v-model="changeForm.productOrigin" placeholder="请输入产品原产地" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="交货地点">
                    <el-input v-model="changeForm.deliveryLocation" placeholder="请输入交货地点" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-form-item label="业务性质">
                <el-input
                  v-model="changeForm.businessNature"
                  type="textarea"
                  :rows="2"
                  placeholder="请输入业务性质"
                />
              </el-form-item>

              <el-form-item label="ECI产品">
                <el-input
                  v-model="changeForm.productsForEci"
                  type="textarea"
                  :rows="2"
                  placeholder="请输入ECI产品信息"
                />
              </el-form-item>

              <el-form-item label="质量认证">
                <el-input
                  v-model="changeForm.qualityCertifications"
                  type="textarea"
                  :rows="2"
                  placeholder="请输入质量认证信息"
                />
              </el-form-item>
            </el-collapse-item>

            <!-- 付款与银行信息 -->
            <el-collapse-item title="付款与银行信息" name="payment">
              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="发票类型">
                    <el-input v-model="changeForm.invoiceType" placeholder="请输入发票类型" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="付款天数">
                    <el-input
                      v-model="changeForm.paymentTermsDays"
                      type="number"
                      placeholder="请输入付款天数"
                    />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-form-item label="付款方式">
                <el-checkbox-group v-model="changeForm.paymentMethods">
                  <el-checkbox label="cash">现金</el-checkbox>
                  <el-checkbox label="cheque">支票</el-checkbox>
                  <el-checkbox label="wire">电汇</el-checkbox>
                  <el-checkbox label="other">其他</el-checkbox>
                </el-checkbox-group>
              </el-form-item>

              <el-form-item v-if="changeForm.paymentMethods.includes('other')" label="其他付款方式">
                <el-input
                  v-model="changeForm.paymentMethodsOther"
                  placeholder="请输入其他付款方式"
                />
              </el-form-item>

              <el-divider content-position="left">银行信息</el-divider>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item label="银行名称">
                    <el-input v-model="changeForm.bankName" placeholder="请输入银行名称" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="SWIFT代码">
                    <el-input v-model="changeForm.swiftCode" placeholder="请输入SWIFT代码" />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-form-item label="银行地址">
                <el-input v-model="changeForm.bankAddress" placeholder="请输入银行地址" />
              </el-form-item>
            </el-collapse-item>

            <!-- 其他信息 -->
            <el-collapse-item title="其他信息" name="other">
              <el-form-item label="服务类别">
                <el-input v-model="changeForm.serviceCategory" placeholder="请输入服务类别" />
              </el-form-item>

              <el-form-item label="备注">
                <el-input
                  v-model="changeForm.notes"
                  type="textarea"
                  :rows="3"
                  placeholder="请输入备注信息"
                />
              </el-form-item>
            </el-collapse-item>
          </el-collapse>
        </el-form>
      </div>

      <template #footer>
        <el-button @click="handleCloseCreateDialog">取消</el-button>
        <el-button type="primary" :loading="createFormLoading" @click="handleSubmitChangeRequest">
          提交申请
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted, reactive, defineAsyncComponent } from "vue";
import { useRouter } from "vue-router";

import { Clock, CircleCheck, CircleClose, Document, Refresh } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import { useAuthStore } from "@/stores/auth";
import {
  getSupplierChangeRequests,
  getChangeRequestDetails,
  submitChangeRequest,
} from "@/api/changeRequests";
import type { ChangeRequest, ChangeRequestDetails } from "@/api/changeRequests";
import { fetchSuppliers } from "@/api/suppliers";
import type { Supplier } from "@/types";
import { useI18n } from "vue-i18n";


import { useNotification } from "@/composables";
const notification = useNotification();

const ChangeRequestTimeline = defineAsyncComponent(
  () => import("@/components/ChangeRequestTimeline.vue"),
);

const router = useRouter();
const authStore = useAuthStore();
const { t } = useI18n();

const loading = ref(false);
const requests = ref<ChangeRequest[]>([]);
const detailsVisible = ref(false);
const selectedRequest = ref<ChangeRequest | null>(null);
const requestDetails = ref<ChangeRequestDetails | null>(null);

// Create dialog state
const createDialogVisible = ref(false);
const createFormLoading = ref(false);
const originalSupplierData = ref<Supplier | null>(null);

const incotermCodes = [
  "EXW",
  "FOB",
  "CIF",
  "CFR",
  "DDP",
  "DDU",
  "DAP",
  "DAT",
  "FCA",
  "CPT",
  "CIP",
] as const;

const incotermOptions = computed(() =>
  incotermCodes.map((code) => ({
    value: code,
    label: t(`supplierRegistration.incoterms.${code}`),
  })),
);

// Form data for change request
const changeForm = reactive({
  // 必填字段（12个）
  companyName: "",
  companyId: "",
  contactPerson: "",
  contactPhone: "",
  contactEmail: "",
  category: "",
  address: "",
  businessRegistrationNumber: "",
  paymentTerms: "",
  paymentCurrency: "",
  bankAccount: "",
  region: "",

  // 公司详情（可选）
  englishName: "",
  chineseName: "",
  companyType: "",
  companyTypeOther: "",
  authorizedCapital: "",
  issuedCapital: "",
  directors: "",
  owners: "",

  // 注册信息（可选）
  registeredOffice: "",
  businessAddress: "",
  businessPhone: "",
  faxNumber: "",

  // 额外联系人（可选）
  salesContactName: "",
  salesContactEmail: "",
  salesContactPhone: "",
  financeContactName: "",
  financeContactEmail: "",
  financeContactPhone: "",
  customerServiceContactName: "",
  customerServiceContactEmail: "",
  customerServiceContactPhone: "",

  // 业务信息（可选）
  businessNature: "",
  operatingCurrency: "",
  deliveryLocation: "",
  shipCode: "",
  productOrigin: "",
  productsForEci: "",
  establishedYear: "",
  employeeCount: "",
  qualityCertifications: "",

  // 付款详情（可选）
  invoiceType: "",
  paymentTermsDays: "",
  paymentMethods: [] as string[],
  paymentMethodsOther: "",

  // 银行详情（可选）
  bankName: "",
  bankAddress: "",
  swiftCode: "",

  // 其他（可选）
  notes: "",
  serviceCategory: "",
});

// 必填字段集合
const REQUIRED_FIELDS = new Set([
  "companyName",
  "companyId",
  "contactPerson",
  "contactPhone",
  "contactEmail",
  "category",
  "address",
  "businessRegistrationNumber",
  "paymentTerms",
  "paymentCurrency",
  "bankAccount",
  "region",
]);

// 判断是否为必填字段
const isRequired = (fieldKey: string) => REQUIRED_FIELDS.has(fieldKey);

// 折叠面板激活状态
const activeCollapsePanels = ref(["basic"]);

// 字段标签映射
const fieldLabels: Record<string, string> = {
  companyName: "公司名称",
  companyId: "供应商代码",
  contactPerson: "主联系人",
  contactPhone: "联系电话",
  contactEmail: "联系邮箱",
  category: "供应商类别",
  address: "注册地址",
  businessRegistrationNumber: "营业执照号",
  paymentTerms: "付款条款",
  paymentCurrency: "付款币种",
  bankAccount: "银行账户",
  region: "地区",
  productOrigin: "产品原产地",
};

// 统计数据
const stats = computed(() => {
  return {
    total: requests.value.length,
    pending: requests.value.filter((r) => r.status.includes("pending")).length,
    approved: requests.value.filter((r) => r.status === "approved").length,
    rejected: requests.value.filter((r) => r.status === "rejected").length,
  };
});

// 加载申请列表
const loadRequests = async () => {
  if (!authStore.user?.supplierId) {
    notification.error("无法获取供应商信息");
    return;
  }

  try {
    loading.value = true;
    requests.value = await getSupplierChangeRequests(authStore.user.supplierId);
  } catch (error: any) {
    console.error("加载变更申请失败:", error);
    notification.error(error.message || "加载失败");
  } finally {
    loading.value = false;
  }
};

const refreshList = () => {
  loadRequests();
  notification.success("已刷新");
};

// 查看详情
const viewDetails = async (request: ChangeRequest) => {
  selectedRequest.value = request;
  detailsVisible.value = true;
  requestDetails.value = null;

  try {
    requestDetails.value = await getChangeRequestDetails(request.id);
  } catch (error: any) {
    console.error("加载详情失败:", error);
    notification.error("加载详情失败");
  }
};

// 获取风险等级文本
const getRiskLevelText = (level?: string) => {
  const map: Record<string, string> = {
    high: "高风险",
    medium: "中风险",
    low: "低风险",
  };
  return map[level || "low"] || "未知";
};

// 获取状态文本
const getStatusText = (status: string) => {
  if (status === "approved") return "已批准";
  if (status === "rejected") return "已拒绝";
  if (status.startsWith("pending_")) {
    return "审批中";
  }
  return status;
};

// 获取状态类型
const getStatusType = (status: string) => {
  if (status === "approved") return "success";
  if (status === "rejected") return "danger";
  if (status.includes("pending")) return "warning";
  return "info";
};

// 获取当前步骤文本
const getCurrentStepText = (step: string) => {
  const map: Record<string, string> = {
    purchaser: "采购员审批",
    quality_manager: "品质审批",
    procurement_manager: "采购经理审批",
    procurement_director: "采购总监审批",
    finance_director: "财务总监审批",
  };
  return map[step] || step;
};

// 获取变更字段数量
const getChangedFieldsCount = (payload: any) => {
  return Object.keys(payload || {}).length;
};

// 获取变更字段数组
const getChangedFieldsArray = (payload: any) => {
  if (!payload) return [];
  return Object.entries(payload).map(([key, value]) => ({
    key,
    label: fieldLabels[key] || key,
    newValue: value,
  }));
};

// 格式化日期时间
const formatDateTime = (dateStr: string) => {
  if (!dateStr) return "-";
  return new Date(dateStr).toLocaleString("zh-CN");
};

// 加载供应商数据
const loadSupplierData = async () => {
  if (!authStore.user?.supplierId) {
    notification.error("无法获取供应商信息");
    return;
  }

  try {
    createFormLoading.value = true;
    const suppliers = await fetchSuppliers({ limit: 1 });

    if (suppliers.length === 0) {
      notification.error("未找到供应商数据");
      return;
    }

    const supplier = suppliers[0];
    originalSupplierData.value = supplier;

    // Populate form with existing data - all fields
    // 必填字段
    changeForm.companyName = supplier.companyName || "";
    changeForm.companyId = supplier.companyId || "";
    changeForm.contactPerson = supplier.contactPerson || "";
    changeForm.contactPhone = supplier.contactPhone || "";
    changeForm.contactEmail = supplier.contactEmail || "";
    changeForm.category = supplier.category || "";
    changeForm.address = supplier.address || "";
    changeForm.businessRegistrationNumber = supplier.businessRegistrationNumber || "";
    changeForm.paymentTerms = supplier.paymentTerms || "";
    changeForm.paymentCurrency = supplier.paymentCurrency || "";
    changeForm.bankAccount = supplier.bankAccount || "";
    changeForm.region = supplier.region || "";

    // 公司详情
    changeForm.englishName = supplier.englishName || "";
    changeForm.chineseName = supplier.chineseName || "";
    changeForm.companyType = supplier.companyType || "";
    changeForm.companyTypeOther = supplier.companyTypeOther || "";
    changeForm.authorizedCapital = supplier.authorizedCapital || "";
    changeForm.issuedCapital = supplier.issuedCapital || "";
    changeForm.directors = supplier.directors || "";
    changeForm.owners = supplier.owners || "";

    // 注册信息
    changeForm.registeredOffice = supplier.registeredOffice || "";
    changeForm.businessAddress = supplier.businessAddress || "";
    changeForm.businessPhone = supplier.businessPhone || "";
    changeForm.faxNumber = supplier.faxNumber || "";

    // 额外联系人
    changeForm.salesContactName = supplier.salesContactName || "";
    changeForm.salesContactEmail = supplier.salesContactEmail || "";
    changeForm.salesContactPhone = supplier.salesContactPhone || "";
    changeForm.financeContactName = supplier.financeContactName || "";
    changeForm.financeContactEmail = supplier.financeContactEmail || "";
    changeForm.financeContactPhone = supplier.financeContactPhone || "";
    changeForm.customerServiceContactName = supplier.customerServiceContactName || "";
    changeForm.customerServiceContactEmail = supplier.customerServiceContactEmail || "";
    changeForm.customerServiceContactPhone = supplier.customerServiceContactPhone || "";

    // 业务信息
    changeForm.businessNature = supplier.businessNature || "";
    changeForm.operatingCurrency = supplier.operatingCurrency || "";
    changeForm.deliveryLocation = supplier.deliveryLocation || "";
    changeForm.productOrigin = supplier.productOrigin || "";
    changeForm.shipCode = supplier.shipCode || "";
    changeForm.productsForEci = supplier.productsForEci || "";
    changeForm.establishedYear = supplier.establishedYear || "";
    changeForm.employeeCount = supplier.employeeCount || "";
    changeForm.qualityCertifications = supplier.qualityCertifications || "";

    // 付款详情
    changeForm.invoiceType = supplier.invoiceType || "";
    changeForm.paymentTermsDays = supplier.paymentTermsDays || "";
    changeForm.paymentMethods = supplier.paymentMethods
      ? Array.isArray(supplier.paymentMethods)
        ? supplier.paymentMethods
        : []
      : [];
    changeForm.paymentMethodsOther = supplier.paymentMethodsOther || "";

    // 银行详情
    changeForm.bankName = supplier.bankName || "";
    changeForm.bankAddress = supplier.bankAddress || "";
    changeForm.swiftCode = supplier.swiftCode || "";

    // 其他
    changeForm.notes = supplier.notes || "";
    changeForm.serviceCategory = supplier.serviceCategory || "";

    // 根据完整度自动展开有缺失字段的面板
    activeCollapsePanels.value = ["basic"];
    if (supplier.complianceSummary) {
      const missingFields = supplier.complianceSummary.missingProfileFields || [];
      if (missingFields.length > 0) {
        // 如果有缺失字段，展开相关面板
        activeCollapsePanels.value.push(
          "company",
          "registration",
          "contacts",
          "business",
          "payment",
          "other",
        );
      }
    }
  } catch (error: any) {
    console.error("加载供应商数据失败:", error);
    notification.error("加载供应商数据失败");
  } finally {
    createFormLoading.value = false;
  }
};

// 创建新申请
const handleCreateRequest = async () => {
  await loadSupplierData();
  createDialogVisible.value = true;
};

// 提交变更申请
const handleSubmitChangeRequest = async () => {
  if (!authStore.user?.supplierId || !originalSupplierData.value) {
    notification.error("无法获取供应商信息");
    return;
  }

  // Build changes object with only modified fields
  const changes: Record<string, any> = {};
  const original = originalSupplierData.value;

  // 检查所有字段的变更
  Object.keys(changeForm).forEach((key) => {
    const newValue = changeForm[key as keyof typeof changeForm];
    const oldValue = original[key as keyof typeof original];

    // 处理数组类型（如 paymentMethods）
    if (Array.isArray(newValue)) {
      const oldArray = Array.isArray(oldValue) ? oldValue : [];
      if (JSON.stringify(newValue.sort()) !== JSON.stringify(oldArray.sort())) {
        changes[key] = newValue;
      }
    }
    // 处理普通字段
    else if (newValue !== (oldValue || "")) {
      // 只提交非空值或者从有值变为空值的情况
      if (newValue || oldValue) {
        changes[key] = newValue;
      }
    }
  });

  if (Object.keys(changes).length === 0) {
    notification.warning("您没有修改任何资料");
    return;
  }

  try {
    createFormLoading.value = true;
    const result = await submitChangeRequest(authStore.user.supplierId, changes);

    // 根据后端返回的 flow 显示不同的成功消息
    if (result.flow === "required") {
      notification.success({
        message: `检测到必填字段变更，已进入完整审批流程。申请编号: ${result.requestId}`,
        duration: 5000,
      });
    } else {
      notification.success({
        message: `仅修改可选字段，已提交采购员确认。申请编号: ${result.requestId}`,
        duration: 5000,
      });
    }

    createDialogVisible.value = false;
    await loadRequests();
  } catch (error: any) {
    console.error("提交变更申请失败:", error);
    notification.error(error.message || "提交失败");
  } finally {
    createFormLoading.value = false;
  }
};

// 关闭对话框
const handleCloseCreateDialog = () => {
  createDialogVisible.value = false;
};

onMounted(() => {
  loadRequests();
});




</script>

<style scoped>
.change-requests-view {
  padding: 24px;
  background: #f5f7fa;
  min-height: calc(100vh - 60px);
}




.content-wrapper {
  max-width: 1400px;
  margin: 0 auto;
}

.stats-row {
  margin-bottom: 24px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 16px;
  background: #fff;
  border-radius: 8px;
  padding: 20px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  transition: transform 0.3s;
}

.stat-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
}

.stat-icon {
  width: 56px;
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
  font-size: 28px;
}

.stat-icon.pending {
  background: #fdf6ec;
  color: #e6a23c;
}

.stat-icon.approved {
  background: #f0f9ff;
  color: #67c23a;
}

.stat-icon.rejected {
  background: #fef0f0;
  color: #f56c6c;
}

.stat-icon.total {
  background: #f4f4f5;
  color: #909399;
}

.stat-content {
  flex: 1;
}

.stat-value {
  font-size: 28px;
  font-weight: 600;
  color: #303133;
  line-height: 1;
  margin-bottom: 8px;
}

.stat-label {
  font-size: 14px;
  color: #909399;
}

.list-card {
  border-radius: 8px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 600;
}

.empty-state {
  padding: 40px 20px;
}

.details-content {
  padding: 0 8px;
}

:deep(.el-descriptions__label) {
  font-weight: 600;
}

:deep(.el-divider__text) {
  font-weight: 600;
  font-size: 15px;
}

.create-form-content {
  max-height: 650px;
  overflow-y: auto;
  padding: 0 8px;
}

.workflow-notice {
  margin: 8px 0 0 20px;
  padding: 0;
  line-height: 1.8;
}

.workflow-notice li {
  margin: 4px 0;
}

.change-form-collapse {
  border: none;
}

.change-form-collapse :deep(.el-collapse-item__header) {
  font-weight: 600;
  font-size: 15px;
  background-color: #f5f7fa;
  padding-left: 16px;
  border-radius: 4px;
  margin-bottom: 8px;
}

.change-form-collapse :deep(.el-collapse-item__wrap) {
  border: none;
}

.change-form-collapse :deep(.el-collapse-item__content) {
  padding: 16px 12px;
}

.create-form-content :deep(.el-divider__text) {
  color: #409eff;
  font-weight: 600;
}

.create-form-content :deep(.el-form-item) {
  margin-bottom: 18px;
}

.create-form-content :deep(.el-form-item__label) {
  color: #606266;
}

/* 必填字段标识样式 */
.create-form-content :deep(.el-form-item__label:has-text("⭐")) {
  font-weight: 600;
  color: #f56c6c;
}
</style>
