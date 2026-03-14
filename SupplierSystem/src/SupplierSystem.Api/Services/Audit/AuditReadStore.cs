using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Audit;

public sealed class AuditReadStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public AuditReadStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<AuditLog> QueryAuditLogs(bool asNoTracking = true)
    {
        var query = _dbContext.AuditLogs.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public Task<AuditLog?> FindAuditLogAsync(int id, CancellationToken cancellationToken)
    {
        return QueryAuditLogs().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }
}
