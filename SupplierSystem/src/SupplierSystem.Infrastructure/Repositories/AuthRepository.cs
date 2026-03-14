using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Repositories;

public sealed class AuthRepository : IAuthRepository
{
    private readonly SupplierSystemDbContext _dbContext;

    public AuthRepository(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        var normalized = identifier.Trim().ToLower();
        var supplierId = int.TryParse(identifier, out var parsed) ? parsed : (int?)null;

        return await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user =>
                user.Id == identifier ||
                user.Id.ToLower() == normalized ||
                (supplierId.HasValue && user.SupplierId == supplierId.Value) ||
                user.Name.ToLower() == normalized ||
                user.Username.ToLower() == normalized ||
                (user.Email != null && user.Email.ToLower() == normalized),
            cancellationToken);
    }

    public async Task<User?> FindByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalized = id.Trim().ToLower();
        return await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id.ToLower() == normalized, cancellationToken);
    }
}
