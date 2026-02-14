namespace SupplierSystem.Application.Security;

public static class Permissions
{
    public const string SupplierSelfRegister = "supplier.self.register";
    public const string SupplierSelfStatus = "supplier.self.status";
    public const string SupplierRfqShort = "rfq.short.participate";
    public const string SupplierContractChecklist = "contracts.self.checklist";
    public const string SupplierContractUpload = "contracts.self.upload";
    public const string SupplierUpgradeProgress = "supplier.upgrade.progress";
    public const string SupplierRfqLong = "rfq.long.participate";
    public const string SupplierRfqReview = "rfq.long.review";
    public const string SupplierReconciliationView = "supplier.reconciliation.view";
    public const string SupplierReconciliationUpload = "supplier.reconciliation.upload";

    public const string ViewRegistrationStatus = "registration.status.view";

    public const string PurchaserSegmentManage = "supplier.segment.manage";
    public const string PurchaserRfqTarget = "rfq.short.manage";
    public const string PurchaserUpgradeInit = "supplier.upgrade.init";
    public const string PurchaserInvoiceSupport = "finance.invoice.support";
    public const string PurchaserRegistrationApprove = "registration.approve.purchaser";

    public const string ProcurementManagerUpgradeApprove = "supplier.upgrade.approve.manager";
    public const string ProcurementManagerPermissionException = "supplier.permission.exception";
    public const string ProcurementManagerRfqReview = "rfq.review.manage";
    public const string ProcurementManagerRegistrationApprove = "registration.approve.manager";

    public const string QualityManagerUpgradeReview = "supplier.upgrade.approve.quality";
    public const string QualityManagerRegistrationApprove = "registration.approve.quality";

    public const string ProcurementDirectorRfqApprove = "rfq.long.approve.large";
    public const string ProcurementDirectorCoreSupplier = "supplier.core.approve";
    public const string ProcurementDirectorProcessException = "procurement.process.exception";
    public const string ProcurementDirectorReportsView = "analytics.procurement.view";
    public const string ProcurementDirectorWhitelistManage = "supplier.whitelist.manage";
    public const string ProcurementDirectorBlacklistManage = "supplier.blacklist.manage";
    public const string ProcurementDirectorUpgradeApprove = "supplier.upgrade.approve.director";
    public const string ProcurementDirectorRegistrationApprove = "registration.approve.director";

    public const string FinanceAccountantInvoiceAudit = "finance.invoice.audit";
    public const string FinanceAccountantRegistrationApprove = "registration.approve.accountant";
    public const string FinanceCashierRegistrationApprove = "registration.approve.cashier";
    public const string FinanceAccountantReconciliation = "finance.reconciliation.manage";
    public const string FinanceDirectorInvoiceApprove = "finance.invoice.approve.large";
    public const string FinanceDirectorAdvanceException = "finance.advance.approve.exception";
    public const string FinanceDirectorRiskMonitor = "finance.risk.monitor";
    public const string FinanceDirectorCompliance = "finance.compliance.manage";
    public const string FinanceDirectorReconciliation = "finance.reconciliation.view";
    public const string FinanceDirectorUpgradeApprove = "supplier.upgrade.approve.finance";
    public const string FinanceDirectorRegistrationApprove = "registration.approve.finance";

    public const string AdminRoleManage = "admin.roles.manage";
    public const string AdminApprovalFlowConfig = "admin.approval.configure";
    public const string AdminSupplierTags = "admin.tags.manage";
    public const string AdminTemplateManage = "admin.templates.manage";
    public const string AdminPurchasingGroupsManage = "admin.purchasing_groups.manage";
    public const string AdminBuyerAssignmentsManage = "admin.buyer_assignments.manage";
    public const string AdminOrgUnitsManage = "admin.org_units.manage";
    public const string AdminSystemConfig = "admin.system.config";
    public const string AdminEmergencyLockdown = "admin.emergency.lockdown";

    public const string DepartmentRequisitionManage = "department.requisition.manage";

    public const string RfqCreate = "rfq.create";
    public const string RfqEdit = "rfq.edit";
    public const string RfqEditAll = "rfq.edit.all";
    public const string RfqView = "rfq.view";
    public const string RfqViewAll = "rfq.view.all";
    public const string RfqViewQuotes = "rfq.view.quotes";
    public const string RfqPublish = "rfq.publish";
    public const string RfqClose = "rfq.close";
    public const string RfqDelete = "rfq.delete";
    public const string RfqInviteSuppliers = "rfq.invite.suppliers";

    public const string PurchaserSupplierCreate = "supplier.create";
}
