using Microsoft.EntityFrameworkCore;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class SystemHealthService
{
    private readonly SupplierSystemDbContext _dbContext;

    public SystemHealthService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.CanConnectAsync(cancellationToken);
    }

    public async Task<Dictionary<string, object?>> GetCoreEntityCountsAsync(CancellationToken cancellationToken)
    {
        var stats = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            stats["suppliers"] = await _dbContext.Suppliers.CountAsync(cancellationToken);
            stats["users"] = await _dbContext.Users.CountAsync(cancellationToken);
            stats["rfqs"] = await _dbContext.Rfqs.CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            stats["error"] = ex.Message;
        }

        return stats;
    }
}
