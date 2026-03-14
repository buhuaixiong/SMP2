using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class DashboardController
{
    private async Task<int> GetPendingRfqCountAsync(int supplierId, CancellationToken cancellationToken)
    {
        return await _dashboardDataService.GetPendingRfqCountAsync(
            supplierId,
            SupplierDeclinedInvitationStatuses,
            SupplierPendingQuoteStatuses,
            cancellationToken);
    }

    private async Task<int> GetExpiringUploadsCountAsync(CancellationToken cancellationToken)
    {
        var values = await _dashboardDataService.GetApprovedUploadValidityDatesAsync(cancellationToken);
        return CountExpiringDates(values, DateTimeOffset.UtcNow);
    }

    private async Task<int> GetIncompleteSupplierCountAsync(CancellationToken cancellationToken)
    {
        return await _dashboardDataService.GetIncompleteSupplierCountAsync(
            SupplierActiveStatuses,
            RequiredComplianceDocumentKeys,
            cancellationToken);
    }

    private async Task<SupplierSummaryContext?> BuildSupplierSummaryContextAsync(int supplierId, CancellationToken cancellationToken)
    {
        var supplier = await _dashboardDataService.GetSupplierAsync(supplierId, cancellationToken);

        if (supplier == null)
        {
            return null;
        }

        var documents = await _dashboardDataService.GetSupplierDocumentsAsync(supplierId, cancellationToken);
        var whitelist = await _dashboardDataService.GetWhitelistedDocumentTypesAsync(supplierId, cancellationToken);
        var summary = BuildComplianceSummary(supplier, documents, whitelist);

        return new SupplierSummaryContext(supplier, documents, summary);
    }
}
