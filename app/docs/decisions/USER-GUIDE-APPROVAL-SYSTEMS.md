# 两套审批系统使用指南

## 🔍 问题说明

系统中存在**两套独立的供应商审批流程**，它们服务于不同的业务场景：

---

## 📊 系统对比

### 1️⃣ 旧的供应商审批系统（供应商审批）

**访问路径**: `/approvals`
**菜单名称**: "供应商审批" (Supplier Approvals)
**适用角色**:
- ✅ 采购员 (purchaser)
- ✅ 采购经理 (procurement_manager)

**数据来源**: `suppliers` 表（已创建的供应商记录）

**审批状态**:
- `under_review` - 审核中
- `pending_purchaser` - 等待采购员审批
- `pending_purchase_manager` - 等待采购经理审批
- `pending_finance_manager` - 等待财务经理审批
- `approved` - 已批准
- `rejected` - 已拒绝

**示例供应商**:
- Shanghai Yirui Electronics Co., Ltd (状态: under_review)
- 这些是**已经在系统中创建的供应商记录**，需要通过审批流程变更状态

**使用场景**:
- 审批已存在的供应商记录的状态变更
- 处理供应商资质审核
- 管理供应商准入流程

---

### 2️⃣ 新的注册申请审批系统（新注册审批）

**访问路径**: `/registration-approvals`
**菜单名称**: "新注册审批" (New Registration Approvals)
**适用角色**:
- ✅ 质量经理 (quality_manager)
- ✅ 采购经理 (procurement_manager)
- ✅ 采购总监 (procurement_director)
- ✅ 财务总监 (finance_director)
- ✅ 财务会计 (finance_accountant)

**数据来源**: `supplier_registration_applications` 表（注册申请）

**审批状态** (6步审批流程):
1. `pending_quality_manager` - 等待质量经理审批
2. `pending_procurement_manager` - 等待采购经理审批
3. `pending_procurement_director` - 等待采购总监审批
4. `pending_finance_director` - 等待财务总监审批
5. `pending_accountant` - 等待财务会计审批
6. `pending_activation` - 等待供应商激活账户
7. `activated` - 已激活（流程完成）
8. `rejected` - 已拒绝

**示例申请**:
- 温州奥海电气有限公司 (状态: pending_quality_manager)
- 这些是**供应商提交的注册申请**，需要经过6步审批才能激活账户

**使用场景**:
- 审批新供应商的注册申请
- 6步审批工作流：质量→采购经理→采购总监→财务总监→财务会计→供应商激活
- 生成激活令牌，供应商自行激活账户

---

## 🎯 角色与菜单对应关系

### 采购员 (purchaser)
**看到的菜单**:
- ✅ "供应商审批" → `/approvals` (旧系统)
- ❌ 没有 "新注册审批" 菜单项

**原因**: 采购员不参与新的6步注册审批流程，只处理已有供应商的状态审批

---

### 采购经理 (procurement_manager)
**看到的菜单**:
- ✅ "新注册审批" → `/registration-approvals` (新系统)
- ✅ "供应商审批" → `/approvals` (旧系统)

**原因**: 采购经理同时参与两个流程：
1. 作为新注册流程的第2步审批人
2. 作为旧审批流程的审批人

---

### 质量经理、采购总监、财务总监、财务会计
**看到的菜单**:
- ✅ "新注册审批" → `/registration-approvals` (新系统)
- ❌ 没有 "供应商审批" 菜单项

**原因**: 这些角色只参与新的6步注册审批流程

---

## 🐛 用户反馈的问题分析

### 问题描述
用户看到 "Shanghai Yirui Electronics Co., Ltd" (状态: `under_review`) 出现在"注册审批"页面中。

### 根本原因
**用户登录的是采购员账户**，看到的是**旧的供应商审批系统** (`/approvals`)，而不是新的注册审批系统。

### 为什么会混淆？
1. 旧系统的页面标题显示 "注册审批" (Registration Approvals)
2. 但实际数据来自 `suppliers` 表，不是 `supplier_registration_applications` 表
3. 采购员角色**不会看到**"新注册审批"菜单项，因为他们不参与新的6步审批流程

---

## ✅ 解决方案

### 方案1: 理解系统设计（推荐）
**采购员用户**需要明白：
- 您看到的是**旧的供应商审批系统**，这是正常的
- "Shanghai Yirui Electronics Co., Ltd" 是一个已存在的供应商记录，需要通过旧流程审批
- **新的注册审批系统** (`/registration-approvals`) 只对质量经理等5个角色可见
- 您的角色不参与新的6步注册审批工作流

### 方案2: 修改页面标题（避免混淆）
可以修改 `ApprovalQueueView.vue` 的页面标题：
- 从 "注册审批" (Registration Approvals)
- 改为 "供应商审批" (Supplier Approvals)
- 这样可以明确区分两个系统

---

## 🧪 测试指南

### 测试旧系统（供应商审批）
```bash
# 使用采购员账户登录
用户名: purch001
密码: Purch#123

# 或
用户名: modric.zhang
密码: [询问实际密码]
```

**访问**: `/approvals`
**预期**: 看到 `suppliers` 表中的供应商记录（如 Shanghai Yirui Electronics）

---

### 测试新系统（新注册审批）
```bash
# 使用质量经理账户登录
用户名: qmgr001
密码: Quality#123
```

**访问**: `/registration-approvals`
**预期**: 看到 `supplier_registration_applications` 表中的注册申请（如温州奥海电气有限公司）

---

## 📝 数据库查询验证

### 查看旧系统数据
```sql
SELECT id, companyName, status, createdAt
FROM suppliers
WHERE status IN ('under_review', 'pending_purchaser', 'pending_purchase_manager')
ORDER BY createdAt DESC;
```

### 查看新系统数据
```sql
SELECT id, companyName, status, createdAt
FROM supplier_registration_applications
WHERE status LIKE 'pending_%'
ORDER BY createdAt DESC;
```

---

## 🎓 总结

| 特性 | 旧系统（供应商审批） | 新系统（新注册审批） |
|------|---------------------|---------------------|
| **路由** | `/approvals` | `/registration-approvals` |
| **菜单名** | 供应商审批 | 新注册审批 |
| **数据表** | `suppliers` | `supplier_registration_applications` |
| **角色** | 采购员、采购经理 | 质量经理、采购经理、采购总监、财务总监、财务会计 |
| **流程** | 3步旧审批流程 | 6步新审批流程 |
| **业务场景** | 已有供应商的状态审批 | 新供应商的注册申请审批 |

---

**创建日期**: 2025-11-04
**相关文档**:
- `SIDEBAR-FIX-SUMMARY.md` - 侧边栏修复详情
- `REGISTRATION-REDESIGN-SUMMARY.md` - 新注册流程设计文档
