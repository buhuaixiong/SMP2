using Microsoft.EntityFrameworkCore;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 基准数据

    public async Task<object> GetBenchmarksAsync(CancellationToken cancellationToken)
    {
        // 获取供应商统计基准数据
        var total = await _context.Suppliers.CountAsync(cancellationToken);
        var approved = await _context.Suppliers.CountAsync(s => s.Status == "approved", cancellationToken);
        var avgCompletion = await _context.Suppliers.AverageAsync(s => s.CompletionScore ?? 0, cancellationToken);

        return new
        {
            totalSuppliers = total,
            approvedSuppliers = approved,
            approvalRate = total > 0 ? Math.Round((double)approved / total * 100, 1) : 0,
            averageCompletionScore = Math.Round(avgCompletion, 1),
            byStatus = new
            {
                pending = await _context.Suppliers.CountAsync(s => s.Status == "pending", cancellationToken),
                approved = approved,
                rejected = await _context.Suppliers.CountAsync(s => s.Status == "rejected", cancellationToken),
                blocked = await _context.Suppliers.CountAsync(s => s.Status == "blocked", cancellationToken)
            },
            byCategory = new object[0],
            byRegion = new object[0]
        };
    }

    #endregion
}
