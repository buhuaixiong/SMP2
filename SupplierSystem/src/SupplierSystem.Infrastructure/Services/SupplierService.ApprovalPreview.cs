using SupplierSystem.Application.DTOs.Suppliers;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 审批预览

    public Task<ApprovalPreviewResponse> PreviewApprovalAsync(ApprovalPreviewRequest request, CancellationToken cancellationToken)
    {
        var changedFields = request.ChangedFields ?? new List<string>();
        var generatedAt = DateTime.UtcNow;

        return Task.FromResult(new ApprovalPreviewResponse
        {
            Template = "standard_approval",
            TemplateLabel = "Standard Approval",
            EstimatedWorkingDays = 3,
            Steps = new List<ApprovalPreviewStepResponse>
            {
                new()
                {
                    Key = "director_approval",
                    Title = "Director Approval",
                    Role = "procurement_director",
                    SlaDays = 3,
                    Eta = generatedAt.AddDays(3).ToString("o"),
                    Status = "pending",
                    Description = changedFields.Count == 0
                        ? "Awaiting standard approval workflow."
                        : $"Review required for {changedFields.Count} changed field(s)."
                }
            },
            RiskFlags = new List<ApprovalRiskFlagResponse>(),
            CategoryAChanges = new List<string>(),
            RequiresTempAccount = false,
            ChangeSummary = new Dictionary<string, object?>
            {
                ["changedFields"] = changedFields,
                ["supplierId"] = request.SupplierId,
                ["baselineVersion"] = request.BaselineVersion,
            },
            GeneratedAt = generatedAt.ToString("o")
        });
    }

    #endregion
}
