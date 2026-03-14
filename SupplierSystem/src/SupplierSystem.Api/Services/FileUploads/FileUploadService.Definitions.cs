using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Services.FileUploads;

public sealed partial class FileUploadService
{
    private static readonly IReadOnlyList<FileUploadWorkflowStep> ApprovalWorkflow =
    [
        new FileUploadWorkflowStep(
            "purchaser",
            "采购员审批",
            Permissions.PurchaserUpgradeInit,
            ["purchaser"],
            0),
        new FileUploadWorkflowStep(
            "quality_manager",
            "品质审批",
            Permissions.QualityManagerUpgradeReview,
            ["quality_manager"],
            1),
        new FileUploadWorkflowStep(
            "procurement_manager",
            "采购经理审批",
            Permissions.ProcurementManagerUpgradeApprove,
            ["procurement_manager"],
            2),
        new FileUploadWorkflowStep(
            "procurement_director",
            "采购总监审批",
            Permissions.ProcurementDirectorProcessException,
            ["procurement_director"],
            3),
        new FileUploadWorkflowStep(
            "finance_director",
            "财务总监审批",
            Permissions.FinanceDirectorCompliance,
            ["finance_director"],
            4),
    ];

    private static readonly IReadOnlyDictionary<string, FileUploadWorkflowStep> WorkflowStepIndex =
        ApprovalWorkflow.ToDictionary(step => step.Step, step => step, StringComparer.OrdinalIgnoreCase);
}
