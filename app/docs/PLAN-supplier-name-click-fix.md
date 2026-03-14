# 实施计划：供应商详细对比弹窗 - 点击供应商名称查看详情

## 一、问题总结

| 项目 | 说明 |
|------|------|
| 原计划修改的文件 | `RfqQuoteComparison.vue` |
| 实际需要修改的文件 | `RfqPriceComparisonSection.vue` |
| 问题原因 | 两个文件都有"供应商详细对比"弹窗，但结构不同 |
| 截图对应的弹窗 | `RfqPriceComparisonSection.vue` 第199-257行 |

## 二、两个弹窗的结构差异

| 特征 | RfqQuoteComparison.vue | RfqPriceComparisonSection.vue |
|------|------------------------|------------------------------|
| 供应商名称位置 | 表头（横向排列） | 第一列数据（纵向排列） |
| 修改方式 | 自定义 `#header` 模板 | 自定义 `#default` 模板 |
| 已有供应商详情弹窗 | 有（第893-1024行） | 没有 |

## 三、方案选择

### 方案 A：保留 RfqQuoteComparison.vue 的修改，同时修改 RfqPriceComparisonSection.vue

- 两个文件都可能被使用，保留两处修改更完整
- `RfqQuoteComparison.vue` 的修改已经通过审查，代码正确

### 方案 B：撤销 RfqQuoteComparison.vue 的修改，只修改 RfqPriceComparisonSection.vue

- 只修改实际需要的位置
- 更简洁

**推荐方案 A**：保留已有的正确修改，同时在 `RfqPriceComparisonSection.vue` 中添加相同功能。

## 四、RfqPriceComparisonSection.vue 修改方案

### 4.1 需要添加的内容

由于 `RfqPriceComparisonSection.vue` 没有供应商详情弹窗，需要：
1. 添加供应商详情弹窗组件（从 `RfqQuoteComparison.vue` 复用）
2. 添加状态变量
3. 添加点击处理函数
4. 修改表格列模板
5. 添加必要的样式

### 4.2 修改点详细说明

#### 修改点 1：添加图标导入

**位置**：第277行

**当前代码**：
```typescript
import { ArrowRight } from '@element-plus/icons-vue'
```

**修改后**：
```typescript
import { ArrowRight, TopRight, CircleCheck, Clock } from '@element-plus/icons-vue'
```

---

#### 修改点 2：添加状态变量

**位置**：第318行之后（在 `selectedQuoteForDetails` 定义之后）

**新增代码**：
```typescript
const showSupplierProfileDialog = ref(false)
const selectedSupplierForProfile = ref<any>(null)
```

---

#### 修改点 3：添加日期格式化函数

**位置**：在 `formatCurrencyValue` 函数之后（约第752行）

**新增代码**：
```typescript
function formatDateValue(dateStr: string | Date | null | undefined): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return '-'
  return date.toLocaleDateString('zh-CN')
}
```

---

#### 修改点 4：添加点击处理函数

**位置**：在文件末尾的 `</script>` 之前

**新增代码**：
```typescript
// Handle supplier name click to show profile
function handleSupplierNameClick(quote: any) {
  selectedSupplierForProfile.value = quote
  showSupplierProfileDialog.value = true
}
```

---

#### 修改点 5：修改表格列模板

**位置**：第212行

**当前代码**：
```vue
<el-table-column :label="$t('supplier.companyName')" prop="supplierName" min-width="160" fixed />
```

**修改后**：
```vue
<el-table-column :label="$t('supplier.companyName')" min-width="160" fixed>
  <template #default="{ row }">
    <el-link
      type="primary"
      :underline="false"
      class="supplier-name-link"
      @click.stop="handleSupplierNameClick(row)"
    >
      {{ row.supplierName }}
      <el-icon class="link-icon"><TopRight /></el-icon>
    </el-link>
  </template>
</el-table-column>
```

---

#### 修改点 6：添加供应商详情弹窗

**位置**：在第257行 `</el-dialog>` 之后，第259行 `<!-- Quote Details Dialog -->` 之前

**新增代码**（从 `RfqQuoteComparison.vue` 复制供应商详情弹窗，约120行）：

```vue
    <!-- Supplier Profile Dialog -->
    <el-dialog
      v-model="showSupplierProfileDialog"
      :title="$t('rfq.quotes.supplierProfile')"
      width="700px"
    >
      <div v-if="selectedSupplierForProfile" class="supplier-profile">
        <el-tabs type="border-card">
          <el-tab-pane :label="$t('rfq.quotes.basicInfo')">
            <el-descriptions :column="2" border>
              <el-descriptions-item :label="$t('supplier.companyName')">
                {{ selectedSupplierForProfile.companyName || selectedSupplierForProfile.supplierName }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.companyId')">
                {{ selectedSupplierForProfile.companyId || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.stage')">
                <el-tag :type="selectedSupplierForProfile.stage === 'formal' ? 'success' : 'warning'">
                  {{ $t(`supplier.stages.${selectedSupplierForProfile.stage ?? 'null'}`) }}
                </el-tag>
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.category')">
                {{ selectedSupplierForProfile.category || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.region')">
                {{ selectedSupplierForProfile.region || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.address')">
                {{ selectedSupplierForProfile.address || '-' }}
              </el-descriptions-item>
            </el-descriptions>
          </el-tab-pane>

          <el-tab-pane :label="$t('rfq.quotes.financialInfo')">
            <el-descriptions :column="2" border>
              <el-descriptions-item :label="$t('supplier.registeredCapital')">
                <el-text v-if="selectedSupplierForProfile.registeredCapital" type="primary" size="large">
                  {{ formatCurrencyValue(selectedSupplierForProfile.registeredCapital, 'CNY') }}
                </el-text>
                <span v-else>-</span>
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.registrationDate')">
                {{ selectedSupplierForProfile.registrationDate ? formatDateValue(selectedSupplierForProfile.registrationDate) : '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.paymentTerms')">
                {{ selectedSupplierForProfile.paymentTerms || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.paymentCurrency')">
                {{ selectedSupplierForProfile.paymentCurrency || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.bankName')">
                {{ selectedSupplierForProfile.bankName || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.bankAccount')">
                {{ selectedSupplierForProfile.bankAccount || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.taxRegistrationNumber')">
                {{ selectedSupplierForProfile.taxRegistrationNumber || '-' }}
              </el-descriptions-item>
            </el-descriptions>
          </el-tab-pane>

          <el-tab-pane :label="$t('rfq.quotes.legalInfo')">
            <el-descriptions :column="2" border>
              <el-descriptions-item :label="$t('supplier.businessRegistrationNumber')">
                <el-text type="primary">
                  {{ selectedSupplierForProfile.businessRegistrationNumber || '-' }}
                </el-text>
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.legalRepresentative')">
                {{ selectedSupplierForProfile.legalRepresentative || '-' }}
              </el-descriptions-item>
            </el-descriptions>
          </el-tab-pane>

          <el-tab-pane :label="$t('rfq.quotes.contactInfo')">
            <el-descriptions :column="2" border>
              <el-descriptions-item :label="$t('supplier.contactPerson')">
                {{ selectedSupplierForProfile.contactPerson || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.contactPhone')">
                {{ selectedSupplierForProfile.contactPhone || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.contactEmail')" :span="2">
                {{ selectedSupplierForProfile.contactEmail || '-' }}
              </el-descriptions-item>
            </el-descriptions>
          </el-tab-pane>

          <el-tab-pane
            :label="$t('rfq.quotes.documentsInfo')"
            v-if="selectedSupplierForProfile.supplierDocuments?.length"
          >
            <el-table :data="selectedSupplierForProfile.supplierDocuments" border>
              <el-table-column :label="$t('supplier.documentCategory')" width="180">
                <template #default="{ row }">
                  <el-tag size="small">{{ $t(`supplier.documentCategories.${row.category}`) }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="$t('supplier.fileName')" prop="file_name" />
              <el-table-column :label="$t('supplier.uploadedAt')" width="160">
                <template #default="{ row }">
                  {{ formatDateValue(row.uploaded_at) }}
                </template>
              </el-table-column>
              <el-table-column :label="$t('supplier.expiryDate')" width="120">
                <template #default="{ row }">
                  <span v-if="row.expiry_date">{{ formatDateValue(row.expiry_date) }}</span>
                  <span v-else>-</span>
                </template>
              </el-table-column>
              <el-table-column :label="$t('supplier.verified')" width="100" align="center">
                <template #default="{ row }">
                  <el-icon v-if="row.verified" color="green" size="20"><CircleCheck /></el-icon>
                  <el-icon v-else color="gray" size="20"><Clock /></el-icon>
                </template>
              </el-table-column>
            </el-table>
          </el-tab-pane>
        </el-tabs>
      </div>
    </el-dialog>
```

---

#### 修改点 7：添加样式

**位置**：在 `</style>` 之前（约第1108行）

**新增代码**：
```css
/* Supplier name link in comparison dialog */
.supplier-name-link {
  font-weight: 600;
  font-size: 13px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.supplier-name-link .link-icon {
  font-size: 12px;
  opacity: 0.7;
  transition: opacity 0.2s;
}

.supplier-name-link:hover .link-icon {
  opacity: 1;
}

/* Supplier profile dialog styles */
.supplier-profile {
  :deep(.el-tabs__content) {
    padding: 16px;
  }
}
```

---

## 五、实施检查清单

### 阶段 A：修改 RfqPriceComparisonSection.vue

| 步骤 | 描述 | 验证方法 |
|------|------|----------|
| A1 | 修改第277行：添加 `TopRight, CircleCheck, Clock` 图标导入 | 检查导入语法 |
| A2 | 在第318行后添加 `showSupplierProfileDialog` 和 `selectedSupplierForProfile` 变量 | 检查变量定义 |
| A3 | 在 `formatCurrencyValue` 函数后添加 `formatDateValue` 函数 | 检查函数定义 |
| A4 | 在文件末尾添加 `handleSupplierNameClick` 函数 | 检查函数定义 |
| A5 | 修改第212行的表格列为带链接的模板 | 检查模板语法 |
| A6 | 在第257行后添加供应商详情弹窗 | 检查弹窗结构 |
| A7 | 在样式末尾添加 `.supplier-name-link` 相关样式 | 检查CSS语法 |

### 阶段 B：构建和验证

| 步骤 | 描述 | 验证方法 |
|------|------|----------|
| B1 | 运行 `npx vite build` 检查编译是否通过 | 无编译错误 |
| B2 | 重启 API 服务器 | 服务器正常启动 |
| B3 | 强制刷新浏览器（Ctrl+Shift+R） | 页面正常加载 |

### 阶段 C：功能验证

| 测试项 | 操作 | 预期结果 |
|--------|------|----------|
| C1 | 打开 RFQ 详情页面 | 页面正常显示 |
| C2 | 点击"对比详情"打开供应商详细对比弹窗 | 弹窗正常显示 |
| C3 | 查看供应商名称样式 | 显示为蓝色链接，右侧有小图标 |
| C4 | 鼠标悬停供应商名称 | 图标透明度增加 |
| C5 | 点击供应商名称 | 打开供应商资料弹窗 |
| C6 | 确认资料弹窗内容 | 显示对应供应商的详细信息 |
| C7 | 关闭资料弹窗 | 对比弹窗仍然保持打开状态 |
| C8 | 点击不同供应商名称 | 资料弹窗内容正确切换 |

---

## 六、易错点提醒

| 风险 | 规避措施 |
|------|----------|
| 变量名与 RfqQuoteComparison.vue 不同 | 使用 `selectedSupplierForProfile` 而非 `selectedSupplierQuote` |
| 数据结构可能不同 | 同时检查 `companyName` 和 `supplierName` 字段 |
| 弹窗层叠 z-index | Element Plus 默认处理，但需测试验证 |
| 事件冒泡 | 使用 `@click.stop` 阻止冒泡 |
| 日期格式化函数缺失 | 添加 `formatDateValue` 函数 |

---

## 七、最终实施清单

```
IMPLEMENTATION CHECKLIST:

1. [ ] 打开文件 C:/supplier-deploy/app/apps/web/src/components/RfqPriceComparisonSection.vue
2. [ ] 在第277行修改图标导入，添加 TopRight, CircleCheck, Clock
3. [ ] 在第318行后添加 showSupplierProfileDialog 和 selectedSupplierForProfile 变量（2行）
4. [ ] 在 formatCurrencyValue 函数后添加 formatDateValue 函数（约6行）
5. [ ] 在文件末尾 </script> 之前添加 handleSupplierNameClick 函数（约5行）
6. [ ] 修改第212行的表格列，改为带链接的模板（约10行）
7. [ ] 在第257行后添加供应商详情弹窗（约120行）
8. [ ] 在样式末尾添加 .supplier-name-link 相关样式（约20行）
9. [ ] 保存文件
10. [ ] 运行 npx vite build 检查编译是否通过
11. [ ] 重启 API 服务器
12. [ ] 强制刷新浏览器（Ctrl+Shift+R）
13. [ ] 验证：打开供应商详细对比弹窗
14. [ ] 验证：供应商名称显示为蓝色链接样式，带有 TopRight 图标
15. [ ] 验证：点击供应商名称，供应商资料弹窗正确显示
16. [ ] 验证：资料弹窗显示正确的供应商数据
17. [ ] 验证：关闭资料弹窗后，对比弹窗仍然保持打开
18. [ ] 验证：点击不同供应商名称，弹窗内容正确切换
```

---

## 八、关于已修改的 RfqQuoteComparison.vue

之前对 `RfqQuoteComparison.vue` 的修改（点击表头供应商名称查看详情）代码是正确的，可以保留。该修改适用于另一种布局的对比弹窗。

**已完成的修改：**
- 第1921-1925行：添加了 `handleSupplierNameClickInComparison` 函数
- 第1041-1063行：修改了表格列定义，添加了 `#header` 模板
- 第2496-2514行：添加了 `.supplier-name-link` 相关样式

---

*文档创建日期：2025-11-26*
