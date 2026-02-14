using System.Collections.Immutable;

namespace SupplierSystem.Application.Security;

public static class RolePermissions
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Roles =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tracking"] = new[] { Permissions.ViewRegistrationStatus },
            ["temp_supplier"] = new[]
            {
                Permissions.SupplierSelfRegister,
                Permissions.SupplierSelfStatus,
                Permissions.SupplierRfqShort,
                Permissions.SupplierContractChecklist,
                Permissions.SupplierContractUpload,
                Permissions.SupplierUpgradeProgress,
            },
            ["formal_supplier"] = new[]
            {
                Permissions.SupplierSelfStatus,
                Permissions.SupplierRfqLong,
                Permissions.SupplierRfqReview,
                Permissions.SupplierContractChecklist,
                Permissions.SupplierContractUpload,
                Permissions.SupplierReconciliationView,
                Permissions.SupplierReconciliationUpload,
            },
            ["purchaser"] = new[]
            {
                Permissions.PurchaserSegmentManage,
                Permissions.PurchaserRfqTarget,
                Permissions.PurchaserUpgradeInit,
                Permissions.PurchaserInvoiceSupport,
                Permissions.PurchaserRegistrationApprove,
                Permissions.SupplierRfqShort,
                Permissions.PurchaserSupplierCreate,
                Permissions.RfqCreate,
                Permissions.RfqEdit,
                Permissions.RfqView,
                Permissions.RfqViewQuotes,
                Permissions.RfqPublish,
                Permissions.RfqInviteSuppliers,
                Permissions.RfqClose,
            },
            ["procurement_manager"] = new[]
            {
                Permissions.PurchaserSegmentManage,
                Permissions.PurchaserRfqTarget,
                Permissions.ProcurementManagerUpgradeApprove,
                Permissions.ProcurementManagerRegistrationApprove,
                Permissions.ProcurementManagerPermissionException,
                Permissions.ProcurementManagerRfqReview,
                Permissions.SupplierRfqLong,
                Permissions.SupplierRfqReview,
                Permissions.AdminPurchasingGroupsManage,
                Permissions.AdminSupplierTags,
                Permissions.AdminOrgUnitsManage,
                Permissions.RfqView,
                Permissions.RfqViewAll,
                Permissions.RfqViewQuotes,
            },
            ["procurement_director"] = new[]
            {
                Permissions.ProcurementDirectorRfqApprove,
                Permissions.ProcurementDirectorCoreSupplier,
                Permissions.ProcurementDirectorProcessException,
                Permissions.ProcurementDirectorReportsView,
                Permissions.ProcurementDirectorWhitelistManage,
                Permissions.ProcurementDirectorBlacklistManage,
                Permissions.ProcurementDirectorUpgradeApprove,
                Permissions.ProcurementDirectorRegistrationApprove,
                Permissions.ProcurementManagerRfqReview,
                Permissions.SupplierRfqLong,
                Permissions.SupplierRfqReview,
                Permissions.AdminOrgUnitsManage,
                Permissions.RfqView,
                Permissions.RfqViewAll,
                Permissions.RfqViewQuotes,
            },
            ["quality_manager"] = new[]
            {
                Permissions.QualityManagerUpgradeReview,
                Permissions.QualityManagerRegistrationApprove,
            },
            ["finance_accountant"] = new[]
            {
                Permissions.FinanceAccountantInvoiceAudit,
                Permissions.FinanceAccountantRegistrationApprove,
                Permissions.FinanceAccountantReconciliation,
                Permissions.PurchaserInvoiceSupport,
            },
            ["finance_cashier"] = new[]
            {
                Permissions.FinanceCashierRegistrationApprove,
            },
            ["finance_director"] = new[]
            {
                Permissions.FinanceDirectorInvoiceApprove,
                Permissions.FinanceDirectorAdvanceException,
                Permissions.FinanceDirectorRiskMonitor,
                Permissions.FinanceDirectorCompliance,
                Permissions.FinanceDirectorReconciliation,
                Permissions.FinanceDirectorUpgradeApprove,
                Permissions.FinanceDirectorRegistrationApprove,
                Permissions.FinanceAccountantInvoiceAudit,
                Permissions.FinanceAccountantReconciliation,
            },
            ["admin"] = new[]
            {
                Permissions.AdminRoleManage,
                Permissions.AdminApprovalFlowConfig,
                Permissions.AdminSupplierTags,
                Permissions.AdminTemplateManage,
                Permissions.AdminPurchasingGroupsManage,
                Permissions.AdminBuyerAssignmentsManage,
                Permissions.AdminOrgUnitsManage,
                Permissions.AdminSystemConfig,
                Permissions.AdminEmergencyLockdown,
                Permissions.PurchaserSegmentManage,
                Permissions.ProcurementDirectorReportsView,
                Permissions.ProcurementDirectorWhitelistManage,
                Permissions.ProcurementDirectorBlacklistManage,
                Permissions.QualityManagerUpgradeReview,
                Permissions.QualityManagerRegistrationApprove,
                Permissions.ProcurementManagerUpgradeApprove,
                Permissions.ProcurementManagerRegistrationApprove,
                Permissions.ProcurementDirectorUpgradeApprove,
                Permissions.ProcurementDirectorRegistrationApprove,
                Permissions.FinanceDirectorUpgradeApprove,
                Permissions.FinanceDirectorRegistrationApprove,
                Permissions.FinanceAccountantRegistrationApprove,
                Permissions.FinanceDirectorRiskMonitor,
                Permissions.PurchaserSupplierCreate,
                Permissions.RfqCreate,
                Permissions.RfqEdit,
                Permissions.RfqEditAll,
                Permissions.RfqView,
                Permissions.RfqViewAll,
                Permissions.RfqPublish,
                Permissions.RfqClose,
                Permissions.RfqDelete,
                Permissions.RfqInviteSuppliers,
            },
            ["department_user"] = new[] { Permissions.DepartmentRequisitionManage },
        }.ToImmutableDictionary();

    private static readonly IReadOnlyDictionary<string, string> RoleAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["supplier"] = "formal_supplier",
            ["temporary_supplier"] = "temp_supplier",
            ["purchase_manager"] = "procurement_manager",
            ["procurement_manager"] = "procurement_manager",
            ["procurement_director"] = "procurement_director",
            ["quality_manager"] = "quality_manager",
            ["quality"] = "quality_manager",
            ["finance_manager"] = "finance_director",
            ["financeAccountant"] = "finance_accountant",
            ["FinanceAccountant"] = "finance_accountant",
            ["Finance Accountant"] = "finance_accountant",
            ["finance-accountant"] = "finance_accountant",
            ["finance_cashier"] = "finance_cashier",
            ["finance-cashier"] = "finance_cashier",
            ["financecashier"] = "finance_cashier",
            ["cashier"] = "finance_cashier",
            ["financeDirector"] = "finance_director",
            ["FinanceDirector"] = "finance_director",
            ["Finance Director"] = "finance_director",
            ["finance-director"] = "finance_director",
            ["departmentuser"] = "department_user",
            ["departmentUser"] = "department_user",
            ["DepartmentUser"] = "department_user",
            ["department-user"] = "department_user",
            ["department"] = "department_user",
        }.ToImmutableDictionary();

    public static string? GetRoleKey(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        var normalized = role.Trim();
        if (Roles.ContainsKey(normalized))
        {
            return normalized;
        }

        return RoleAliases.TryGetValue(normalized, out var alias) ? alias : null;
    }

    public static IReadOnlyList<string> GetPermissionsForRole(string? role)
    {
        var key = GetRoleKey(role);
        if (key == null || !Roles.TryGetValue(key, out var permissions))
        {
            return Array.Empty<string>();
        }

        return permissions;
    }
}
