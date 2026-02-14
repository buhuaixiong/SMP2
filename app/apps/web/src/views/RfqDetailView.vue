<template>
  <div class="rfq-detail-view" v-loading="loading">
    <PageHeader>
      <template #title>
        <div class="header-left">
          <el-button :icon="ArrowLeft" @click="goBack">{{ t("common.back") }}</el-button>
          <h1>{{ rfq?.title }}</h1>
          <el-tag :type="getStatusType(rfq?.status)" size="large">
            {{ t(`rfq.status.${rfq?.status}`) }}
          </el-tag>
        </div>
      </template>
      <template #actions>
        <div class="header-actions" v-if="!isSupplier && canEditRfq">
          <el-button v-if="rfq?.status === 'draft'" type="warning" :icon="Edit" @click="editRfq">
            {{ t("common.edit") }}
          </el-button>
          <el-button
            v-if="rfq?.status === 'draft'"
            type="primary"
            :icon="Promotion"
            @click="publishRfq"
          >
            {{ t("rfq.actions.publish") }}
          </el-button>
          <el-button
            v-if="rfq?.status === 'published' || rfq?.status === 'in_progress'"
            type="danger"
            :icon="Close"
            @click="cancelRfq"
          >
            {{ t("rfq.actions.cancel") }}
          </el-button>
        </div>
      </template>
    </PageHeader>

    <!-- RFQ Information (Only visible to purchaser, procurement_manager, procurement_director) -->
    <el-card v-if="canViewRfqDetails" class="info-card" shadow="never">
      <template #header>
        <span class="card-title">{{ t("rfq.detail.rfqInfo") }}</span>
      </template>

      <el-descriptions :column="3" border>
        <el-descriptions-item :label="t('rfq.detail.rfqId')"> #{{ rfq?.id }} </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.materialType.label')">
          {{ t(`rfq.materialType.${rfq?.materialType}`) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.rfqType')">
          <el-tag :type="rfq?.rfqType === 'short_term' ? 'success' : 'warning'">
            {{ t(`rfq.rfqType.${rfq?.rfqType}`) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.distributionForm.category')">
          {{ getCategoryLabel(rfq?.distributionCategory) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.distributionForm.subcategory')">
          {{ getSubcategoryLabel(rfq?.distributionSubcategory) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.deliveryPeriod')">
          {{ rfq?.deliveryPeriod }} {{ t("common.days") }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.budgetAmount')">
          {{ rfq?.budgetAmount ? `${rfq.budgetAmount} ${rfq.currency}` : "-" }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.validUntil')">
          {{ formatDateTime(rfq?.validUntil) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.detail.createdBy')">
          {{ rfq?.createdBy }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.requestingParty')">
          {{ rfq?.requestingParty || "-" }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.requestingDepartment')">
          {{ rfq?.requestingDepartment || "-" }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.requirementDate')">
          {{ rfq?.requirementDate || "-" }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.description')" :span="3">
          {{ rfq?.description || "-" }}
        </el-descriptions-item>
        <el-descriptions-item
          v-if="rfq?.detailedParameters"
          :label="t('rfq.form.detailedParameters')"
          :span="3"
        >
          <pre style="margin: 0; white-space: pre-wrap; word-break: break-word; font-size: 13px">{{
            rfq.detailedParameters
          }}</pre>
        </el-descriptions-item>
      </el-descriptions>
    </el-card>

    <!-- Supplier Quote Submission Form (Suppliers Only - Prominent Position) -->
    <el-card
      v-if="showSupplierQuoteForm"
      class="supplier-quote-card prominent"
      shadow="hover"
    >
      <template #header>
        <div class="quote-form-header">
          <div class="header-content">
            <span class="card-title">{{ t("rfq.detail.submitQuote") }}</span>
            <el-tag v-if="rfq?.validUntil" :type="getDeadlineTagType()" size="large">
              {{ getDeadlineText() }}
            </el-tag>
          </div>
          <el-alert
            v-if="supplierInvitation?.needsResponse"
            :title="t('rfq.quote.pleaseSubmitQuote')"
            type="warning"
            :closable="false"
            show-icon
            style="margin-top: 12px"
          />
        </div>
      </template>

      <SupplierQuoteForm
        v-if="rfq"
        :rfq="rfq"
        :existing-quote="supplierQuote"
        @submitted="handleQuoteSubmitted"
        @cancel="handleQuoteEditCancelled"
      />
    </el-card>

    <el-card v-if="isSupplier && supplierInvitation" class="invitation-card" shadow="never">
      <template #header>
        <span class="card-title">{{ t("rfq.management.supplierInvitations.title") }}</span>
      </template>
      <el-descriptions :column="2" border>
        <el-descriptions-item :label="t('rfq.management.supplierInvitations.columns.status')">
          <el-tag :type="supplierInvitationTagType(supplierInvitation!)" size="small">
            {{ translateRfqStatusLabel(supplierInvitation?.rfqStatus) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.management.supplierInvitations.columns.quoteStatus')">
          {{ translateQuoteStatusLabel(supplierInvitation?.quoteStatus) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.management.supplierInvitations.columns.validUntil')">
          {{ formatDateTime(supplierInvitation?.validUntil || null) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.management.supplierInvitations.columns.daysRemaining')">
          {{ formatInvitationDays(supplierInvitation?.daysRemaining ?? null) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.management.supplierInvitations.columns.needsResponse')" :span="2">
          <el-tag :type="supplierInvitationTagType(supplierInvitation!)" size="small">
            {{ supplierInvitationStatusLabel(supplierInvitation!) }}
          </el-tag>
        </el-descriptions-item>
      </el-descriptions>
    </el-card>
    <!-- RFQ Items (for multi-item RFQs) -->
    <el-card
      v-if="rfq?.items && rfq.items.length > 0"
      class="items-card"
      shadow="never"
    >
      <template #header>
        <span class="card-title">
          {{ t("rfq.items.title") }}
          ({{ rfq.items.length }} {{ t("rfq.items.title") }})
        </span>
      </template>

      <el-table :data="rfq.items" border style="width: 100%">
        <el-table-column
          type="index"
          :label="t('rfq.items.lineNumber')"
          width="70"
          align="center"
        />
        <el-table-column
          v-if="!isLineItemMode"
          :label="t('rfq.materialType.label')"
          prop="materialType"
          width="100"
        >
          <template #default="{ row }">
            {{ t(`rfq.materialType.${row.materialType}`) }}
          </template>
        </el-table-column>
        <el-table-column
          v-if="isLineItemMode"
          :label="t('rfq.lineItems.materialCategory')"
          width="140"
        >
          <template #default="{ row }">
            {{ getCategoryLabel(row.materialCategory || row.distributionCategory || row.materialType) }}
          </template>
        </el-table-column>
        <el-table-column
          v-if="!isLineItemMode"
          :label="t('rfq.distributionForm.category')"
          width="140"
        >
          <template #default="{ row }">
            {{ getCategoryLabel(row.distributionCategory) }}
          </template>
        </el-table-column>
        <el-table-column
          v-if="!isLineItemMode"
          :label="t('rfq.distributionForm.subcategory')"
          width="140"
        >
          <template #default="{ row }">
            {{ getSubcategoryLabel(row.distributionSubcategory) }}
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.items.itemName')" min-width="180">
          <template #default="{ row }">
            {{ row.itemName || row.description || "-" }}
          </template>
        </el-table-column>
        <el-table-column
          :label="t('rfq.items.specifications')"
          prop="specifications"
          min-width="200"
        />
        <el-table-column :label="t('rfq.items.quantity')" width="120">
          <template #default="{ row }"> {{ row.quantity }} {{ row.unit }} </template>
        </el-table-column>
        <el-table-column :label="t('rfq.items.estimatedPrice')" width="140">
          <template #default="{ row }">
            <span v-if="row.estimatedUnitPrice">
              {{ row.estimatedUnitPrice.toFixed(2) }} {{ row.currency || "CNY" }}
            </span>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.items.totalAmount')" width="140">
          <template #default="{ row }">
            <span v-if="row.quantity && row.estimatedUnitPrice" class="total-amount">
              {{ (row.quantity * row.estimatedUnitPrice).toFixed(2) }} {{ row.currency || "CNY" }}
            </span>
            <span v-else>-</span>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Invited Suppliers -->
    <el-card v-if="!isSupplier" class="suppliers-card" shadow="never">
      <template #header>
        <span class="card-title">
          {{ t("rfq.detail.invitedSuppliers") }}
          ({{ totalInvitedSuppliers }})
        </span>
      </template>

      <el-table :data="rfq?.invitedSuppliers" style="width: 100%">
        <el-table-column
          prop="companyName"
          :label="t('supplier.companyName')"
          min-width="200"
        />
        <el-table-column
          prop="vendorCode"
          :label="t('supplier.companyCode')"
          width="160"
        >
          <template #default="{ row }">
            {{ row.vendorCode ?? row.supplierCode ?? row.companyId ?? "-" }}
          </template>
        </el-table-column>
        <el-table-column prop="stage" :label="t('supplier.stage')" width="120">
          <template #default="{ row }">
            <el-tag :type="row.stage === 'temporary' ? 'warning' : 'success'" size="small">
              {{ t(`supplier.stages.${row.stage ?? "null"}`) }}
            </el-tag>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Supplier Quote Submission Section (Suppliers Only) -->
    <el-card
      v-if="isSupplier && supplierQuote && !editingQuote"
      class="supplier-quote-summary-card"
      shadow="never"
    >
      <template #header>
        <div class="card-header-flex">
          <span class="card-title">{{ t("rfq.quote.yourQuote") }}</span>
          <el-button
            v-if="canSupplierEditQuote"
            type="primary"
            size="small"
            @click="startEditingQuote"
          >
            {{ t("rfq.quote.editQuote") }}
          </el-button>
        </div>
      </template>
      <el-alert
        type="info"
        :closable="false"
        :title="t('rfq.quote.viewSubmittedMessage')"
        show-icon
        class="summary-alert"
      />
      <el-descriptions :column="2" border style="margin-top: 16px">
        <el-descriptions-item :label="t('rfq.quote.statusLabel')">
          <el-tag size="small" :type="supplierQuote.status === 'submitted' ? 'success' : 'info'">
            {{ translateQuoteStatusLabel(supplierQuote.status) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.quote.totalAmount')">
          {{ formatPrice(supplierQuote.totalAmount || 0, supplierQuote.currency || "CNY") }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.quote.submittedAt')">
          {{ formatDateTime(supplierQuote.submittedAt) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.quote.lastUpdated')">
          {{ formatDateTime(supplierQuote.updatedAt) }}
        </el-descriptions-item>
        <el-descriptions-item v-if="supplierQuote.notes" :label="t('rfq.quote.notes')" :span="2">
          {{ supplierQuote.notes }}
        </el-descriptions-item>
      </el-descriptions>

      <el-table
        v-if="supplierQuoteLineItems.length"
        :data="supplierQuoteLineItems"
        border
        size="small"
        style="margin-top: 20px"
      >
        <el-table-column type="index" width="60" />
        <el-table-column :label="t('rfq.quote.summaryColumns.item')" min-width="160">
          <template #default="{ row }">
            <div class="item-name">{{ row.itemName || '-' }}</div>
            <div v-if="row.specifications" class="item-spec">{{ row.specifications }}</div>
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.quote.summaryColumns.quantity')" width="120">
          <template #default="{ row }">
            {{ row.quantity ?? '-' }} {{ row.unit || '' }}
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.quote.summaryColumns.unitPrice')" width="140">
          <template #default="{ row }">
            {{ row.unitPrice != null ? formatPrice(row.unitPrice, supplierQuote.currency || 'CNY') : '-' }}
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.quote.summaryColumns.totalPrice')" width="140">
          <template #default="{ row }">
            {{ row.totalPrice != null ? formatPrice(row.totalPrice, supplierQuote.currency || 'CNY') : '-' }}
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.quote.taxStatus')" width="120">
          <template #default="{ row }">
            {{ t('rfq.quote.' + (getEffectiveTaxStatus(row, supplierQuote) === 'exclusive' ? 'taxExclusive' : 'taxInclusive')) }}
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.quote.deliveryDate')" width="160">
          <template #default="{ row }">
            {{ formatDateTime(row.deliveryDate) }}
          </template>
        </el-table-column>
      </el-table>

      <el-divider content-position="left" style="margin-top: 20px">
        {{ t("rfq.quotes.attachments") }}
      </el-divider>
      <div v-if="supplierQuoteAttachments.length">
        <el-table :data="supplierQuoteAttachments" border size="small">
          <el-table-column :label="t('rfq.quotes.fileName')" min-width="220">
            <template #default="{ row }">
              <el-link
                v-if="row.downloadUrl"
                :href="row.downloadUrl"
                type="primary"
                target="_blank"
                download
              >
                {{ row.originalName || row.storedName || "-" }}
              </el-link>
              <span v-else>{{ row.originalName || row.storedName || "-" }}</span>
            </template>
          </el-table-column>
          <el-table-column :label="t('rfq.quotes.fileSize')" width="130">
            <template #default="{ row }">
              {{ formatFileSize(row.fileSize) }}
            </template>
          </el-table-column>
          <el-table-column :label="t('rfq.quotes.uploadedAt')" width="180">
            <template #default="{ row }">
              {{ formatDateTime(row.uploadedAt ?? null) }}
            </template>
          </el-table-column>
        </el-table>
      </div>
      <el-alert
        v-else
        :title="t('rfq.quotes.noAttachments')"
        type="info"
        :closable="false"
      />
    </el-card>

    <!-- Line Item Independent Workflow (Internal Staff Only - Line Item Mode) -->
    <el-card v-if="showLineItemWorkflow" class="line-item-workflow-card" shadow="never">
      <RfqLineItemWorkflowLayout
        v-if="rfq?.id"
        :rfq-id="rfq.id"
      />
    </el-card>

    <!-- Quotes Section (Internal Staff Only - Non Line Item Mode) -->
    <el-card v-if="!isSupplier && !showLineItemWorkflow" class="quotes-card" shadow="never">
      <template #header>
        <div class="card-header-flex">
          <span class="card-title">
            {{ t("rfq.detail.receivedQuotes") }}
            ({{ quoteProgressLabel }})
          </span>
          <el-button
            v-if="canReviewQuotes"
            type="primary"
            :icon="Select"
            @click="showReviewDialog = true"
          >
            {{ t("rfq.detail.submitForApproval") }}
          </el-button>
        </div>
      </template>

      <!-- Quote Visibility Notice -->
      <div
        v-if="!canViewQuotes && submittedQuoteCount > 0"
        class="visibility-notice"
      >
        <el-alert :title="t('rfq.quotes.quotesHidden')" type="info" :closable="false">
          <template #default>
            <p style="margin: 0">
              {{
                t("rfq.quotes.quotesHiddenReason", {
                  total: rfq?.visibilityReason?.totalInvited ?? totalInvitedSuppliers,
                  received: rfq?.visibilityReason?.submittedCount ?? submittedQuoteCount,
                  deadline: formatDateTime(rfq?.visibilityReason?.deadline || rfq?.validUntil),
                })
              }}
            </p>
          </template>
        </el-alert>
      </div>

      <div v-else>
        <RfqPriceComparisonTable
          :rfq-id="rfq?.id"
          :line-items="rfqLineItems"
          :quotes="rfq?.quotes || []"
          :price-comparisons="rfqPriceComparisons"
          :material-type="rfq?.materialType || rfq?.materialCategoryType"
          @refresh="loadRfq"
        />
      </div>
    </el-card>

    <!-- Approval Workflow (Traditional Mode Only) -->
    <RfqApprovalWorkflow
      v-if="!isSupplier && rfq?.id && !showLineItemWorkflow"
      :rfq-id="rfq.id"
      :approvals="approvals"
      @refresh="loadRfq"
    />

    <!-- PR Fill Form (鏄剧ず鏉′欢锛氬鎵瑰凡瀹屾垚 && PR鏈～鍐?&& 鐢ㄦ埛鏄噰璐憳) -->
    <PRFillForm
      v-if="shouldShowPrFillForm"
      :rfq-id="rfq!.id"
      :rfq="rfq"
      :selected-quote="selectedQuote"
      @submitted="handlePrSubmitted"
      @cancel="handlePrCancel"
    />

    <!-- Review Dialog -->
    <el-dialog
      v-model="showReviewDialog"
      :title="t('rfq.review.submitForApprovalTitle')"
      width="700px"
      :close-on-click-modal="false"
    >
      <el-form :model="reviewForm" label-width="140px">
        <el-form-item :label="t('rfq.review.selectWinner')" required>
          <el-select
            v-model="reviewForm.selectedQuoteId"
            :placeholder="t('rfq.review.selectQuote')"
            style="width: 100%"
          >
            <el-option
              v-for="quote in rfq?.quotes"
              :key="quote.id"
              :label="getQuoteOptionLabel(quote)"
              :value="quote.id"
            />
          </el-select>
        </el-form-item>

        <el-form-item :label="t('rfq.review.scores')">
          <div class="review-scores">
            <el-row :gutter="16">
              <el-col :span="12">
                <el-input v-model="reviewForm.scores.price" type="number">
                  <template #prepend>{{ t("rfq.criteria.price") }}</template>
                  <template #append>/100</template>
                </el-input>
              </el-col>
              <el-col :span="12">
                <el-input v-model="reviewForm.scores.quality" type="number">
                  <template #prepend>{{ t("rfq.criteria.quality") }}</template>
                  <template #append>/100</template>
                </el-input>
              </el-col>
            </el-row>
            <el-row :gutter="16" style="margin-top: 16px">
              <el-col :span="12">
                <el-input v-model="reviewForm.scores.delivery" type="number">
                  <template #prepend>{{ t("rfq.criteria.delivery") }}</template>
                  <template #append>/100</template>
                </el-input>
              </el-col>
              <el-col :span="12">
                <el-input v-model="reviewForm.scores.service" type="number">
                  <template #prepend>{{ t("rfq.criteria.service") }}</template>
                  <template #append>/100</template>
                </el-input>
              </el-col>
            </el-row>
          </div>
        </el-form-item>

        <el-form-item :label="t('rfq.review.comments')">
          <el-input
            v-model="reviewForm.comments"
            type="textarea"
            :rows="4"
            :placeholder="t('rfq.review.commentsPlaceholder')"
          />
        </el-form-item>

        <!-- Price Comparison Section (IDM Materials Only) -->
        <template v-if="requiresPriceComparison">
          <el-divider>{{ t('rfq.priceComparison.title') }}</el-divider>
          <el-alert
            :title="t('rfq.priceComparison.alert')"
            type="info"
            :closable="false"
            style="margin-bottom: 16px"
          />

          <!-- 1688 -->
          <el-form-item :label="t('rfq.priceComparison.1688')">
            <template #label>
              <span>{{ t('rfq.priceComparison.1688') }}</span>
              <el-tag
                v-if="isPlatformUploaded('1688')"
                type="success"
                size="small"
                style="margin-left: 8px"
              >
                {{ t('rfq.priceComparison.uploaded') }}
              </el-tag>
              <span v-else style="color: #f56c6c; margin-left: 4px">*</span>
            </template>
            <div style="width: 100%">
              <div v-if="isPlatformUploaded('1688')" style="color: #67c23a; font-size: 14px;">
                ✓ {{ t('rfq.priceComparison.alreadyUploaded') || 'Price comparison data already uploaded' }}
              </div>
              <el-row v-else :gutter="12">
                <el-col :span="8">
                  <input
                    type="file"
                    accept="image/*"
                    @change="(e) => handlePriceComparisonFileChange('1688', e)"
                    style="width: 100%"
                  />
                </el-col>
                <el-col :span="10">
                  <el-input
                    v-model="reviewForm.priceComparisons['1688'].url"
                    :placeholder="t('rfq.priceComparison.urlPlaceholder')"
                  />
                </el-col>
                <el-col :span="6">
                  <el-input
                    v-model.number="reviewForm.priceComparisons['1688'].price"
                    type="number"
                    :placeholder="t('rfq.priceComparison.pricePlaceholder')"
                  >
                    <template #prepend>楼</template>
                  </el-input>
                </el-col>
              </el-row>
            </div>
          </el-form-item>

          <!-- JD (浜笢) -->
          <el-form-item :label="t('rfq.priceComparison.jd')">
            <template #label>
              <span>{{ t('rfq.priceComparison.jd') }}</span>
              <el-tag
                v-if="isPlatformUploaded('jd')"
                type="success"
                size="small"
                style="margin-left: 8px"
              >
                {{ t('rfq.priceComparison.uploaded') }}
              </el-tag>
              <span v-else style="color: #f56c6c; margin-left: 4px">*</span>
            </template>
            <div style="width: 100%">
              <div v-if="isPlatformUploaded('jd')" style="color: #67c23a; font-size: 14px;">
                ✓ {{ t('rfq.priceComparison.alreadyUploaded') || 'Price comparison data already uploaded' }}
              </div>
              <el-row v-else :gutter="12">
                <el-col :span="8">
                  <input
                    type="file"
                    accept="image/*"
                    @change="(e) => handlePriceComparisonFileChange('jd', e)"
                    style="width: 100%"
                  />
                </el-col>
                <el-col :span="10">
                  <el-input
                    v-model="reviewForm.priceComparisons['jd'].url"
                    :placeholder="t('rfq.priceComparison.urlPlaceholder')"
                  />
                </el-col>
                <el-col :span="6">
                  <el-input
                    v-model.number="reviewForm.priceComparisons['jd'].price"
                    type="number"
                    :placeholder="t('rfq.priceComparison.pricePlaceholder')"
                  >
                    <template #prepend>楼</template>
                  </el-input>
                </el-col>
              </el-row>
            </div>
          </el-form-item>

          <!-- ZKH (闇囧潳琛? -->
          <el-form-item :label="t('rfq.priceComparison.zkh')">
            <template #label>
              <span>{{ t('rfq.priceComparison.zkh') }}</span>
              <el-tag
                v-if="isPlatformUploaded('zkh')"
                type="success"
                size="small"
                style="margin-left: 8px"
              >
                {{ t('rfq.priceComparison.uploaded') }}
              </el-tag>
              <span v-else style="color: #f56c6c; margin-left: 4px">*</span>
            </template>
            <div style="width: 100%">
              <div v-if="isPlatformUploaded('zkh')" style="color: #67c23a; font-size: 14px;">
                ✓ {{ t('rfq.priceComparison.alreadyUploaded') || 'Price comparison data already uploaded' }}
              </div>
              <el-row v-else :gutter="12">
                <el-col :span="8">
                  <input
                    type="file"
                    accept="image/*"
                    @change="(e) => handlePriceComparisonFileChange('zkh', e)"
                    style="width: 100%"
                  />
                </el-col>
                <el-col :span="10">
                  <el-input
                    v-model="reviewForm.priceComparisons['zkh'].url"
                    :placeholder="t('rfq.priceComparison.urlPlaceholder')"
                  />
                </el-col>
                <el-col :span="6">
                  <el-input
                    v-model.number="reviewForm.priceComparisons['zkh'].price"
                    type="number"
                    :placeholder="t('rfq.priceComparison.pricePlaceholder')"
                  >
                    <template #prepend>楼</template>
                  </el-input>
                </el-col>
              </el-row>
            </div>
          </el-form-item>
        </template>
      </el-form>

      <template #footer>
        <el-button @click="showReviewDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" :loading="reviewing" @click="submitReview">
          {{ t("rfq.review.submitForApprovalButton") }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed, watch, defineAsyncComponent } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { ArrowLeft, Edit, Promotion, Close, Select } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import {
  fetchRfqById,
  fetchRfqWorkflow,
  fetchSupplierRfq,
  reviewRfq,
  publishRfq as publishRfqApi,
  publishRfqWorkflow as publishRfqWorkflowApi,
  cancelRfq as cancelRfqApi,
  selectQuoteWorkflow,
  submitReviewWorkflow,
  fetchApprovalWorkflow,
  uploadPriceComparison,
  type RfqApprovalStep,
} from "@/api/rfq";
import { useAuthStore } from "@/stores/auth";
import { useNotification } from "@/composables";
import {
  QuoteStatus,
  type Rfq,
  type RfqItem,
  type RfqQuoteItem,
  type QuoteAttachment,
  type SupplierRfqInvitationSummary,
  type Quote,
  type RfqInvitedSupplier,
} from "@/types";
import { resolveUploadDownloadUrl } from "@/utils/fileDownload";
import { extractErrorMessage } from "@/utils/errorHandling";

type RfqItemLike = RfqItem & {
  id?: number;
  rfqLineItemId?: number | string | null;
  rfq_line_item_id?: number | string | null;
  rfqItemId?: number | string | null;
  rfq_item_id?: number | string | null;
  materialCategory?: string | null;
  materialType?: string | null;
  lineNumber?: number | string | null;
  line_number?: number | string | null;
  itemNumber?: number | string | null;
  item_number?: number | string | null;
  description?: string | null;
  parameters?: string | null;
  remarks?: string | null;
};

type NormalizedRfqItem = RfqItemLike & {
  id: number;
};

type QuoteItemLike = RfqQuoteItem & {
  rfqLineItemId?: number | string | null;
  rfq_line_item_id?: number | string | null;
  rfqItemId?: number | string | null;
  rfq_item_id?: number | string | null;
};

type QuoteAttachmentLike = QuoteAttachment & {
  stored_name?: string | null;
  storedFileName?: string | null;
  stored_file_name?: string | null;
  filePath?: string | null;
  file_path?: string | null;
  fileName?: string | null;
  file_name?: string | null;
  original_name?: string | null;
};

type PriceComparisonRecord = {
  platform?: string | null;
  platformKey?: string | null;
  platform_key?: string | null;
  fileName?: string | null;
  file_name?: string | null;
  filePath?: string | null;
  file_path?: string | null;
  storedFileName?: string | null;
  stored_file_name?: string | null;
  productUrl?: string | null;
  product_url?: string | null;
  platformPrice?: number | null;
  platform_price?: number | null;
  downloadUrl?: string | null;
  lineItemId?: number | string | null;
  line_item_id?: number | string | null;
  lineItemNumber?: number | string | null;
  line_item_number?: number | string | null;
} & Record<string, unknown>;

type RfqLike = Rfq & {
  lineItems?: RfqItemLike[];
  priceComparisons?: PriceComparisonRecord[];
  selectedQuoteId?: number | string | null;
  selected_quote_id?: number | string | null;
  prStatus?: string | null;
  pr_status?: string | null;
  approvals?: RfqApprovalStep[];
  invitations?: InvitedSupplierLike[];
};

type InvitedSupplierLike = RfqInvitedSupplier & {
  vendor_code?: string | null;
  supplier_code?: string | null;
  company_id?: string | null;
};

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const notification = useNotification();
const isSupplier = computed(() => !!authStore.user?.supplierId);

const RfqPriceComparisonTable = defineAsyncComponent(
  () => import("@/components/RfqPriceComparisonTable.vue"),
);
const SupplierQuoteForm = defineAsyncComponent(() => import("@/components/SupplierQuoteForm.vue"));
const RfqApprovalWorkflow = defineAsyncComponent(
  () => import("@/components/RfqApprovalWorkflow.vue"),
);
const PRFillForm = defineAsyncComponent(() => import("@/components/PRFillForm.vue"));
const RfqLineItemWorkflowLayout = defineAsyncComponent(
  () => import("@/components/RfqLineItemWorkflowLayout.vue"),
);

// Check if user can edit/delete RFQs (only purchaser can)
const canEditRfq = computed(() => {
  const role = authStore.user?.role;
  return role === 'purchaser' || role === 'admin';
});

// Check if user can view detailed RFQ info (purchaser, procurement_manager, procurement_director, department_user)
const canViewRfqDetails = computed(() => {
  const role = authStore.user?.role;
  return role === 'purchaser' || role === 'procurement_manager' || role === 'procurement_director' || role === 'department_user' || role === 'admin';
});

const loading = ref(false);
const reviewing = ref(false);
const showReviewDialog = ref(false);
const rfq = ref<RfqLike | null>(null);
const isLineItemMode = computed(() => Boolean(rfq.value?.isLineItemMode));
const rfqLineItems = computed<NormalizedRfqItem[]>(() => {
  const rawItems = (rfq.value?.lineItems ?? rfq.value?.items ?? []) as RfqItemLike[];
  return rawItems.map((item) => {
    const id = Number(
      item.id ??
        item.rfqLineItemId ??
        item.rfqItemId ??
        item.rfq_line_item_id ??
        item.rfq_item_id ??
        0
    );
    return { ...item, id };
  });
});
const rfqPriceComparisons = computed<PriceComparisonRecord[]>(
  () => rfq.value?.priceComparisons ?? []
);
const approvals = ref<RfqApprovalStep[]>([]);

// Compute total invited suppliers (internal + external)
const totalInvitedSuppliers = computed(() => {
  if (!rfq.value) return 0;
  const visibilityTotal = rfq.value.visibilityReason?.totalInvited;
  if (typeof visibilityTotal === "number" && visibilityTotal > 0) {
    return visibilityTotal;
  }
  return rfq.value.invitedSuppliers?.length || 0;
});

const supplierInvitation = computed(() => {
  const data = rfq.value;
  return data?.invitation ?? data?.supplierInvitation ?? null;
});

// Get supplier's existing quote (if any)
const supplierQuote = computed((): Quote | null => {
  if (!isSupplier.value || !rfq.value?.quotes) return null;

  const supplierId = authStore.user?.supplierId;
  if (!supplierId) return null;

  // Find the supplier's quote
  return rfq.value.quotes.find((quote: Quote) => quote.supplierId === supplierId) || null;
});

const editingQuote = ref(false);

// Simplified form visibility logic - show form when supplier can submit/edit
const showSupplierQuoteForm = computed(() => {
  // Must be a supplier with an invitation
  if (!isSupplier.value || !supplierInvitation.value) return false;

  // If no quote exists yet, always show the form
  if (!supplierQuote.value) return true;

  // If quote exists, only show if user explicitly clicked "Edit Quote"
  return editingQuote.value;
});

// Helper function to get effective taxStatus (item level takes precedence, fallback to quote level)
function getEffectiveTaxStatus(item: RfqQuoteItem, quote: Quote | null): string {
  if (item.taxStatus) return item.taxStatus;
  if (quote?.taxStatus) return quote.taxStatus;
  return 'inclusive';
}

const supplierQuoteLineItems = computed(() => {
  if (!rfqLineItems.value.length || !supplierQuote.value?.quoteItems) return [];
  const map = new Map<string | number | undefined, RfqItemLike>(
    rfqLineItems.value.map((item) => [
      item.id ??
        item.rfqLineItemId ??
        item.rfqItemId ??
        item.rfq_line_item_id ??
        item.rfq_item_id,
      item
    ])
  );
  const quoteItems = (supplierQuote.value.quoteItems ?? []) as QuoteItemLike[];
  return quoteItems.map((item) => {
    const refKey =
      (item.rfqLineItemId ?? item.rfqItemId ?? item.rfq_line_item_id ?? item.rfq_item_id) ??
      undefined;
    const refItem = map.get(refKey);
    return {
      ...item,
      itemName:
        refItem?.itemName ||
        refItem?.description ||
        refItem?.materialCategory ||
        refItem?.materialType ||
        "",
      specifications:
        refItem?.specifications ||
        refItem?.parameters ||
        refItem?.remarks ||
        "",
      quantity: refItem?.quantity ?? null,
      unit: refItem?.unit || ""
    };
  });
});

const supplierQuoteAttachments = computed(() => {
  const attachments = (supplierQuote.value?.attachments ?? []) as QuoteAttachmentLike[];
  return attachments.map((file) => {
    const storedName =
      file.storedName ??
      file.stored_name ??
      file.storedFileName ??
      file.stored_file_name ??
      null;
    const rawPath = file.filePath ?? file.file_path ?? null;
    const resolvedDownloadUrl = resolveUploadDownloadUrl(rawPath, storedName, 'rfq-attachments');
    const downloadUrl = resolvedDownloadUrl ?? file.downloadUrl ?? null;
    return {
      ...file,
      storedName,
      originalName: file.originalName ?? file.original_name ?? file.fileName ?? file.file_name,
      downloadUrl,
    };
  });
});

const SUBMITTED_QUOTE_STATUSES = new Set<string>(
  [
    QuoteStatus.SUBMITTED,
    QuoteStatus.UNDER_REVIEW,
    QuoteStatus.SELECTED,
    QuoteStatus.REJECTED,
  ].map((status) => String(status).toLowerCase()),
);

const submittedQuoteCount = computed(() => {
  if (!rfq.value) return 0;

  const serverCount = rfq.value.visibilityReason?.submittedCount;
  if (typeof serverCount === "number" && serverCount >= 0) {
    return serverCount;
  }

  if (!rfq.value.quotes || rfq.value.quotes.length === 0) return 0;

  return rfq.value.quotes.reduce((count: number, quote: Quote) => {
    const status = String(quote.status || "").toLowerCase();
    if (SUBMITTED_QUOTE_STATUSES.has(status)) {
      return count + 1;
    }
    if (!status && quote.submittedAt) {
      return count + 1;
    }
    return count;
  }, 0);
});

const quoteProgressLabel = computed(() => {
  const submitted = submittedQuoteCount.value;
  const total = totalInvitedSuppliers.value;
  if (total > 0) {
    return `${submitted}/${total}`;
  }
  return String(submitted);
});

const canSupplierEditQuote = computed(() => {
  if (!isSupplier.value || !rfq.value) return false;
  if (!supplierQuote.value) return true;
  const status = rfq.value.status;
  const deadline = rfq.value.validUntil ? new Date(rfq.value.validUntil) : null;
  const deadlineNotPassed = !deadline || deadline >= new Date();
  return ["published", "in_progress"].includes(status) && deadlineNotPassed;
});

// Check if user can view quotes
const canViewQuotes = computed(() => {
  if (!rfq.value) return false;

  // Admin can always view
  if (authStore.user?.role === "admin") return true;

  // For suppliers, they can always view their own quotes
  if (authStore.user?.role === "temp_supplier" || authStore.user?.role === "formal_supplier") {
    return true;
  }

  // Only purchaser, procurement_manager, and procurement_director can view during collection phase
  const authorizedRoles = ["purchaser", "procurement_manager", "procurement_director"];
  if (!authorizedRoles.includes(authStore.user?.role || "")) {
    return false;
  }

  // Use backend's quotesVisible flag if available
  if (rfq.value.quotesVisible !== undefined) {
    return rfq.value.quotesVisible;
  }

  // For purchasers: quotes are hidden until all suppliers submit OR deadline passes
  // This applies to all RFQ statuses (published, in_progress, etc.)
  // Only when RFQ is closed/cancelled, quotes become visible regardless of completion

  // If RFQ is already closed/cancelled, quotes are always visible
  if (rfq.value.status === "closed" || rfq.value.status === "cancelled") {
    return true;
  }

  // For all other statuses (published, in_progress, etc.), apply the completion rule:
  // Quotes are hidden until:
  // 1. All invited suppliers have submitted quotes, OR
  // 2. The deadline (validUntil) has passed

  const now = new Date();
  const deadline = rfq.value.validUntil ? new Date(rfq.value.validUntil) : null;
  const deadlinePassed = deadline ? now >= deadline : false;

  const invitedCount =
    rfq.value.visibilityReason?.totalInvited ?? totalInvitedSuppliers.value ?? 0;
  const allSuppliersSubmitted =
    invitedCount > 0 ? submittedQuoteCount.value >= invitedCount : submittedQuoteCount.value > 0;

  return allSuppliersSubmitted || deadlinePassed;
});

// Check if user can review and close the RFQ
const canReviewQuotes = computed(() => {
  if (isSupplier.value) return false;
  if (!rfq.value) return false;

  // Must be in published or in_progress status
  const allowedStatuses = ["published", "in_progress"];
  if (!allowedStatuses.includes(rfq.value.status)) return false;

  // Must have quotes
  if (submittedQuoteCount.value === 0) return false;

  // Must be authorized and able to view quotes
  return canViewQuotes.value;
});

// Get selected quote (winning quote)
const selectedQuote = computed(() => {
  const selectedQuoteIdRaw = rfq.value?.selectedQuoteId ?? rfq.value?.selected_quote_id;
  if (selectedQuoteIdRaw == null || !rfq.value?.quotes) return null;
  const selectedQuoteId = Number(selectedQuoteIdRaw);
  if (!Number.isFinite(selectedQuoteId)) return null;
  return rfq.value.quotes.find((quote) => Number(quote.id) === selectedQuoteId) || null;
});

// Check if should show PR fill form
const shouldShowPrFillForm = computed(() => {
  if (isSupplier.value || !rfq.value) return false;

  // In line item mode, PR is generated from the workflow, not this form
  if (showLineItemWorkflow.value) return false;

  // Must be a purchaser
  const userRole = authStore.user?.role;
  if (userRole !== 'purchaser' && userRole !== 'admin') return false;

  // Approval must be completed (all approvals approved)
  const allApproved = approvals.value.length > 0 &&
    approvals.value.every((approval) => approval.status === 'approved');
  if (!allApproved) return false;

  // PR must not be filled yet
  const prStatus = rfq.value?.prStatus ?? rfq.value?.pr_status;
  if (prStatus !== 'not_filled') return false;

  return true;
});

// Check if should show line item independent workflow
const showLineItemWorkflow = computed(() => {
  if (isSupplier.value || !rfq.value) return false;
  // Show workflow if RFQ is configured for line-by-line independent approval
  // Support both boolean true and truthy values (like number 1)
  return !!rfq.value.isLineItemMode;
});

const reviewForm = reactive({
  selectedQuoteId: null as number | null,
  scores: {
    price: 0,
    quality: 0,
    delivery: 0,
    service: 0,
  },
  comments: "",
  priceComparisons: {
    '1688': { file: null as File | null, url: '', price: null as number | null },
    'jd': { file: null as File | null, url: '', price: null as number | null },
    'zkh': { file: null as File | null, url: '', price: null as number | null },
  }
});

// Check if RFQ requires price comparisons (IDM/consumables)
const requiresPriceComparison = computed(() => {
  if (!rfq.value) return false;
  const materialType = rfq.value.materialCategoryType || rfq.value.materialType;
  return materialType === 'IDM';
});

function normalizePriceComparisonRecords(records: PriceComparisonRecord[] | undefined | null) {
  if (!Array.isArray(records)) return [];
  return records.map((record) => {
    const platformRaw = record.platform ?? record.platformKey ?? record.platform_key ?? '';
    let platform = String(platformRaw || '').toLowerCase();
    if (platform === 'zhenkunxing') {
      platform = 'zkh';
    }
    if (platform !== '1688' && platform !== 'jd' && platform !== 'zkh') {
      platform = String(platformRaw || '');
    }
    const fileName = record.fileName ?? record.file_name ?? '';
    const rawPath = record.filePath ?? record.file_path ?? '';
    const storedFileName =
      record.storedFileName ??
      record.stored_file_name ??
      (typeof rawPath === 'string' ? rawPath.split('/').pop() : '') ??
      '';
    const productUrl = record.productUrl ?? record.product_url ?? '';
    const platformPrice = record.platformPrice ?? record.platform_price ?? null;

    const downloadUrl = resolveUploadDownloadUrl(rawPath, storedFileName, 'rfq_price_comparison');
    return {
      ...record,
      platform,
      fileName,
      productUrl,
      platformPrice,
      downloadUrl,
    };
  });
}

onMounted(() => {
  loadRfq();
});

async function loadRfq() {
  loading.value = true;
  try {
    const rfqId = Number(route.params.id);
    const baseData: RfqLike = isSupplier.value
      ? await fetchSupplierRfq(rfqId)
      : await fetchRfqWorkflow(rfqId);

    console.log('[RfqDetailView] Loaded RFQ data, quotes count:', baseData?.quotes?.length);
    console.log('[RfqDetailView] Quotes:', baseData?.quotes?.map((quote) => ({ id: quote.id, supplierId: quote.supplierId })));

    if (!Array.isArray(baseData.items) && Array.isArray(baseData.lineItems)) {
      baseData.items = baseData.lineItems;
    }

    baseData.priceComparisons = normalizePriceComparisonRecords(baseData.priceComparisons);

    const invitedSource =
      Array.isArray(baseData.invitedSuppliers) && baseData.invitedSuppliers.length > 0
        ? baseData.invitedSuppliers
        : baseData.invitations ?? [];
    baseData.invitedSuppliers = normalizeInvitedSuppliers(invitedSource);

    if (!isSupplier.value) {
      if (Array.isArray(baseData.approvals) && baseData.approvals.length > 0) {
        approvals.value = baseData.approvals;
      }
    }

    if (baseData.supplierInvitation && !baseData.invitation) {
      baseData.invitation = baseData.supplierInvitation;
    }

    rfq.value = baseData;

    if (!isSupplier.value && (!Array.isArray(baseData.approvals) || baseData.approvals.length === 0)) {
      try {
        approvals.value = await fetchApprovalWorkflow(rfqId);
      } catch (error) {
        approvals.value = [];
      }
    }
  } catch (error) {
    notification.error(t("rfq.detail.loadError"));
    router.push("/rfq");
  } finally {
    loading.value = false;
  }
}

function normalizeInvitedSuppliers(
  raw: InvitedSupplierLike[] | undefined | null
): RfqInvitedSupplier[] {
  if (!Array.isArray(raw)) {
    return [];
  }
  return raw.map((entry) => {
    const vendorCode =
      entry.vendorCode ??
      entry.vendor_code ??
      entry.supplierCode ??
      entry.supplier_code ??
      entry.companyId ??
      entry.company_id ??
      null;
    return {
      ...entry,
      vendorCode,
    };
  });
}

function goBack() {
  router.push("/rfq");
}

function editRfq() {
  router.push(`/rfq/${rfq.value?.id}/edit`);
}

async function publishRfq() {
  if (isSupplier.value || !rfq.value) {
    return;
  }
  try {
    await notification.confirm(t("rfq.actions.publishConfirm"), t("common.confirm"), {
      confirmButtonText: t("common.confirm"),
      cancelButtonText: t("common.cancel"),
      type: "warning",
    });

    if (rfq.value?.id) {
      const publishFn = isLineItemMode.value ? publishRfqWorkflowApi : publishRfqApi;
      await publishFn(rfq.value.id);
      notification.success(t("rfq.actions.publishSuccess"));
      loadRfq();
    }
  } catch {
    // User cancelled
  }
}

async function cancelRfq() {
  if (isSupplier.value || !rfq.value) {
    return;
  }
  try {
    const { value: reason } = await notification.prompt(
      t("rfq.actions.cancelPrompt"),
      t("rfq.actions.cancel"),
      {
        confirmButtonText: t("common.confirm"),
        cancelButtonText: t("common.cancel"),
        inputPlaceholder: t("rfq.actions.cancelReason"),
      },
    );

    if (rfq.value?.id) {
      await cancelRfqApi(rfq.value.id, reason);
      notification.success(t("rfq.actions.cancelSuccess"));
      loadRfq();
    }
  } catch {
    // User cancelled
  }
}

async function submitReview() {
  if (!reviewForm.selectedQuoteId) {
    notification.warning(t("rfq.review.selectWinnerRequired"));
    return;
  }

  // Validate price comparisons for IDM materials
  if (requiresPriceComparison.value) {
    const platforms = ['1688', 'jd', 'zkh'];

    // Check which platforms already have uploaded data from backend
    const existingPriceComparisons = rfq.value?.priceComparisons ?? [];
    const uploadedPlatforms = new Set(
      existingPriceComparisons.map((pc) => {
        const platform = String(pc.platform || '').toLowerCase();
        return platform === 'zhenkunxing' ? 'zkh' : platform;
      })
    );

    // Only check for missing platforms that haven't been uploaded yet
    const missingPlatforms = platforms.filter(platform => {
      // If already uploaded to backend, skip validation
      if (uploadedPlatforms.has(platform)) {
        return false;
      }
      // Otherwise, check if user provided file in the form
      const comparison = reviewForm.priceComparisons[platform as keyof typeof reviewForm.priceComparisons];
      return !comparison.file || !comparison.url || comparison.price === null;
    });

    if (missingPlatforms.length > 0) {
      const platformNames = {
        '1688': '1688',
        'jd': t('rfq.priceComparison.jd') || 'JD',
        'zkh': t('rfq.priceComparison.zkh') || 'Zhenkunxing'
      } as const;
      const missing = missingPlatforms
        .map((platform) => platformNames[platform as keyof typeof platformNames])
        .join(', ');
      const fallbackMessage = `Please upload price comparisons for: ${missing}`;
      notification.warning(t('rfq.priceComparison.required', { platforms: missing }) || fallbackMessage);
      return;
    }
  }

  reviewing.value = true;
  try {
    if (rfq.value?.id) {
      // Step 1: Upload price comparisons if required (IDM materials)
      // Only upload NEW comparisons that haven't been uploaded yet
      if (requiresPriceComparison.value) {
        const platforms = ['1688', 'jd', 'zkh'];

        // Get already uploaded platforms from backend
        const existingPriceComparisons = rfq.value?.priceComparisons ?? [];
        const uploadedPlatforms = new Set(
          existingPriceComparisons.map((pc) => {
            const platform = String(pc.platform || '').toLowerCase();
            return platform === 'zhenkunxing' ? 'zkh' : platform;
          })
        );

        // Only upload platforms that are NOT already uploaded
        for (const platform of platforms) {
          // Skip if already uploaded to backend
          if (uploadedPlatforms.has(platform)) {
            continue;
          }

          const comparison = reviewForm.priceComparisons[platform as keyof typeof reviewForm.priceComparisons];
          if (comparison.file && comparison.url && comparison.price !== null) {
            await uploadPriceComparison(
              rfq.value.id,
              platform,
              comparison.file,
              comparison.price,
              comparison.url
            );
          }
        }
      }

      // Step 2: Select winning quote
      await selectQuoteWorkflow(rfq.value.id, reviewForm.selectedQuoteId);

      // Step 3: Submit for approval workflow
      await submitReviewWorkflow(rfq.value.id);

      notification.success(t("rfq.review.workflowSuccess") || "Review submitted successfully. Approval workflow initiated.");
      showReviewDialog.value = false;
      loadRfq();
    }
  } catch (error: unknown) {
    notification.error(extractErrorMessage(error));
  } finally {
    reviewing.value = false;
  }
}

// Check if a platform has already uploaded price comparison data
function isPlatformUploaded(platform: '1688' | 'jd' | 'zkh'): boolean {
  const existingPriceComparisons = rfq.value?.priceComparisons ?? [];
  return existingPriceComparisons.some((pc) => {
    const pcPlatform = String(pc.platform || '').toLowerCase();
    const normalized = pcPlatform === 'zhenkunxing' ? 'zkh' : pcPlatform;
    return normalized === platform;
  });
}

// Handle price comparison file upload
function handlePriceComparisonFileChange(platform: '1688' | 'jd' | 'zkh', event: Event) {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files.length > 0) {
    reviewForm.priceComparisons[platform].file = target.files[0];
  }
}

// Handle quote submission from supplier
function handleQuoteSubmitted() {
  editingQuote.value = false;
  loadRfq();
}

function handleQuoteEditCancelled() {
  if (supplierQuote.value) {
    editingQuote.value = false;
  } else {
    router.push("/rfq");
  }
}

function startEditingQuote() {
  if (!canSupplierEditQuote.value) return;
  editingQuote.value = true;
}

// Handle PR form submission
function handlePrSubmitted() {
  notification.success(t("rfq.pr.submitSuccess") || "PR submitted successfully");
  loadRfq(); // Reload to refresh PR status
}

function handlePrCancel() {
  // Optional: show confirmation or just do nothing
}

function getCategoryLabel(category: string | null | undefined): string {
  if (!category) return "-";
  if (category === "equipment") return t("rfq.distributionCategory.equipment");
  if (category === "consumables") return t("rfq.lineItems.consumables");
  if (category === "hardware") return t("rfq.distributionCategory.hardware");
  if (category === "other") return t("common.other");
  const key = `rfq.distributionCategory.${category}`;
  const translated = t(key);
  return translated === key ? category : translated;
}

function getSubcategoryLabel(subcategory: string | null | undefined): string {
  if (!subcategory) return "-";
  const key = `rfq.distributionSubcategory.${subcategory}`;
  const translated = t(key);
  return translated === key ? subcategory : translated;
}

function getStatusType(status: string | undefined): "success" | "info" | "warning" | "danger" {
  if (!status) return "info";
  const statusMap: Record<string, "success" | "info" | "warning" | "danger"> = {
    draft: "info",
    published: "warning",
    in_progress: "info",
    closed: "success",
    cancelled: "danger",
  };
  return statusMap[status] || "info";
}

function translateRfqStatusLabel(status: string | null | undefined): string {
  if (!status) return "-";
  const key = `rfq.status.${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

function translateQuoteStatusLabel(status: string | null | undefined): string {
  if (!status) return "-";
  let key = `rfq.quoteStatus.${status}`;
  let translated = t(key);
  if (translated === key) {
    key = `rfq.quotes.statuses.${status}`;
    translated = t(key);
  }
  if (translated === key) {
    key = `rfq.quote.statuses.${status}`;
    translated = t(key);
  }
  return translated === key ? status : translated;
}
function supplierInvitationTagType(
  invitation: SupplierRfqInvitationSummary & { denialReason?: string | null }
): "info" | "success" | "warning" {
  if (invitation.needsResponse) {
    return "warning";
  }
  if (invitation.denialReason) {
    return "info";
  }
  return "success";
}

function supplierInvitationStatusLabel(
  invitation: SupplierRfqInvitationSummary & { denialReason?: string | null }
): string {
  if (invitation.needsResponse) {
    return t("rfq.management.supplierInvitations.needsResponseTag");
  }
  if (invitation.denialReason) {
    return t("rfq.management.supplierInvitations.expiredTag");
  }
  return t("rfq.management.supplierInvitations.responseSubmittedTag");
}

function formatInvitationDays(value: number | null): string {
  if (value === null || value === undefined) {
    return "-";
  }
  return String(value);
}

function formatFileSize(size?: number | null): string {
  if (size === null || size === undefined || Number.isNaN(Number(size))) {
    return "-";
  }
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = Number(size);
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  const precision = unitIndex === 0 ? 0 : value < 10 ? 1 : 2;
  return `${value.toFixed(precision)} ${units[unitIndex]}`;
}

function formatDateTime(dateString: string | null | undefined): string {
  if (!dateString) return "-";
  return new Date(dateString).toLocaleString();
}


function formatPrice(amount?: number | null, currency = "CNY"): string {
  if (amount === undefined || amount === null) return "-";
  const value = Number(amount);
  if (!Number.isFinite(value)) return "-";
  return formatCurrency(value, currency);
}

function formatCurrency(amount: number, currency: string): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency,
  }).format(amount);
}

// Format quote option label for selection dropdown
function getQuoteOptionLabel(quote: Quote): string {
  const companyName = quote.companyName || t('rfq.quote.unknownSupplier') || 'Unknown Supplier';
  const currency = quote.currency || 'CNY';

  // For multi-item/line-item RFQs, use total amount instead of unit price
  if (rfq.value?.isLineItemMode || rfq.value?.isMultiItem || quote.isItemized) {
    const totalAmount = quote.totalAmount || 0;
    return `${companyName} - ${t('rfq.quote.total')}: ${formatCurrency(totalAmount, currency)}`;
  }

  // For single-item RFQs, use unit price
  const unitPrice = quote.unitPrice || 0;
  if (unitPrice === 0 && quote.totalAmount && quote.totalAmount > 0) {
    // Fallback to total amount if unit price is 0 but total amount exists
    return `${companyName} - ${t('rfq.quote.total')}: ${formatCurrency(quote.totalAmount, currency)}`;
  }

  return `${companyName} - ${formatCurrency(unitPrice, currency)}`;
}

// Deadline helpers for supplier quote form
function getDeadlineText(): string {
  if (!rfq.value?.validUntil) return "";

  const deadline = new Date(rfq.value.validUntil);
  const now = new Date();
  const diffMs = deadline.getTime() - now.getTime();
  const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays < 0) {
    return t("rfq.quote.deadlinePassed") || "Deadline Passed";
  } else if (diffDays === 0) {
    const diffHours = Math.ceil(diffMs / (1000 * 60 * 60));
    if (diffHours <= 0) {
      return t("rfq.quote.deadlineToday") || "Due Today";
    }
    return `${diffHours} ${t("common.hours") || "hours"} ${t("common.remaining") || "remaining"}`;
  } else if (diffDays === 1) {
    return t("rfq.quote.deadline1Day") || "1 day remaining";
  } else {
    return `${diffDays} ${t("common.days") || "days"} ${t("common.remaining") || "remaining"}`;
  }
}

function getDeadlineTagType(): "success" | "warning" | "danger" | "info" {
  if (!rfq.value?.validUntil) return "info";

  const deadline = new Date(rfq.value.validUntil);
  const now = new Date();
  const diffMs = deadline.getTime() - now.getTime();
  const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays < 0) return "danger";
  if (diffDays <= 1) return "danger";
  if (diffDays <= 3) return "warning";
  return "success";
}
</script>

<style scoped>
.rfq-detail-view {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}


.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.header-left h1 {
  margin: 0;
  font-size: 24px;
  font-weight: 600;
  color: #303133;
}

.header-actions {
  display: flex;
  gap: 12px;
}

.info-card,
.suppliers-card,
.quotes-card {
  margin-bottom: 24px;
}

.card-title {
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.card-header-flex {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.review-scores {
  width: 100%;
}

.visibility-notice {
  padding: 16px 0;
}

.items-card {
  margin-top: 24px;
}

.total-amount {
  font-weight: 600;
  color: #409eff;
}
.invitation-card {
  margin-bottom: 16px;
}


.supplier-quote-card {
  margin-bottom: 24px;
}

.supplier-quote-card.prominent {
  border: 2px solid #409eff;
  box-shadow: 0 4px 12px rgba(64, 158, 255, 0.15);
}

.supplier-quote-card.prominent:hover {
  box-shadow: 0 6px 16px rgba(64, 158, 255, 0.25);
}

.quote-form-header {
  width: 100%;
}

.quote-form-header .header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}

.supplier-quote-summary-card {
  margin-bottom: 24px;
}

.summary-alert {
  margin-bottom: 8px;
}
</style>















