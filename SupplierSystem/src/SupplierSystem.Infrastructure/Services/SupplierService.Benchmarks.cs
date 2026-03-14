using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.DTOs.Suppliers;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 基准数据

    public async Task<SupplierBenchmarksResponse> GetBenchmarksAsync(CancellationToken cancellationToken)
    {
        var total = await _context.Suppliers.CountAsync(cancellationToken);
        var approved = await _context.Suppliers.CountAsync(s => s.Status == "approved", cancellationToken);
        var avgCompletion = await _context.Suppliers.AverageAsync(s => s.CompletionScore ?? 0, cancellationToken);

        return new SupplierBenchmarksResponse
        {
            TotalSuppliers = total,
            ApprovedSuppliers = approved,
            ApprovalRate = total > 0 ? Math.Round((double)approved / total * 100, 1) : 0,
            AverageCompletionScore = (double)Math.Round(avgCompletion, 1),
            ByStatus = new SupplierBenchmarkStatusBreakdown
            {
                Pending = await _context.Suppliers.CountAsync(s => s.Status == "pending", cancellationToken),
                Approved = approved,
                Rejected = await _context.Suppliers.CountAsync(s => s.Status == "rejected", cancellationToken),
                Blocked = await _context.Suppliers.CountAsync(s => s.Status == "blocked", cancellationToken)
            },
            ByCategory = new List<SupplierBenchmarkBucket>(),
            ByRegion = new List<SupplierBenchmarkBucket>()
        };
    }

    #endregion
}
