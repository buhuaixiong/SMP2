using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 历史记录

    public async Task<SupplierHistoryResponse> GetHistoryAsync(int supplierId, int limit, int offset, CancellationToken cancellationToken)
    {
        // 从审计日志获取历史记录
        var auditLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityType == "supplier" && a.EntityId == supplierId.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var total = await _context.AuditLogs
            .CountAsync(a => a.EntityType == "supplier" && a.EntityId == supplierId.ToString(), cancellationToken);

        var history = auditLogs.Select(a => new SupplierHistoryEntry
        {
            Id = a.Id,
            Timestamp = a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            Action = a.Action,
            Actor = a.ActorId,
            ActorName = a.ActorName,
            Changes = a.Changes
        }).ToList();

        return new SupplierHistoryResponse
        {
            History = history,
            Total = total
        };
    }

    #endregion
}
