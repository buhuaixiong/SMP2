<template>
  <section class="permissions-overview">
    <header class="panel-header">
      <h2>{{ t("rolePermissions.pageTitle") }}</h2>
      <p>{{ t("rolePermissions.introduction") }}</p>
    </header>

    <div class="legend">
      <h3>{{ t("rolePermissions.legend.title") }}</h3>
      <ul class="legend-list">
        <li><span class="icon full">✓</span> {{ t("rolePermissions.legend.fullAccess") }}</li>
        <li><span class="icon read">◐</span> {{ t("rolePermissions.legend.readOnly") }}</li>
        <li>
          <span class="icon conditional">⚠</span> {{ t("rolePermissions.legend.conditional") }}
        </li>
        <li><span class="icon none">✗</span> {{ t("rolePermissions.legend.noAccess") }}</li>
      </ul>
    </div>

    <div class="role-grid">
      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.tempSupplier.name") }}</h3>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.tempSupplier.supplierInfo.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.tempSupplier.supplierInfo.viewOwn") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.tempSupplier.supplierInfo.editOwn") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.tempSupplier.supplierInfo.applyUpgrade") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.tempSupplier.documents.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.tempSupplier.documents.upload") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.tempSupplier.documents.view") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.tempSupplier.rfq.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">{{ t("rolePermissions.roles.tempSupplier.rfq.view") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.tempSupplier.rfq.submit") }}</li>
          </ul>
        </section>
      </article>

      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.formalSupplier.name") }}</h3>
        <p class="role-summary">{{ t("rolePermissions.roles.formalSupplier.inherits") }}</p>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.formalSupplier.contracts.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.formalSupplier.contracts.view") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.formalSupplier.contracts.sign") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.formalSupplier.invoices.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.formalSupplier.invoices.submit") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.formalSupplier.invoices.track") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.formalSupplier.ratings.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.formalSupplier.ratings.view") }}
            </li>
          </ul>
        </section>
      </article>

      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.purchaser.name") }}</h3>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.purchaser.suppliers.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.purchaser.suppliers.create") }}
            </li>
            <li class="section-item">{{ t("rolePermissions.roles.purchaser.suppliers.edit") }}</li>
            <li class="section-item">
              {{ t("rolePermissions.roles.purchaser.suppliers.viewAll") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.purchaser.suppliers.manageSegments") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.purchaser.rfq.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">{{ t("rolePermissions.roles.purchaser.rfq.create") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.purchaser.rfq.invite") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.purchaser.rfq.evaluate") }}</li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.purchaser.documents.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.purchaser.documents.upload") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.purchaser.documents.review") }}
            </li>
          </ul>
        </section>
      </article>

      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.quality.name") }}</h3>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.quality.ratings.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">{{ t("rolePermissions.roles.quality.ratings.create") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.quality.ratings.approve") }}</li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.quality.upgrades.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.quality.upgrades.reviewFirst") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.quality.suppliers.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.quality.suppliers.viewAll") }} ({{
                t("rolePermissions.roles.quality.suppliers.readonly")
              }})
            </li>
          </ul>
        </section>
      </article>

      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.procurementManager.name") }}</h3>
        <p class="role-summary">{{ t("rolePermissions.roles.procurementManager.inherits") }}</p>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.procurementManager.approval.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementManager.approval.suppliers") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementManager.approval.contracts") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementManager.approval.upgrades") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.procurementManager.reports.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementManager.reports.view") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementManager.reports.export") }}
            </li>
          </ul>
        </section>
      </article>

      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.procurementDirector.name") }}</h3>
        <p class="role-summary">{{ t("rolePermissions.roles.procurementDirector.inherits") }}</p>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.procurementDirector.finalApproval.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementDirector.finalApproval.suppliers") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementDirector.finalApproval.contracts") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementDirector.finalApproval.upgrades") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.procurementDirector.strategy.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementDirector.strategy.configure") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.procurementDirector.strategy.analytics") }}
            </li>
          </ul>
        </section>
      </article>

      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.financeAccountant.name") }}</h3>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.financeAccountant.invoices.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.financeAccountant.invoices.review") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.financeAccountant.invoices.process") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.financeAccountant.invoices.reconcile") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.financeAccountant.settlements.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.financeAccountant.settlements.create") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.financeAccountant.settlements.track") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.financeAccountant.suppliers.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.financeAccountant.suppliers.view") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.financeAccountant.suppliers.edit") }}
            </li>
          </ul>
        </section>
      </article>

      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.financeDirector.name") }}</h3>
        <p class="role-summary">{{ t("rolePermissions.roles.financeDirector.inherits") }}</p>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.financeDirector.approval.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.financeDirector.approval.invoices") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.financeDirector.approval.payments") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.financeDirector.approval.suppliers") }}
            </li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">
            {{ t("rolePermissions.roles.financeDirector.reports.module") }}
          </h4>
          <ul class="section-list">
            <li class="section-item">
              {{ t("rolePermissions.roles.financeDirector.reports.view") }}
            </li>
            <li class="section-item">
              {{ t("rolePermissions.roles.financeDirector.reports.audit") }}
            </li>
          </ul>
        </section>
      </article>

      <article class="role-card">
        <h3 class="role-title">{{ t("rolePermissions.roles.admin.name") }}</h3>
        <p class="role-summary">{{ t("rolePermissions.roles.admin.fullAccess") }}</p>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.admin.users.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">{{ t("rolePermissions.roles.admin.users.create") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.admin.users.edit") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.admin.users.deactivate") }}</li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.admin.system.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">{{ t("rolePermissions.roles.admin.system.configure") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.admin.system.backup") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.admin.system.logs") }}</li>
          </ul>
        </section>

        <section class="role-section">
          <h4 class="section-title">{{ t("rolePermissions.roles.admin.permissions.module") }}</h4>
          <ul class="section-list">
            <li class="section-item">{{ t("rolePermissions.roles.admin.permissions.modify") }}</li>
            <li class="section-item">{{ t("rolePermissions.roles.admin.permissions.audit") }}</li>
          </ul>
        </section>
      </article>
    </div>

    <div class="notes">
      <h3>{{ t("rolePermissions.notes.title") }}</h3>
      <ul>
        <li>{{ t("rolePermissions.notes.inheritance") }}</li>
        <li>{{ t("rolePermissions.notes.hierarchy") }}</li>
        <li>{{ t("rolePermissions.notes.contextual") }}</li>
        <li>{{ t("rolePermissions.notes.dynamic") }}</li>
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

const { t } = useI18n();

defineOptions({ name: "RolePermissionsTable" });
</script>

<style scoped>
.permissions-overview {
  background: white;
  border-radius: 12px;
  padding: 32px;
  margin-bottom: 24px;
  box-shadow: 0 18px 45px rgba(33, 41, 72, 0.08);
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.panel-header h2 {
  margin: 0 0 4px 0;
  font-size: 22px;
  color: #303133;
  font-weight: 700;
}

.panel-header p {
  margin: 0;
  color: #606266;
  font-size: 14px;
}

.legend {
  background: #f3f5ff;
  border-radius: 8px;
  padding: 16px;
}

.legend h3 {
  margin: 0 0 12px 0;
  font-size: 16px;
  color: #4545a5;
}

.legend-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
}

.legend-list li {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: #606266;
}

.legend-list .icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  border-radius: 4px;
  font-weight: bold;
}

.legend-list .icon.full {
  background: #67c23a;
  color: white;
}

.legend-list .icon.read {
  background: #409eff;
  color: white;
}

.legend-list .icon.conditional {
  background: #e6a23c;
  color: white;
}

.legend-list .icon.none {
  background: #f56c6c;
  color: white;
}

.notes {
  background: #fff8e1;
  border-radius: 8px;
  padding: 16px;
}

.notes h3 {
  margin: 0 0 12px 0;
  font-size: 16px;
  color: #e6a23c;
}

.notes ul {
  margin: 0;
  padding: 0 0 0 20px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.notes li {
  font-size: 13px;
  color: #606266;
  line-height: 1.6;
}

.role-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 20px;
}

.role-card {
  border: 1px solid #e4e7ed;
  border-radius: 10px;
  padding: 20px;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  gap: 16px;
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease;
}

.role-card:hover {
  border-color: #c0c4cc;
  box-shadow: 0 12px 30px rgba(93, 92, 222, 0.12);
}

.role-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #4545a5;
}

.role-summary {
  margin: 0;
  color: #606266;
  font-size: 14px;
}

.role-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.section-title {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
  color: #303133;
}

.section-description {
  margin: 0;
  color: #606266;
  font-size: 13px;
}

.section-list {
  margin: 0;
  padding: 0 0 0 16px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.section-item {
  font-size: 13px;
  color: #4c4d63;
}

.permission-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

.permission-table th,
.permission-table td {
  border: 1px solid #e4e7ed;
  padding: 8px 10px;
  text-align: left;
  vertical-align: top;
}

.permission-table th {
  background: #f3f5ff;
  color: #4545a5;
  font-weight: 600;
}

.permission-table td {
  color: #4c4d63;
}
</style>
