<template>
  <div class="documents-list">
    <!-- Required Documents Status -->
    <div class="doc-requirement">
      <h4>必备文件</h4>
      <div class="required-docs">
        <div
          v-for="docType in requiredDocumentTypes"
          :key="docType.type"
          class="doc-status"
          :class="hasDocument(docType.type) ? 'uploaded' : 'missing'"
        >
          <div class="doc-name">
            <el-icon v-if="hasDocument(docType.type)">
              <CircleCheckFilled />
            </el-icon>
            <el-icon v-else>
              <CircleCloseFilled />
            </el-icon>
            <span>{{ docType.label }}</span>
          </div>
          <div v-if="getDocument(docType.type)" class="doc-info">
            <span class="validity">
              有效期：{{ formatDate(getDocument(docType.type)?.validFrom) }} -
              {{ formatDate(getDocument(docType.type)?.expiresAt) }}
            </span>
          </div>
          <div v-else class="doc-info">
            <span class="missing-text">未上传</span>
          </div>
        </div>
      </div>
    </div>

    <!-- All Uploaded Documents -->
    <div v-if="documents && documents.length > 0" class="all-documents">
      <h4>已上传全部文件</h4>
      <div class="doc-list">
        <div v-for="doc in documents" :key="doc.id" class="doc-item">
          <div class="doc-header">
            <span class="doc-type">{{ doc.docType ? docTypeName(doc.docType) : "-" }}</span>
            <span class="doc-filename">{{ doc.originalName }}</span>
          </div>
          <div class="doc-dates">
            <span v-if="doc.validFrom" class="valid-from">有效日期: {{ formatDate(doc.validFrom) }}</span>
            <span
              v-if="doc.expiresAt"
              class="expires-at"
              :class="{ expired: checkExpired(doc), expiring: checkExpiringSoon(doc) }"
            >
              截止日期: {{ formatDate(doc.expiresAt) }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { CircleCheckFilled, CircleCloseFilled } from "@element-plus/icons-vue";
import type { SupplierDocument } from "@/types";
import { REQUIRED_DOCUMENT_TYPES } from "@/composables/useSupplierDocuments";

interface Props {
  documents?: SupplierDocument[];
}

const props = defineProps<Props>();

const requiredDocumentTypes = computed(() => REQUIRED_DOCUMENT_TYPES);

// 文档类型名称映射
const docTypeMap: Record<string, string> = {
  business_license: "营业执照",
  tax_certificate: "税务登记证",
  bank_information: "银行资料",
  quality_certificate: "质量认证证书",
  quality_compensation_agreement: "质量赔偿协议",
  packaging_transport_agreement: "材料包装运输协议",
  quality_assurance_agreement: "质量保证协议",
  quality_kpi_annual_target: "质量KPI年度目标",
  supplier_manual_template: "供应商手册模板",
  other: "其他文件",
};

const docTypeName = (docType: string): string => {
  return docTypeMap[docType] || docType;
};

// 检查是否有指定类型的文档
const hasDocument = (docType: string): boolean => {
  if (!props.documents) return false;
  return props.documents.some((doc) => doc.docType === docType);
};

// 获取指定类型的文档
const getDocument = (docType: string): SupplierDocument | undefined => {
  if (!props.documents) return undefined;
  return props.documents.find((doc) => doc.docType === docType);
};

// 格式化日期
const formatDate = (date: string | null | undefined): string => {
  if (!date) return "N/A";
  const d = new Date(date);
  return d.toLocaleDateString("zh-CN", { year: "numeric", month: "2-digit", day: "2-digit" });
};

// 检查文档是否过期
const checkExpired = (doc: SupplierDocument): boolean => {
  if (!doc.expiresAt) return false;
  return new Date(doc.expiresAt) < new Date();
};

// 检查文档是否即将过期（30天内）
const checkExpiringSoon = (doc: SupplierDocument): boolean => {
  if (!doc.expiresAt) return false;
  const expiryDate = new Date(doc.expiresAt);
  const today = new Date();
  const daysUntilExpiry = Math.floor((expiryDate.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
  return daysUntilExpiry > 0 && daysUntilExpiry <= 30;
};
</script>

<style scoped>
.documents-list {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.doc-requirement h4 {
  font-size: 1rem;
  font-weight: 600;
  color: #374151;
  margin: 0 0 0.75rem 0;
}

.required-docs {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.doc-status {
  padding: 0.75rem;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
}

.doc-status.uploaded {
  background: #f0fdf4;
  border-color: #86efac;
}

.doc-status.missing {
  background: #fef2f2;
  border-color: #fca5a5;
}

.doc-name {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  color: #111827;
  margin-bottom: 0.5rem;
}

.doc-name .el-icon {
  font-size: 1.25rem;
}

.doc-status.uploaded .doc-name .el-icon {
  color: #22c55e;
}

.doc-status.missing .doc-name .el-icon {
  color: #ef4444;
}

.doc-info {
  font-size: 0.85rem;
  color: #6b7280;
}

.doc-info .validity {
  color: #059669;
}

.doc-info .missing-text {
  color: #dc2626;
}

.all-documents h4 {
  font-size: 1rem;
  font-weight: 600;
  color: #374151;
  margin: 0 0 0.75rem 0;
}

.doc-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.doc-item {
  padding: 0.75rem;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  background: #f9fafb;
}

.doc-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.doc-type {
  display: inline-block;
  padding: 0.25rem 0.5rem;
  background: #dbeafe;
  color: #1e40af;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
}

.doc-filename {
  font-size: 0.9rem;
  color: #111827;
  font-weight: 500;
}

.doc-dates {
  display: flex;
  gap: 1rem;
  font-size: 0.85rem;
}

.valid-from {
  color: #059669;
}

.expires-at {
  color: #6b7280;
}

.expires-at.expired {
  color: #dc2626;
  font-weight: 600;
}

.expires-at.expiring {
  color: #f59e0b;
  font-weight: 600;
}
</style>
