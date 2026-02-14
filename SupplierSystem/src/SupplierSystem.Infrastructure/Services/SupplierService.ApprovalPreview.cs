namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 审批预览

    public Task<object> PreviewApprovalAsync(object request, CancellationToken cancellationToken)
    {
        // 简化的审批预览实现
        return Task.FromResult<object>(new
        {
            template = "standard_approval",
            templateLabel = "Standard Approval",
            estimatedWorkingDays = 3,
            steps = new[]
            {
                new { key = "director_approval", title = "Director Approval", role = "procurement_director", slaDays = 3, status = "pending" }
            },
            riskFlags = new object[0],
            categoryAChanges = new string[0],
            requiresTempAccount = false,
            changeSummary = new { },
            generatedAt = DateTime.UtcNow.ToString("o")
        });
    }

    #endregion
}
