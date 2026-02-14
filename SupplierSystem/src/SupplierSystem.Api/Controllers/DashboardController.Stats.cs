using Microsoft.EntityFrameworkCore;
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
                    new[] { "suppliers" },
                    () => _context.Suppliers.CountAsync(cancellationToken),
                    cancellationToken);
                stats["activeRfqs"] = await SafeCountAsync(
                    new[] { "rfqs" },
                    () => _context.Rfqs.CountAsync(r => r.Status == "published" || r.Status == "in_progress", cancellationToken),
                    cancellationToken);
                stats["receivedQuotes"] = await SafeCountAsync(
                    new[] { "quotes" },
                    () => _context.Quotes.CountAsync(q => q.Status == "submitted", cancellationToken),
                    cancellationToken);
                stats["pendingRequisitions"] = await SafeCountAsync(
                    new[] { "material_requisitions" },
                    () => _context.MaterialRequisitions.CountAsync(r => r.Status == "submitted" && (!r.ConvertedToRfqId.HasValue || r.ConvertedToRfqId == 0), cancellationToken),
                    cancellationToken);
                break;
            case "finance_accountant":
                stats["totalInvoices"] = await SafeCountAsync(
                    new[] { "invoices" },
                    () => _context.Invoices.CountAsync(cancellationToken),
                    cancellationToken);
                stats["pendingInvoices"] = await SafeCountAsync(
                    new[] { "invoices" },
                    () => _context.Invoices.CountAsync(i => i.Status == "pending", cancellationToken),
                    cancellationToken);
                break;
            case "finance_director":
                stats["totalInvoices"] = await SafeCountAsync(
                    new[] { "invoices" },
                    () => _context.Invoices.CountAsync(cancellationToken),
                    cancellationToken);
                stats["pendingDirectorApprovals"] = await SafeCountAsync(
                    new[] { "invoices" },
                    () => _context.Invoices.CountAsync(i => i.Status == "pending_director_approval", cancellationToken),
                    cancellationToken);
                break;
            default:
                break;
        }

        return stats;
    }
}
