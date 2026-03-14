using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Controllers;

public sealed partial class DashboardController
{
    private async Task<Dictionary<string, object?>> ResolveStatsAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var stats = new Dictionary<string, object?>();

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            return stats;
        }

        switch (user.Role)
        {
            case "temp_supplier":
            case "formal_supplier":
                if (user.SupplierId.HasValue)
                {
                    var summary = await BuildSupplierSummaryContextAsync(user.SupplierId.Value, cancellationToken);
                    if (summary != null)
                    {
                        stats["supplierStatus"] = summary.Supplier.Status ?? "unknown";
                        stats["missingProfileFields"] = summary.Summary.MissingProfileFields;
                        stats["missingProfileFieldCount"] = summary.Summary.MissingProfileFields.Count;
                        stats["documentsUploaded"] = summary.Documents.Count;
                        stats["missingDocumentTypes"] = summary.Summary.MissingDocumentTypes;
                        stats["missingDocumentCount"] = summary.Summary.MissingDocumentTypes.Count;
                        stats["pendingRfqs"] = await GetPendingRfqCountAsync(user.SupplierId.Value, cancellationToken);
                        stats["expiringDocuments"] = CountExpiringDates(summary.Documents.Select(d => d.ExpiresAt), DateTimeOffset.UtcNow);
                    }
                }
                break;
            case "purchaser":
            case "procurement_manager":
            case "procurement_director":
                stats["totalSuppliers"] = await SafeCountAsync(
                    () => _dashboardDataService.CountSuppliersAsync(cancellationToken));
                stats["activeRfqs"] = await SafeCountAsync(
                    () => _dashboardDataService.CountRfqsByStatusesAsync(new[] { "published", "in_progress" }, cancellationToken));
                stats["receivedQuotes"] = await SafeCountAsync(
                    () => _dashboardDataService.CountQuotesByStatusAsync("submitted", cancellationToken));
                stats["pendingRequisitions"] = await SafeCountAsync(
                    () => _dashboardDataService.CountMaterialRequisitionsPendingAsync(cancellationToken));
                break;
            case "finance_accountant":
                stats["totalInvoices"] = await SafeCountAsync(
                    () => _dashboardDataService.CountInvoicesAsync(cancellationToken));
                stats["pendingInvoices"] = await SafeCountAsync(
                    () => _dashboardDataService.CountInvoicesByStatusAsync("pending", cancellationToken));
                break;
            case "finance_director":
                stats["totalInvoices"] = await SafeCountAsync(
                    () => _dashboardDataService.CountInvoicesAsync(cancellationToken));
                stats["pendingDirectorApprovals"] = await SafeCountAsync(
                    () => _dashboardDataService.CountInvoicesByStatusAsync("pending_director_approval", cancellationToken));
                break;
            default:
                break;
        }

        return stats;
    }
}
