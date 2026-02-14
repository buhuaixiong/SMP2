# RFQ 完整采购流程设计方案

## 📋 业务流程概述

### 流程阶段
```
1. 采购员创建RFQ（5步向导）
   ├─ Step 1: 选择物料大类（DM/IDM）
   ├─ Step 2: 填写需求行（多行，可增删）
   ├─ Step 3: 填写询价单基本信息
   ├─ Step 4: 邀请供应商
   └─ Step 5: 预览提交

2. 供应商报价（3天内）
   └─ 对每个需求行分别报价

3. 采购员评审选定供应商
   ├─ 查看所有报价
   ├─ 耗材/五金配件需上传价格对比截图（1688/震坤行/京东）
   ├─ 可粘贴对比链接
   └─ 选定中标供应商

4. 采购经理审批
   ├─ 查看RFQ、报价、价格对比
   ├─ 可邀请采购员评论
   └─ 批准/驳回

5. 采购总监审批
   ├─ 查看完整信息
   ├─ 可邀请采购员评论
   └─ 批准/驳回

6. 生成PR（采购请求单）
   └─ 自动生成PR单据
```

---

## 🗄️ 数据库架构设计

### 1. RFQ 需求行表（rfq_line_items）

```sql
CREATE TABLE rfq_line_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  rfq_id INTEGER NOT NULL,
  line_number INTEGER NOT NULL,           -- 行号
  material_category TEXT NOT NULL,        -- 物料类别（equipment/consumables/hardware/fixtures/molds/blades）
  brand TEXT,                             -- 品牌
  item_name TEXT NOT NULL,                -- 物料名称
  specifications TEXT,                     -- 规格参数
  quantity REAL NOT NULL,                  -- 数量
  unit TEXT NOT NULL,                      -- 单位
  estimated_unit_price REAL,               -- 预估单价
  currency TEXT DEFAULT 'CNY',             -- 币种
  parameters TEXT,                         -- 详细参数（JSON）
  notes TEXT,                              -- 备注
  created_at TEXT DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (rfq_id) REFERENCES rfqs(id) ON DELETE CASCADE
);

CREATE INDEX idx_rfq_line_items_rfq_id ON rfq_line_items(rfq_id);
```

### 2. RFQ 附件表（rfq_attachments）

```sql
CREATE TABLE rfq_attachments (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  rfq_id INTEGER NOT NULL,
  line_item_id INTEGER,                   -- 关联需求行（可选，NULL表示RFQ级别附件）
  file_name TEXT NOT NULL,                 -- 文件名
  file_path TEXT NOT NULL,                 -- 文件路径
  file_size INTEGER,                       -- 文件大小（字节）
  file_type TEXT,                          -- MIME类型
  uploaded_by INTEGER NOT NULL,            -- 上传人
  uploaded_at TEXT DEFAULT CURRENT_TIMESTAMP,
  description TEXT,                        -- 文件描述
  FOREIGN KEY (rfq_id) REFERENCES rfqs(id) ON DELETE CASCADE,
  FOREIGN KEY (line_item_id) REFERENCES rfq_line_items(id) ON DELETE CASCADE,
  FOREIGN KEY (uploaded_by) REFERENCES users(id)
);

CREATE INDEX idx_rfq_attachments_rfq_id ON rfq_attachments(rfq_id);
CREATE INDEX idx_rfq_attachments_line_item_id ON rfq_attachments(line_item_id);
```

### 3. 供应商报价行表（quote_line_items）

```sql
CREATE TABLE quote_line_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  quote_id INTEGER NOT NULL,
  rfq_line_item_id INTEGER NOT NULL,      -- 关联RFQ需求行
  unit_price REAL NOT NULL,                -- 单价
  total_price REAL NOT NULL,               -- 总价
  brand TEXT,                              -- 品牌
  tax_status TEXT DEFAULT 'inclusive',     -- 含税/不含税
  delivery_date TEXT,                      -- 交货期
  parameters TEXT,                         -- 参数说明（JSON）
  notes TEXT,                              -- 备注
  FOREIGN KEY (quote_id) REFERENCES quotes(id) ON DELETE CASCADE,
  FOREIGN KEY (rfq_line_item_id) REFERENCES rfq_line_items(id) ON DELETE CASCADE
);

CREATE INDEX idx_quote_line_items_quote_id ON quote_line_items(quote_id);
CREATE INDEX idx_quote_line_items_rfq_line_item_id ON quote_line_items(rfq_line_item_id);
```

### 4. 价格对比截图表（price_comparison_attachments）

```sql
CREATE TABLE price_comparison_attachments (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  rfq_id INTEGER NOT NULL,
  line_item_id INTEGER,                   -- 关联需求行
  platform TEXT NOT NULL,                 -- 平台（1688/jd/zkh）
  file_name TEXT NOT NULL,                 -- 截图文件名
  file_path TEXT NOT NULL,                 -- 文件路径
  product_url TEXT,                        -- 产品链接
  platform_price REAL,                     -- 平台价格
  uploaded_by INTEGER NOT NULL,            -- 上传人
  uploaded_at TEXT DEFAULT CURRENT_TIMESTAMP,
  notes TEXT,
  FOREIGN KEY (rfq_id) REFERENCES rfqs(id) ON DELETE CASCADE,
  FOREIGN KEY (line_item_id) REFERENCES rfq_line_items(id) ON DELETE SET NULL,
  FOREIGN KEY (uploaded_by) REFERENCES users(id)
);

CREATE INDEX idx_price_comparison_rfq_id ON price_comparison_attachments(rfq_id);
```

### 5. RFQ 审批流程表（rfq_approvals）

```sql
CREATE TABLE rfq_approvals (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  rfq_id INTEGER NOT NULL,
  step_order INTEGER NOT NULL,            -- 审批顺序（1=采购经理，2=采购总监）
  step_name TEXT NOT NULL,                -- 步骤名称
  approver_role TEXT NOT NULL,            -- 审批人角色
  approver_id INTEGER,                    -- 实际审批人ID
  status TEXT DEFAULT 'pending',          -- pending/approved/rejected
  decision TEXT,                          -- 审批意见
  decided_at TEXT,                        -- 决策时间
  created_at TEXT DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (rfq_id) REFERENCES rfqs(id) ON DELETE CASCADE,
  FOREIGN KEY (approver_id) REFERENCES users(id)
);

CREATE INDEX idx_rfq_approvals_rfq_id ON rfq_approvals(rfq_id);
CREATE INDEX idx_rfq_approvals_status ON rfq_approvals(status);
```

### 6. 审批评论表（approval_comments）

```sql
CREATE TABLE approval_comments (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  approval_id INTEGER NOT NULL,
  author_id INTEGER NOT NULL,             -- 评论人
  author_name TEXT NOT NULL,              -- 评论人姓名
  content TEXT NOT NULL,                  -- 评论内容
  created_at TEXT DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (approval_id) REFERENCES rfq_approvals(id) ON DELETE CASCADE,
  FOREIGN KEY (author_id) REFERENCES users(id)
);

CREATE INDEX idx_approval_comments_approval_id ON approval_comments(approval_id);
```

### 7. PR（采购请求单）表（purchase_requests）

```sql
CREATE TABLE purchase_requests (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  pr_number TEXT UNIQUE NOT NULL,         -- PR单号（自动生成）
  rfq_id INTEGER NOT NULL,
  selected_quote_id INTEGER NOT NULL,     -- 选定的报价
  supplier_id INTEGER NOT NULL,           -- 供应商
  total_amount REAL NOT NULL,             -- 总金额
  currency TEXT DEFAULT 'CNY',
  status TEXT DEFAULT 'draft',            -- draft/submitted/approved/rejected
  created_by INTEGER NOT NULL,            -- 创建人
  created_at TEXT DEFAULT CURRENT_TIMESTAMP,
  approved_at TEXT,
  notes TEXT,
  FOREIGN KEY (rfq_id) REFERENCES rfqs(id),
  FOREIGN KEY (selected_quote_id) REFERENCES quotes(id),
  FOREIGN KEY (supplier_id) REFERENCES suppliers(id),
  FOREIGN KEY (created_by) REFERENCES users(id)
);

CREATE INDEX idx_purchase_requests_rfq_id ON purchase_requests(rfq_id);
CREATE INDEX idx_purchase_requests_pr_number ON purchase_requests(pr_number);
```

### 8. PR 行项目表（pr_line_items）

```sql
CREATE TABLE pr_line_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  pr_id INTEGER NOT NULL,
  rfq_line_item_id INTEGER NOT NULL,
  quote_line_item_id INTEGER NOT NULL,
  item_name TEXT NOT NULL,
  quantity REAL NOT NULL,
  unit TEXT NOT NULL,
  unit_price REAL NOT NULL,
  total_price REAL NOT NULL,
  FOREIGN KEY (pr_id) REFERENCES purchase_requests(id) ON DELETE CASCADE,
  FOREIGN KEY (rfq_line_item_id) REFERENCES rfq_line_items(id),
  FOREIGN KEY (quote_line_item_id) REFERENCES quote_line_items(id)
);

CREATE INDEX idx_pr_line_items_pr_id ON pr_line_items(pr_id);
```

### 9. 更新 rfqs 表结构

```sql
-- 新增字段
ALTER TABLE rfqs ADD COLUMN material_category_type TEXT DEFAULT 'IDM'; -- 'DM' or 'IDM'
ALTER TABLE rfqs ADD COLUMN is_line_item_mode INTEGER DEFAULT 1;       -- 1=多行模式
ALTER TABLE rfqs ADD COLUMN selected_quote_id INTEGER;                 -- 选定的报价
ALTER TABLE rfqs ADD COLUMN review_completed_at TEXT;                  -- 评审完成时间
ALTER TABLE rfqs ADD COLUMN approval_status TEXT DEFAULT 'pending';    -- pending/in_approval/approved/rejected
ALTER TABLE rfqs ADD COLUMN pr_generated INTEGER DEFAULT 0;            -- 是否已生成PR
```

---

## 🎨 前端界面设计

### 1. RFQ 创建向导（5步）

#### Step 1: 选择物料大类
```vue
<el-card class="step-card">
  <h2>选择物料大类</h2>
  <el-radio-group v-model="formData.materialCategoryType" size="large">
    <el-radio-button value="IDM">
      <div class="material-type-option">
        <span class="type-icon">🔧</span>
        <div>
          <div class="type-title">IDM物料</div>
          <div class="type-desc">间接物料 - 设备、耗材、五金配件等</div>
        </div>
      </div>
    </el-radio-button>
    <el-radio-button value="DM" disabled>
      <div class="material-type-option">
        <span class="type-icon">📦</span>
        <div>
          <div class="type-title">DM物料</div>
          <div class="type-desc">直接物料 - 生产原料（暂未开放）</div>
        </div>
      </div>
    </el-radio-button>
  </el-radio-group>
</el-card>
```

#### Step 2: 填写需求行
```vue
<el-card class="step-card">
  <div class="header-actions">
    <h2>需求明细</h2>
    <el-button type="primary" @click="addLineItem">
      <el-icon><Plus /></el-icon> 添加需求行
    </el-button>
  </div>

  <el-table :data="formData.lineItems" border>
    <el-table-column type="index" label="行号" width="60" />

    <el-table-column label="物料类别" width="150">
      <template #default="{ row }">
        <el-select v-model="row.materialCategory" placeholder="选择类别">
          <el-option label="设备" value="equipment" />
          <el-option label="耗材" value="consumables" />
          <el-option label="五金配件" value="hardware" />
          <el-option label="夹治具" value="fixtures" />
          <el-option label="模具" value="molds" />
          <el-option label="刀片" value="blades" />
        </el-select>
      </template>
    </el-table-column>

    <el-table-column label="物料名称" min-width="180">
      <template #default="{ row }">
        <el-input v-model="row.itemName" placeholder="输入物料名称" />
      </template>
    </el-table-column>

    <el-table-column label="品牌" width="120">
      <template #default="{ row }">
        <el-input v-model="row.brand" placeholder="品牌" />
      </template>
    </el-table-column>

    <el-table-column label="数量" width="100">
      <template #default="{ row }">
        <el-input-number v-model="row.quantity" :min="1" />
      </template>
    </el-table-column>

    <el-table-column label="单位" width="80">
      <template #default="{ row }">
        <el-input v-model="row.unit" placeholder="件" />
      </template>
    </el-table-column>

    <el-table-column label="规格参数" min-width="200">
      <template #default="{ row }">
        <el-input v-model="row.specifications" type="textarea" :rows="2" />
      </template>
    </el-table-column>

    <el-table-column label="附件" width="100">
      <template #default="{ row, $index }">
        <el-upload
          :action="`/api/rfq/upload-temp`"
          :show-file-list="false"
          :on-success="(res) => handleAttachmentUpload(res, $index)"
        >
          <el-button size="small" link>
            <el-icon><Upload /></el-icon>
            上传 ({{ row.attachments?.length || 0 }})
          </el-button>
        </el-upload>
      </template>
    </el-table-column>

    <el-table-column label="操作" width="100" fixed="right">
      <template #default="{ $index }">
        <el-button type="danger" size="small" link @click="removeLineItem($index)">
          删除
        </el-button>
      </template>
    </el-table-column>
  </el-table>
</el-card>
```

#### Step 3: 询价单基本信息
```vue
<el-card class="step-card">
  <h2>询价单基本信息</h2>
  <el-form :model="formData" label-width="120px">
    <el-form-item label="询价单标题" required>
      <el-input v-model="formData.title" placeholder="输入描述性标题" />
    </el-form-item>
    <el-form-item label="询价类型" required>
      <el-radio-group v-model="formData.rfqType">
        <el-radio value="short_term">短期</el-radio>
        <el-radio value="long_term">长期</el-radio>
      </el-radio-group>
    </el-form-item>
    <el-form-item label="报价截止日期" required>
      <el-date-picker v-model="formData.validUntil" type="datetime" />
    </el-form-item>
    <el-form-item label="交货期" required>
      <el-input-number v-model="formData.deliveryPeriod" :min="1" />
      <span style="margin-left: 8px">天</span>
    </el-form-item>
    <el-form-item label="预算金额">
      <el-input-number v-model="formData.budgetAmount" />
      <el-select v-model="formData.currency" style="width: 100px; margin-left: 8px">
        <el-option label="CNY" value="CNY" />
        <el-option label="USD" value="USD" />
      </el-select>
    </el-form-item>
    <el-form-item label="需求描述">
      <el-input v-model="formData.description" type="textarea" :rows="4" />
    </el-form-item>
  </el-form>
</el-card>
```

#### Step 4: 邀请供应商
```vue
<!-- 复用现有的 RfqSupplierInvitation 组件 -->
<RfqSupplierInvitation
  v-model="formData.supplierIds"
  v-model:external-emails="formData.externalEmails"
/>
```

#### Step 5: 预览提交
```vue
<el-card class="step-card">
  <h2>预览并提交</h2>

  <!-- 显示物料大类 -->
  <el-descriptions :column="2" border>
    <el-descriptions-item label="物料大类">
      {{ formData.materialCategoryType === 'IDM' ? 'IDM物料' : 'DM物料' }}
    </el-descriptions-item>
    <!-- 其他基本信息 -->
  </el-descriptions>

  <!-- 需求明细表 -->
  <h3>需求明细 ({{ formData.lineItems.length }} 行)</h3>
  <el-table :data="formData.lineItems" border>
    <!-- 显示所有需求行信息 -->
  </el-table>

  <!-- 邀请供应商信息 -->
  <h3>邀请供应商 ({{ formData.supplierIds.length }} 家)</h3>
  <!-- ... -->
</el-card>
```

### 2. 采购员评审界面

```vue
<el-card class="review-card">
  <h2>选定供应商</h2>

  <!-- 报价对比表 -->
  <el-table :data="quotes">
    <el-table-column label="供应商" prop="supplierName" />
    <el-table-column label="总报价">
      <template #default="{ row }">
        {{ row.totalAmount }} {{ row.currency }}
      </template>
    </el-table-column>
    <el-table-column label="操作">
      <template #default="{ row }">
        <el-button @click="viewQuoteDetail(row)">查看明细</el-button>
        <el-button type="primary" @click="selectQuote(row)">
          选定此供应商
        </el-button>
      </template>
    </el-table-column>
  </el-table>

  <!-- 价格对比截图上传（耗材/五金配件必填） -->
  <div v-if="needsPriceComparison" class="price-comparison-section">
    <h3>价格对比（必填）</h3>
    <el-alert type="info" :closable="false">
      耗材或五金配件物料需上传1688、震坤行、京东平台的价格截图
    </el-alert>

    <div v-for="platform in ['1688', 'zkh', 'jd']" :key="platform" class="platform-comparison">
      <h4>{{ getPlatformName(platform) }}</h4>
      <el-form>
        <el-form-item label="产品链接">
          <el-input v-model="priceComparison[platform].url" placeholder="粘贴产品链接" />
        </el-form-item>
        <el-form-item label="价格截图">
          <el-upload
            :action="`/api/rfq/${rfqId}/price-comparison`"
            :data="{ platform }"
            :on-success="(res) => handleScreenshotUpload(res, platform)"
          >
            <el-button type="primary">上传截图</el-button>
          </el-upload>
        </el-form-item>
        <el-form-item label="平台价格">
          <el-input-number v-model="priceComparison[platform].price" />
        </el-form-item>
      </el-form>
    </div>
  </div>

  <div class="actions">
    <el-button type="primary" @click="submitReview">
      提交评审并发起审批
    </el-button>
  </div>
</el-card>
```

### 3. 审批界面

```vue
<el-card class="approval-card">
  <h2>审批 - {{ approvalStep.stepName }}</h2>

  <!-- RFQ 信息 -->
  <el-descriptions :column="2" border>
    <!-- 显示完整RFQ信息 -->
  </el-descriptions>

  <!-- 需求明细 -->
  <h3>需求明细</h3>
  <el-table :data="rfq.lineItems" border>
    <!-- ... -->
  </el-table>

  <!-- 选定的报价 -->
  <h3>选定报价</h3>
  <el-table :data="selectedQuote.lineItems" border>
    <!-- ... -->
  </el-table>

  <!-- 价格对比截图（如有） -->
  <div v-if="priceComparisonAttachments.length > 0">
    <h3>价格对比</h3>
    <el-row :gutter="16">
      <el-col v-for="attachment in priceComparisonAttachments" :key="attachment.id" :span="8">
        <el-card>
          <h4>{{ getPlatformName(attachment.platform) }}</h4>
          <el-image :src="attachment.filePath" fit="contain" />
          <p v-if="attachment.productUrl">
            <a :href="attachment.productUrl" target="_blank">查看链接</a>
          </p>
          <p>平台价格: {{ attachment.platformPrice }} CNY</p>
        </el-card>
      </el-col>
    </el-row>
  </div>

  <!-- 评论区 -->
  <div class="comments-section">
    <h3>评论</h3>
    <div v-for="comment in comments" :key="comment.id" class="comment-item">
      <div class="comment-header">
        <strong>{{ comment.authorName }}</strong>
        <span>{{ formatDateTime(comment.createdAt) }}</span>
      </div>
      <p>{{ comment.content }}</p>
    </div>

    <el-form>
      <el-form-item label="邀请采购员评论">
        <el-select v-model="invitedPurchasers" multiple placeholder="选择采购员">
          <el-option
            v-for="user in purchasers"
            :key="user.id"
            :label="user.name"
            :value="user.id"
          />
        </el-select>
      </el-form-item>
      <el-form-item label="评论内容">
        <el-input v-model="newComment" type="textarea" :rows="3" />
      </el-form-item>
      <el-button @click="addComment">发表评论</el-button>
    </el-form>
  </div>

  <!-- 审批决策 -->
  <div class="approval-actions">
    <el-form>
      <el-form-item label="审批意见">
        <el-input v-model="approvalDecision" type="textarea" :rows="3" />
      </el-form-item>
    </el-form>
    <el-button type="success" size="large" @click="approve">批准</el-button>
    <el-button type="danger" size="large" @click="reject">驳回</el-button>
  </div>
</el-card>
```

### 4. PR 生成界面

```vue
<el-card class="pr-card">
  <h2>生成采购请求单（PR）</h2>

  <el-alert type="success" :closable="false">
    所有审批已完成，可以生成PR单据
  </el-alert>

  <el-descriptions :column="2" border>
    <el-descriptions-item label="PR单号">
      {{ prNumber }}（自动生成）
    </el-descriptions-item>
    <el-descriptions-item label="供应商">
      {{ selectedSupplier.companyName }}
    </el-descriptions-item>
    <el-descriptions-item label="总金额">
      {{ totalAmount }} {{ currency }}
    </el-descriptions-item>
    <!-- ... -->
  </el-descriptions>

  <h3>PR 明细</h3>
  <el-table :data="prLineItems" border>
    <el-table-column type="index" label="行号" />
    <el-table-column label="物料名称" prop="itemName" />
    <el-table-column label="数量" prop="quantity" />
    <el-table-column label="单价" prop="unitPrice" />
    <el-table-column label="总价" prop="totalPrice" />
  </el-table>

  <div class="actions">
    <el-button type="primary" size="large" @click="generatePR">
      生成PR单据
    </el-button>
  </div>
</el-card>
```

---

## 📊 业务规则

### 1. 物料大类限制
- **IDM物料**：开放，可正常创建RFQ
- **DM物料**：暂不开放，选项置灰

### 2. 价格对比要求
- **耗材（consumables）** 或 **五金配件（hardware）** 物料：
  - 必须上传 1688、震坤行、京东 三个平台的价格截图
  - 可选填写产品链接
  - 可选填写平台价格
- 其他物料：不强制要求

### 3. 审批流程
- **采购经理审批**：
  - 查看RFQ、报价、价格对比
  - 可邀请采购员评论
  - 批准后进入下一步
  - 驳回则返回采购员修改

- **采购总监审批**：
  - 查看完整信息
  - 可邀请采购员评论
  - 批准后可生成PR
  - 驳回则返回采购员修改

### 4. PR 生成规则
- 只有审批流程全部完成后才能生成PR
- PR单号自动生成：格式为 `PR-YYYYMMDD-XXXX`
- PR包含所有需求行明细和报价信息

---

## 🔄 状态流转

### RFQ 状态
```
draft              → 草稿（创建中）
published          → 已发布（等待供应商报价）
quote_received     → 已收到报价（3天后）
under_review       → 评审中（采购员选定供应商）
pending_approval   → 待审批（采购经理审批中）
manager_approved   → 经理已批准（采购总监审批中）
approved           → 审批完成（可生成PR）
pr_generated       → 已生成PR
rejected           → 已驳回
cancelled          → 已取消
```

### 审批状态
```
pending   → 待审批
approved  → 已批准
rejected  → 已驳回
```

---

## 📝 API 端点设计

### RFQ 相关
```
POST   /api/rfq                          # 创建RFQ
GET    /api/rfq/:id                      # 获取RFQ详情
PUT    /api/rfq/:id                      # 更新RFQ
DELETE /api/rfq/:id/line-items/:lineId  # 删除需求行
POST   /api/rfq/upload-temp              # 上传临时附件
```

### 供应商报价
```
POST   /api/rfq/:id/quotes               # 提交报价
GET    /api/rfq/:id/quotes                # 获取所有报价
```

### 采购员评审
```
POST   /api/rfq/:id/select-quote         # 选定供应商
POST   /api/rfq/:id/price-comparison     # 上传价格对比截图
POST   /api/rfq/:id/submit-review        # 提交评审并发起审批
```

### 审批流程
```
GET    /api/rfq/:id/approvals            # 获取审批流程
POST   /api/rfq/:id/approvals/:approvalId/approve   # 批准
POST   /api/rfq/:id/approvals/:approvalId/reject    # 驳回
POST   /api/rfq/:id/approvals/:approvalId/comments  # 添加评论
POST   /api/rfq/:id/approvals/:approvalId/invite    # 邀请采购员评论
```

### PR 生成
```
POST   /api/rfq/:id/generate-pr          # 生成PR
GET    /api/purchase-requests/:id        # 获取PR详情
```

---

## 🎯 实施计划

### Phase 1: 数据库架构（2小时）
- [ ] 创建所有新表
- [ ] 更新 rfqs 表结构
- [ ] 创建索引

### Phase 2: 后端API（4小时）
- [ ] 实现RFQ创建和管理API
- [ ] 实现供应商报价API
- [ ] 实现评审和选定供应商API
- [ ] 实现审批流程API
- [ ] 实现PR生成API

### Phase 3: 前端组件（6小时）
- [ ] 重构RFQ创建向导（5步）
- [ ] 实现需求行管理组件
- [ ] 实现供应商报价表单
- [ ] 实现采购员评审界面
- [ ] 实现审批界面
- [ ] 实现PR展示界面

### Phase 4: 测试（2小时）
- [ ] 端到端流程测试
- [ ] 权限测试
- [ ] 边界情况测试

**预计总工时：14小时**

---

**最后更新：** 2025-10-21
**设计版本：** v2.0
