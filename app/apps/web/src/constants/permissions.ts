/**
 * 权限常量定义
 *
 * 用于避免权限字符串拼写错误
 * 所有权限检查都应使用这些常量
 *
 * @example
 * import { PERMISSIONS } from '@/constants/permissions'
 *
 * // 在模板中使用
 * <el-button v-permission="PERMISSIONS.SUPPLIER.EDIT">编辑</el-button>
 *
 * // 在代码中使用
 * const { hasPermission } = usePermission()
 * if (hasPermission(PERMISSIONS.SUPPLIER.EDIT)) { ... }
 */

export const PERMISSIONS = {
  /** 供应商相关权限 */
  SUPPLIER: {
    /** 查看供应商列表 */
    VIEW: "supplier.view",
    /** 查看供应商详情 */
    VIEW_DETAIL: "supplier.view_detail",
    /** 创建供应商 */
    CREATE: "supplier.create",
    /** 编辑供应商信息 */
    EDIT: "supplier.edit",
    /** 删除供应商 */
    DELETE: "supplier.delete",
    /** 审批供应商注册 */
    APPROVE: "supplier.approve",
    /** 导出供应商数据 */
    EXPORT: "supplier.export",
    /** 导入供应商数据 */
    IMPORT: "supplier.import",
  },

  /** RFQ（询价）相关权限 */
  RFQ: {
    /** 查看 RFQ 列表 */
    VIEW: "rfq.view",
    /** 查看 RFQ 详情 */
    VIEW_DETAIL: "rfq.view_detail",
    /** 创建 RFQ */
    CREATE: "rfq.create",
    /** 编辑 RFQ */
    EDIT: "rfq.edit",
    /** 删除 RFQ */
    DELETE: "rfq.delete",
    /** 发送 RFQ 给供应商 */
    SEND: "rfq.send",
    /** 审批 RFQ */
    APPROVE: "rfq.approve",
    /** 关闭 RFQ */
    CLOSE: "rfq.close",
    /** 查看所有 RFQ（跨部门）*/
    VIEW_ALL: "rfq.view_all",
    /** 导出 RFQ 数据 */
    EXPORT: "rfq.export",
  },

  /** 采购订单相关权限 */
  PURCHASE_ORDER: {
    /** 查看采购订单 */
    VIEW: "po.view",
    /** 创建采购订单 */
    CREATE: "po.create",
    /** 编辑采购订单 */
    EDIT: "po.edit",
    /** 删除采购订单 */
    DELETE: "po.delete",
    /** 审批采购订单 */
    APPROVE: "po.approve",
    /** 导出采购订单 */
    EXPORT: "po.export",
  },

  /** 审批相关权限 */
  APPROVAL: {
    /** 查看待审批列表 */
    VIEW_QUEUE: "approval.view_queue",
    /** 审批通过 */
    APPROVE: "approval.approve",
    /** 审批拒绝 */
    REJECT: "approval.reject",
    /** 请求更改 */
    REQUEST_CHANGES: "approval.request_changes",
    /** 查看审批历史 */
    VIEW_HISTORY: "approval.view_history",
  },

  /** 供应商变更相关权限 */
  SUPPLIER_CHANGE: {
    /** 提交变更请求 */
    SUBMIT: "supplier_change.submit",
    /** 查看变更请求 */
    VIEW: "supplier_change.view",
    /** 审批变更请求 */
    APPROVE: "supplier_change.approve",
  },

  /** 文件上传相关权限 */
  FILE_UPLOAD: {
    /** 上传文件 */
    UPLOAD: "file.upload",
    /** 查看文件 */
    VIEW: "file.view",
    /** 下载文件 */
    DOWNLOAD: "file.download",
    /** 删除文件 */
    DELETE: "file.delete",
    /** 审批文件 */
    APPROVE: "file.approve",
  },

  /** 用户管理相关权限 */
  USER: {
    /** 查看用户列表 */
    VIEW: "user.view",
    /** 创建用户 */
    CREATE: "user.create",
    /** 编辑用户 */
    EDIT: "user.edit",
    /** 删除用户 */
    DELETE: "user.delete",
    /** 重置密码 */
    RESET_PASSWORD: "user.reset_password",
  },

  /** 角色管理相关权限 */
  ROLE: {
    /** 查看角色列表 */
    VIEW: "role.view",
    /** 创建角色 */
    CREATE: "role.create",
    /** 编辑角色 */
    EDIT: "role.edit",
    /** 删除角色 */
    DELETE: "role.delete",
    /** 分配权限 */
    ASSIGN_PERMISSIONS: "role.assign_permissions",
  },

  /** 审计日志相关权限 */
  AUDIT: {
    /** 查看审计日志 */
    VIEW: "audit.view",
    /** 导出审计日志 */
    EXPORT: "audit.export",
  },

  /** 系统设置相关权限 */
  SYSTEM: {
    /** 查看系统设置 */
    VIEW_SETTINGS: "system.view_settings",
    /** 编辑系统设置 */
    EDIT_SETTINGS: "system.edit_settings",
    /** 查看系统状态 */
    VIEW_STATUS: "system.view_status",
    /** 数据备份 */
    BACKUP: "system.backup",
    /** 数据恢复 */
    RESTORE: "system.restore",
  },

  /** 报表相关权限 */
  REPORT: {
    /** 查看报表 */
    VIEW: "report.view",
    /** 导出报表 */
    EXPORT: "report.export",
    /** 创建自定义报表 */
    CREATE_CUSTOM: "report.create_custom",
  },

  /** 财务相关权限 */
  FINANCE: {
    /** 查看财务数据 */
    VIEW: "finance.view",
    /** 查看发票 */
    VIEW_INVOICE: "finance.view_invoice",
    /** 创建发票 */
    CREATE_INVOICE: "finance.create_invoice",
    /** 审批发票 */
    APPROVE_INVOICE: "finance.approve_invoice",
    /** 对账 */
    RECONCILE: "finance.reconcile",
  },

  /** 供应商注册审批权限 */
  REGISTRATION: {
    /** 查看注册审批状态 */
    VIEW_STATUS: "registration.status.view",
    /** 采购员审批 */
    APPROVE_PURCHASER: "registration.approve.purchaser",
    /** SQE审批 */
    APPROVE_QUALITY: "registration.approve.quality",
    /** 采购经理审批 */
    APPROVE_MANAGER: "registration.approve.manager",
    /** 采购总监审批 */
    APPROVE_DIRECTOR: "registration.approve.director",
    /** 财务总监审批 */
    APPROVE_FINANCE: "registration.approve.finance",
    /** 财务会计审批（绑定供应商代码） */
    APPROVE_ACCOUNTANT: "registration.approve.accountant",
    /** 财务出纳审批 */
    APPROVE_CASHIER: "registration.approve.cashier",
  },

  /** 黑白名单相关权限 */
  LIST_MANAGEMENT: {
    /** 查看黑白名单 */
    VIEW: "list.view",
    /** 添加到白名单 */
    ADD_WHITELIST: "list.add_whitelist",
    /** 添加到黑名单 */
    ADD_BLACKLIST: "list.add_blacklist",
    /** 移除名单 */
    REMOVE: "list.remove",
  },

  /** 采购组相关权限 */
  PURCHASING_GROUP: {
    /** 查看采购组 */
    VIEW: "purchasing_group.view",
    /** 创建采购组 */
    CREATE: "purchasing_group.create",
    /** 编辑采购组 */
    EDIT: "purchasing_group.edit",
    /** 删除采购组 */
    DELETE: "purchasing_group.delete",
    /** 管理成员 */
    MANAGE_MEMBERS: "purchasing_group.manage_members",
  },

  /** 组织单元相关权限 */
  ORG_UNIT: {
    /** 查看组织单元 */
    VIEW: "org_unit.view",
    /** 创建组织单元 */
    CREATE: "org_unit.create",
    /** 编辑组织单元 */
    EDIT: "org_unit.edit",
    /** 删除组织单元 */
    DELETE: "org_unit.delete",
  },
} as const;

/**
 * 权限组（用于批量权限检查）
 */
export const PERMISSION_GROUPS = {
  /** 供应商管理员（所有供应商相关权限）*/
  SUPPLIER_ADMIN: Object.values(PERMISSIONS.SUPPLIER),

  /** RFQ 管理员（所有 RFQ 相关权限）*/
  RFQ_ADMIN: Object.values(PERMISSIONS.RFQ),

  /** 审批人员（所有审批相关权限）*/
  APPROVER: [...Object.values(PERMISSIONS.APPROVAL), PERMISSIONS.SUPPLIER.APPROVE, PERMISSIONS.RFQ.APPROVE],

  /** 财务人员（所有财务相关权限）*/
  FINANCE_USER: Object.values(PERMISSIONS.FINANCE),

  /** 系统管理员（所有系统相关权限）*/
  SYSTEM_ADMIN: [
    ...Object.values(PERMISSIONS.USER),
    ...Object.values(PERMISSIONS.ROLE),
    ...Object.values(PERMISSIONS.AUDIT),
    ...Object.values(PERMISSIONS.SYSTEM),
  ],
} as const;

/**
 * 获取权限的可读名称
 */
export function getPermissionLabel(permission: string): string {
  const labels: Record<string, string> = {
    // 供应商
    "supplier.view": "查看供应商",
    "supplier.view_detail": "查看供应商详情",
    "supplier.create": "创建供应商",
    "supplier.edit": "编辑供应商",
    "supplier.delete": "删除供应商",
    "supplier.approve": "审批供应商",
    "supplier.export": "导出供应商",
    "supplier.import": "导入供应商",

    // RFQ
    "rfq.view": "查看询价单",
    "rfq.view_detail": "查看询价单详情",
    "rfq.create": "创建询价单",
    "rfq.edit": "编辑询价单",
    "rfq.delete": "删除询价单",
    "rfq.send": "发送询价单",
    "rfq.approve": "审批询价单",
    "rfq.close": "关闭询价单",
    "rfq.view_all": "查看所有询价单",
    "rfq.export": "导出询价单",

    // ... 可以继续添加其他权限的标签
  };

  return labels[permission] || permission;
}

/**
 * 类型辅助：提取所有权限值的联合类型
 */
export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS][keyof (typeof PERMISSIONS)[keyof typeof PERMISSIONS]];
