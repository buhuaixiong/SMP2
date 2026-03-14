import { computed } from "vue";
import type { SupplierDocument } from "@/types";

// ==================== 文档类型常量定义 ====================

export interface DocumentTypeConfig {
  type: string;
  label: string;
  required: boolean;
}

export const REQUIRED_DOCUMENT_TYPES: DocumentTypeConfig[] = [
  { type: "quality_compensation_agreement", label: "质量赔偿协议", required: true },
  { type: "packaging_transport_agreement", label: "材料包装运输协议", required: true },
  { type: "quality_assurance_agreement", label: "质量保证协议", required: true },
  { type: "quality_kpi_annual_target", label: "质量KPI年度目标", required: true },
  { type: "supplier_manual_template", label: "供应商手册模板", required: true },
];

export const ALL_DOCUMENT_TYPES: Record<string, string> = {
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

// ==================== 邮件模板常量 ====================

export interface ReminderTemplate {
  subject: string;
  body: string;
}

export const PROFILE_REMINDER_TEMPLATE = (companyName: string, missingFields: string[], missingDocs: string[]): string => {
  const sections: string[] = [];
  if (missingFields.length) {
    sections.push(`缺失信息：\n${missingFields.map((item) => `- ${item}`).join("\n")}`);
  }
  if (missingDocs.length) {
    sections.push(`缺失文件：\n${missingDocs.map((item) => `- ${item}`).join("\n")}`);
  }
  return [
    `您好 ${companyName}：`,
    "",
    "贵司资料尚未完善，请尽快补充以下内容：",
    "",
    sections.join("\n\n"),
    "",
    "如有疑问请联系采购团队，谢谢！",
  ].join("\n");
};

export const EXPIRY_REMINDER_TEMPLATE = (companyName: string, expiringDocs: Array<{ name: string; expiryDate: string; status: string; filename?: string }>): string => {
  const docLines = expiringDocs.map((doc) => {
    const filename = doc.filename ? `，文件：${doc.filename}` : "";
    return `- ${doc.name}（${doc.status}，到期日：${doc.expiryDate}${filename}）`;
  });
  return [
    `您好 ${companyName}：`,
    "",
    "以下文件已过期或即将过期，请尽快补充或更新：",
    "",
    docLines.join("\n"),
    "",
    "如有疑问请联系采购团队，谢谢！",
  ].join("\n");
};

// ==================== 必填字段常量 ====================

export const REQUIRED_PROFILE_FIELDS = [
  { key: "companyName", label: "公司名称" },
  { key: "companyId", label: "公司ID / 注册号" },
  { key: "contactPerson", label: "联系人" },
  { key: "contactPhone", label: "联系电话" },
  { key: "contactEmail", label: "联系邮箱" },
  { key: "category", label: "业务类别" },
  { key: "address", label: "通讯地址" },
  { key: "businessRegistrationNumber", label: "营业执照编号" },
  { key: "paymentTerms", label: "付款条款" },
  { key: "paymentCurrency", label: "付款币种" },
  { key: "bankAccount", label: "银行账号" },
  { key: "region", label: "所属地区" },
] as const;

// ==================== Composable ====================

export function useSupplierDocuments() {
  // 获取文档类型显示名称
  const getDocTypeName = (docType: string): string => {
    return ALL_DOCUMENT_TYPES[docType] || docType;
  };

  // 检查是否有指定类型的文档
  const hasDocument = (documents: SupplierDocument[] | undefined, docType: string): boolean => {
    if (!documents) return false;
    return documents.some((doc) => doc.docType === docType);
  };

  // 获取指定类型的文档
  const getDocument = (documents: SupplierDocument[] | undefined, docType: string): SupplierDocument | undefined => {
    if (!documents) return undefined;
    return documents.find((doc) => doc.docType === docType);
  };

  // 获取所有必填文档状态
  const requiredDocumentsStatus = computed(() => {
    return REQUIRED_DOCUMENT_TYPES.map((config) => ({
      ...config,
      uploaded: false, // 会在使用时通过参数传入
    }));
  });

  // 检查文档是否过期
  const isExpired = (doc: SupplierDocument): boolean => {
    if (!doc.expiresAt) return false;
    return new Date(doc.expiresAt) < new Date();
  };

  // 检查文档是否即将过期（30天内）
  const isExpiringSoon = (doc: SupplierDocument): boolean => {
    if (!doc.expiresAt) return false;
    const expiryDate = new Date(doc.expiresAt);
    const today = new Date();
    const daysUntilExpiry = Math.floor((expiryDate.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
    return daysUntilExpiry > 0 && daysUntilExpiry <= 30;
  };

  // 获取过期或即将过期的文档
  const getExpiringDocuments = (documents: SupplierDocument[] | undefined) => {
    if (!documents) return [];
    return documents.filter((doc) => isExpired(doc) || isExpiringSoon(doc));
  };

  // 格式化日期
  const formatDate = (date: string | null | undefined): string => {
    if (!date) return "N/A";
    const d = new Date(date);
    return d.toLocaleDateString("zh-CN", { year: "numeric", month: "2-digit", day: "2-digit" });
  };

  // 检查供应商资料是否完整
  const checkProfileCompleteness = (
    supplier: Record<string, unknown>,
    requiredFields: readonly { key: string; label: string }[],
  ): string[] => {
    const missingFields: string[] = [];
    requiredFields.forEach((field) => {
      if (!supplier[field.key]) {
        missingFields.push(field.label);
      }
    });
    return missingFields;
  };

  // 获取缺失的必填文档类型
  const getMissingRequiredDocs = (documents: SupplierDocument[] | undefined): string[] => {
    if (!documents) return REQUIRED_DOCUMENT_TYPES.map((d) => d.type);
    return REQUIRED_DOCUMENT_TYPES.filter((config) => !hasDocument(documents, config.type)).map((d) => d.type);
  };

  // 获取缺失的必填文档名称
  const getMissingRequiredDocLabels = (documents: SupplierDocument[] | undefined): string[] => {
    const missingTypes = getMissingRequiredDocs(documents);
    return missingTypes.map((type) => {
      const config = REQUIRED_DOCUMENT_TYPES.find((d) => d.type === type);
      return config?.label || type;
    });
  };

  return {
    // 常量
    REQUIRED_DOCUMENT_TYPES,
    ALL_DOCUMENT_TYPES,
    REQUIRED_PROFILE_FIELDS,
    // 方法
    getDocTypeName,
    hasDocument,
    getDocument,
    isExpired,
    isExpiringSoon,
    getExpiringDocuments,
    formatDate,
    checkProfileCompleteness,
    getMissingRequiredDocs,
    getMissingRequiredDocLabels,
  };
}

// ==================== 邮件发送工具 ====================

export function useSupplierEmailReminder() {
  // 打开邮件客户端
  const openMailClient = (to: string, subject: string, body: string): boolean => {
    if (typeof window === "undefined") return false;
    const mailto = `mailto:${to}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
    window.location.href = mailto;
    return true;
  };

  // 发送资料完善提醒
  const sendProfileReminder = (
    supplier: { companyName: string; contactPerson: string; contactEmail?: string | null },
    missingFields: string[],
    missingDocs: string[],
    onOpenMail?: () => void,
  ): boolean => {
    if (!supplier.contactEmail) {
      return false;
    }

    if (missingFields.length === 0 && missingDocs.length === 0) {
      return false;
    }

    const recipientName = supplier.contactPerson || supplier.companyName || "供应商伙伴";
    const subject = `供应商资料完善提醒 - ${supplier.companyName || "供应商"}`;
    const body = PROFILE_REMINDER_TEMPLATE(recipientName, missingFields, missingDocs);

    const opened = openMailClient(supplier.contactEmail, subject, body);
    if (opened && onOpenMail) {
      onOpenMail();
    }
    return opened;
  };

  // 发送文件到期提醒
  const sendExpiryReminder = (
    supplier: { companyName: string; contactPerson: string; contactEmail?: string | null },
    expiringDocs: Array<{ name: string; expiryDate: string; status: string; filename?: string }>,
    onOpenMail?: () => void,
  ): boolean => {
    if (!supplier.contactEmail) {
      return false;
    }

    if (expiringDocs.length === 0) {
      return false;
    }

    const recipientName = supplier.contactPerson || supplier.companyName || "供应商伙伴";
    const subject = `供应商文件到期提醒 - ${supplier.companyName || "供应商"}`;
    const body = EXPIRY_REMINDER_TEMPLATE(recipientName, expiringDocs);

    const opened = openMailClient(supplier.contactEmail, subject, body);
    if (opened && onOpenMail) {
      onOpenMail();
    }
    return opened;
  };

  return {
    openMailClient,
    sendProfileReminder,
    sendExpiryReminder,
  };
}
