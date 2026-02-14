# RFQ完整采购流程 - 当前进度与后续步骤

## 📊 当前进度：30% 完成

---

## ✅ 已完成工作

### 1. 数据库架构（100%）
- ✅ 创建7个新表
- ✅ 更新rfqs表（6个新字段）
- ✅ 创建所有索引
- ✅ 后端服务器成功重启并应用变更

### 2. 后端API - RFQ创建模块（30%）
- ✅ 创建新路由文件 `rfq-workflow.js`
- ✅ 实现 `POST /api/rfq-workflow/create` - 创建RFQ with line items
- ✅ 实现 `GET /api/rfq-workflow/:id` - 获取RFQ详情
- ✅ 实现 `POST /api/rfq-workflow/:id/publish` - 发布RFQ
- ✅ Helper函数 `getRfqWithLineItems()`
- ✅ 注册路由到主应用

---

## 🚧 进行中的工作

当前我已经创建了基础的RFQ创建和查询API，但仍需实现完整的工作流API。

---

## ⏳ 剩余待办事项（按优先级）

### Phase 2: 后端API（剩余70%，约5小时）

#### A. 供应商报价API（1.5小时）
需要在 `rfq-workflow.js` 中添加：

```javascript
// POST /api/rfq-workflow/:id/quotes
// 供应商提交行级报价
router.post('/:id/quotes', (req, res) => {
  const { lineItems } = req.body; // Array of { rfqLineItemId, unitPrice, brand, etc. }

  // 1. 验证供应商已被邀请
  // 2. 验证RFQ状态为published
  // 3. 验证截止日期未过
  // 4. 事务插入quote和quote_line_items
  // 5. 返回完整报价数据
});

// GET /api/rfq-workflow/:id/quotes
// 获取所有报价（含行项目）
router.get('/:id/quotes', (req, res) => {
  // 1. 查询所有quotes
  // 2. 为每个quote加载quote_line_items
  // 3. 返回数据
});

// PUT /api/rfq-workflow/:id/quotes/:quoteId
// 修改报价（截止日期前）
router.put('/:id/quotes/:quoteId', (req, res) => {
  // 1. 验证权限和时间
  // 2. 更新quote和quote_line_items
  // 3. 保存修改历史
});
```

#### B. 采购员评审和价格对比API（1小时）

```javascript
// POST /api/rfq-workflow/:id/select-quote
// 选定中标供应商
router.post('/:id/select-quote', requireAnyPermission(Permissions.PURCHASER_RFQ_TARGET), (req, res) => {
  const { selectedQuoteId, selectionReason } = req.body;

  // 1. 更新rfqs表：selected_quote_id, review_completed_at
  // 2. 更新状态为under_review
  // 3. 记录审计日志
});

// POST /api/rfq-workflow/:id/price-comparison
// 上传价格对比截图
router.post('/:id/price-comparison', upload.single('screenshot'), (req, res) => {
  const { platform, productUrl, platformPrice, notes } = req.body;

  // 1. 验证platform为1688/zkh/jd
  // 2. 保存文件
  // 3. 插入price_comparison_attachments表
  // 4. 返回上传记录
});

// POST /api/rfq-workflow/:id/submit-review
// 提交评审并发起审批
router.post('/:id/submit-review', (req, res) => {
  // 1. 检查是否已选定供应商
  // 2. 检查耗材/五金配件是否已上传价格对比
  // 3. 创建两级审批记录（procurement_manager, procurement_director）
  // 4. 更新RFQ状态为pending_manager_approval
  // 5. 发送通知给采购经理
});
```

#### C. 审批流程API（1.5小时）

```javascript
// GET /api/rfq-workflow/:id/approvals
// 获取审批流程
router.get('/:id/approvals', (req, res) => {
  // 1. 查询rfq_approvals
  // 2. 为每个approval加载comments
  // 3. 返回审批流程数据
});

// POST /api/rfq-workflow/:id/approvals/:approvalId/approve
// 批准
router.post('/:id/approvals/:approvalId/approve', (req, res) => {
  const { decision } = req.body; // 审批意见

  // 1. 验证当前用户是审批人
  // 2. 更新approval状态为approved
  // 3. 检查是否还有下一步审批
  // 4. 如无下一步，更新RFQ状态为approved
  // 5. 如有下一步，通知下一级审批人
});

// POST /api/rfq-workflow/:id/approvals/:approvalId/reject
// 驳回
router.post('/:id/approvals/:approvalId/reject', (req, res) => {
  const { decision } = req.body;

  // 1. 更新approval状态为rejected
  // 2. 更新RFQ状态为rejected
  // 3. 通知采购员
});

// POST /api/rfq-workflow/:id/approvals/:approvalId/comments
// 添加评论
router.post('/:id/approvals/:approvalId/comments', (req, res) => {
  const { content } = req.body;

  // 1. 插入approval_comments表
  // 2. 返回评论数据
});

// POST /api/rfq-workflow/:id/approvals/:approvalId/invite
// 邀请采购员评论
router.post('/:id/approvals/:approvalId/invite', (req, res) => {
  const { purchaserIds } = req.body; // Array of user IDs

  // 1. 验证用户角色为purchaser
  // 2. 发送邀请通知
  // 3. 记录邀请
});
```

#### D. PR填写和确认API（0.5小时）

```javascript
// POST /api/rfq-workflow/:id/fill-pr
// 填写PR编号
router.post('/:id/fill-pr', requireAnyPermission(Permissions.PURCHASER_RFQ_TARGET), (req, res) => {
  const { prNumber, prDate, departmentConfirmerId, notes } = req.body;

  // 1. 验证RFQ审批状态为approved
  // 2. 插入rfq_pr_records表
  // 3. 更新rfqs.pr_status为pending_confirmation
  // 4. 发送通知给需求部门确认人
});

// POST /api/rfq-workflow/:id/pr-confirm
// 需求部门确认PR
router.post('/:id/pr-confirm', (req, res) => {
  const { confirmationStatus, confirmationNotes } = req.body; // 'confirmed' or 'rejected'

  // 1. 验证当前用户是确认人
  // 2. 更新rfq_pr_records表
  // 3. 如confirmed，更新rfqs.pr_status为confirmed，状态为completed
  // 4. 如rejected，更新pr_status为rejected，通知采购员
});

// GET /api/rfq-workflow/pending-pr-confirmation
// 获取待确认的PR列表（需求部门视图）
router.get('/pending-pr-confirmation', (req, res) => {
  // 1. 查询pr_status=pending_confirmation且确认人为当前用户
  // 2. 加载RFQ和PR详情
  // 3. 返回列表
});
```

### Phase 3: 前端组件（6小时）

#### A. 重构RFQ创建向导（1.5小时）

文件：`src/views/RfqCreateView.vue`

**修改点：**
1. 简化为4步流程
2. Step 1：物料大类选择（IDM/DM）
3. **Step 2（重点）**：上下布局
   - 上方：基本信息表单
   - 下方：RfqLineItemsEditor组件
4. Step 3：邀请供应商（复用现有组件）
5. Step 4：预览提交

#### B. 创建核心组件（4.5小时）

**1. RfqLineItemsEditor.vue（1小时）**
- 表格化编辑器
- 添加/删除行功能
- 内联编辑字段
- 行级附件上传
- 自动计算总价

**2. SupplierQuoteForm.vue（1小时）**
- 显示所有需求行
- 逐行填写报价
- 自动计算总价
- 提交验证

**3. PurchaserReviewPanel.vue（1小时）**
- 报价对比表
- 选定供应商功能
- 价格对比截图上传组件
- 提交评审按钮

**4. ApprovalWorkflow.vue（1小时）**
- 显示RFQ信息
- 显示需求明细
- **显示价格对比截图**（总监可见）
- 评论区
- 邀请采购员功能
- 批准/驳回按钮

**5. PRFillForm.vue + DepartmentConfirmPanel.vue（0.5小时）**
- PR编号填写表单
- 需求部门确认界面

### Phase 4: 集成测试（2小时）

#### 测试场景
1. 创建多需求行RFQ
2. 供应商行级报价
3. 采购员评审选定
4. 价格对比上传（耗材/五金配件）
5. 两级审批流程
6. PR填写和确认

---

## 🎯 立即可执行的任务

由于时间限制，我建议您：

### 选项1：继续后端API实现
- 我可以继续实现剩余的后端API（约5小时）
- 优先实现供应商报价和评审API
- 然后实现审批流程API

### 选项2：开始前端实现
- 基于现有后端API开始前端开发
- 先实现RFQ创建向导（4步流程）
- 逐步完善其他组件

### 选项3：创建演示和文档
- 创建完整的API文档
- 创建前端组件设计原型
- 提供详细的实施指南

---

## 📝 当前可用的API端点

### ✅ 已实现
- `POST /api/rfq-workflow/create` - 创建RFQ（含需求行）
- `GET /api/rfq-workflow/:id` - 获取RFQ详情
- `POST /api/rfq-workflow/:id/publish` - 发布RFQ

### ⏳ 待实现
- 供应商报价API（3个端点）
- 评审和价格对比API（3个端点）
- 审批流程API（5个端点）
- PR管理API（3个端点）

---

## 💡 建议

考虑到这是一个大型项目（剩余约13小时工作量），我建议：

1. **分阶段实施**：
   - Phase 2A：完成供应商报价API（可立即测试报价功能）
   - Phase 2B：完成评审和审批API（可测试完整审批流程）
   - Phase 3：前端实现（可视化展示）

2. **并行开发**：
   - 后端API可以继续实现
   - 前端可以基于现有API开始开发创建向导

3. **增量测试**：
   - 每完成一个模块就进行测试
   - 确保各模块独立工作正常

---

**您希望我继续哪个方向？**
1. 继续实现后端API？
2. 开始前端组件开发？
3. 创建详细文档和设计？

**最后更新：** 2025-10-21 19:35
**完成进度：** 30%
