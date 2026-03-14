using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class UserDirectoryService
{
    private readonly SupplierSystemDbContext _dbContext;

    public UserDirectoryService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<User>> ListUsersAsync(string? role, bool includeSuppliers, string[] supplierRoles, CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (!includeSuppliers)
        {
            query = query.Where(u => !supplierRoles.Contains(u.Role));
        }

        return await query
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
    }
}
