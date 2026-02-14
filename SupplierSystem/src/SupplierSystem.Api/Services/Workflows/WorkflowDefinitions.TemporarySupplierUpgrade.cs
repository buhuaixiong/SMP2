using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Services.Workflows;

public static class TemporarySupplierUpgradeWorkflow
{
    public const string WorkflowType = "temporary_supplier_upgrade";

    public static readonly WorkflowDefinition Definition = new(
        WorkflowType,
        new List<WorkflowStepDefinition>
        {
            new("procurement_review", "\u91c7\u8d2d\u5458\u5ba1\u6279", Permissions.PurchaserUpgradeInit, ["purchaser"]),
            new("quality_review", "\u54c1\u8d28\u5ba1\u6279", Permissions.QualityManagerUpgradeReview, ["quality_manager"]),
            new("procurement_manager_review", "\u91c7\u8d2d\u7ecf\u7406\u5ba1\u6279", Permissions.ProcurementManagerUpgradeApprove, ["procurement_manager"]),
            new("procurement_director_review", "\u91c7\u8d2d\u603b\u76d1\u5ba1\u6279", Permissions.ProcurementDirectorUpgradeApprove, ["procurement_director"]),
            new("finance_director_review", "\u8d22\u52a1\u603b\u76d1\u5ba1\u6279", Permissions.FinanceDirectorUpgradeApprove, ["finance_director"]),
        });

    public static string StatusForStep(string stepKey)
    {
        return $"pending_{stepKey}";
    }
}
