# 侧边栏注册审批链接问题修复

## 🐛 问题描述

**截图反馈**：采购员的"注册审批"页面显示了不属于新注册流程的供应商记录（如 "Shanghai Yirui Electronics Co., Ltd"，状态为 `under_review`）。

## 🔍 问题根源

系统中存在**两种不同的审批流程**，但侧边栏配置混淆了它们：

### 1. 旧的供应商审批流程（`/approvals`）
- **页面**：`ApprovalQueueView.vue`
- **数据来源**：`suppliers` 表
- **状态**：`under_review`, `pending_purchaser`, `pending_purchase_manager` 等
- **用途**：审批已创建的供应商记录的状态变更
- **示例**：Shanghai Yirui Electronics Co., Ltd（状态：under_review）

### 2. 新的注册申请审批流程（`/registration-approvals`）
- **页面**：`RegistrationApprovalView.vue`（新实现）
- **数据来源**：`supplier_registration_applications` 表
- **状态**：`pending_quality_manager`, `pending_procurement_manager` 等（6步审批）
- **用途**：审批供应商的注册申请
- **示例**：温州奥海电气有限公司（状态：Awaiting profile details）

### 问题所在

在修复前，侧边栏的"注册审批"（`sidebar.approvals`）链接指向的是 **旧的供应商审批流程** (`/approvals`)，而不是新实现的注册申请审批流程 (`/registration-approvals`)。

这导致：
- 质量经理、采购经理等新审批流程的角色无法访问他们应该审批的**注册申请**
- 采购员看到的是旧流程中的**供应商记录**，而不是新的注册申请

## ✅ 解决方案

### 1. 更新侧边栏配置（`src/components/Sidebar.vue`）

为参与新注册审批流程的5个角色添加正确的菜单项：

```typescript
// 质量经理
if (role === "quality_manager") {
  return [
    { path: "/dashboard", label: "sidebar.home", icon: HomeFilled },
    { path: "/registration-approvals", label: "sidebar.registrationApprovals", icon: Checked }, // 新增
    { path: "/approval/upgrades", label: "sidebar.upgradeApproval", icon: TrendCharts },
    // ...
  ];
}

// 采购经理
if (role === "procurement_manager") {
  return [
    // ...
    { path: "/registration-approvals", label: "sidebar.registrationApprovals", icon: Checked }, // 新增
    // ...
    { path: "/approvals", label: "sidebar.supplierApprovals", icon: CircleCheck }, // 改名
    // ...
  ];
}

// 采购总监
if (role === "procurement_director") {
  return [
    // ...
    { path: "/registration-approvals", label: "sidebar.registrationApprovals", icon: Checked }, // 新增
    // ...
  ];
}

// 财务总监
if (role === "finance_director") {
  return [
    // ...
    { path: "/registration-approvals", label: "sidebar.registrationApprovals", icon: Checked }, // 新增
    // ...
  ];
}

// 财务会计
if (role === "finance_accountant") {
  return [
    // ...
    { path: "/registration-approvals", label: "sidebar.registrationApprovals", icon: Checked }, // 新增
    // ...
  ];
}

// 采购员（保留旧流程访问）
if (role === "purchaser") {
  const items: MenuItem[] = [
    // ...
    { path: "/approvals", label: "sidebar.supplierApprovals", icon: CircleCheck }, // 改名
    // ...
  ];
}
```

### 2. 添加多语言翻译

#### 中文（`src/locales/zh/sidebar.json`）
```json
{
  "approvals": "注册审批",
  "supplierApprovals": "供应商审批",        // 新增（旧流程）
  "registrationApprovals": "新注册审批",    // 新增（新流程）
}
```

#### 英文（`src/locales/en/sidebar.json`）
```json
{
  "approvals": "Registration Approvals",
  "supplierApprovals": "Supplier Approvals",           // 新增（旧流程）
  "registrationApprovals": "New Registration Approvals", // 新增（新流程）
}
```

## 📊 修复后的菜单结构

### 新注册审批流程角色（显示"新注册审批"）
- ✅ 质量经理（quality_manager）
- ✅ 采购经理（procurement_manager）- **同时显示两个菜单**
- ✅ 采购总监（procurement_director）
- ✅ 财务总监（finance_director）
- ✅ 财务会计（finance_accountant）

### 旧供应商审批流程角色（显示"供应商审批"）
- ✅ 采购员（purchaser）
- ✅ 采购经理（procurement_manager）- **同时显示两个菜单**

## 🎯 预期结果

修复后：
1. **质量经理**访问"新注册审批" → 看到 `supplier_registration_applications` 表中状态为 `pending_quality_manager` 的申请
2. **采购员**访问"供应商审批" → 看到 `suppliers` 表中状态为 `under_review` 等的供应商记录（如 Shanghai Yirui）
3. **采购经理**可以访问两个不同的页面处理不同类型的审批

## 🔧 如何验证

1. 以质量经理身份登录（qmgr001 / Quality#123）
2. 侧边栏应该显示"新注册审批"菜单项
3. 点击后应该看到新的注册申请审批页面（只显示注册申请，不显示 Shanghai Yirui 等供应商）

---

**修复时间**: 2025-11-04
**影响文件**:
- `src/components/Sidebar.vue` ✅
- `src/locales/zh/sidebar.json` ✅
- `src/locales/en/sidebar.json` ✅
- `src/locales/th/sidebar.json` ✅
- `src/locales/zh/approvalQueue.json` ✅ (页面标题)
- `src/locales/en/approvalQueue.json` ✅ (页面标题)
- `src/locales/th/approvalQueue.json` ✅ (页面标题)

**额外修复**:
- 更新旧系统页面标题从"注册审批"改为"供应商审批"，避免与新系统混淆

**文档**:
- `FINAL-FIX-SUMMARY.md` - 完整技术修复说明
- `USER-GUIDE-APPROVAL-SYSTEMS.md` - 两套系统详细对比
- `VISUAL-CHANGES-GUIDE.md` - 界面变化对比
- `QUICK-FIX-REFERENCE.md` - 快速参考卡
