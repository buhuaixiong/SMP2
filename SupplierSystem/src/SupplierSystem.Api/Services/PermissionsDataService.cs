using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class PermissionsDataService
{
    private readonly SupplierSystemDbContext _dbContext;

    public PermissionsDataService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<User> QueryUsers(bool asNoTracking = true)
    {
        var query = _dbContext.Users.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<Supplier> QuerySuppliers(bool asNoTracking = true)
    {
        var query = _dbContext.Suppliers.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<BuyerSupplierAssignment> QueryBuyerSupplierAssignments(bool asNoTracking = true)
    {
        var query = _dbContext.BuyerSupplierAssignments.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<BuyerSupplierPermission> QueryBuyerSupplierPermissions(bool asNoTracking = true)
    {
        var query = _dbContext.BuyerSupplierPermissions.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<ActiveSession> QueryActiveSessions(bool asNoTracking = true)
    {
        var query = _dbContext.ActiveSessions.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<TokenBlacklistEntry> QueryTokenBlacklist(bool asNoTracking = true)
    {
        var query = _dbContext.TokenBlacklist.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public void AddUser(User user) => _dbContext.Users.Add(user);
    public void AddBuyerSupplierAssignment(BuyerSupplierAssignment assignment) => _dbContext.BuyerSupplierAssignments.Add(assignment);
    public void AddBuyerSupplierPermission(BuyerSupplierPermission permission) => _dbContext.BuyerSupplierPermissions.Add(permission);
    public void RemoveBuyerSupplierAssignment(BuyerSupplierAssignment assignment) => _dbContext.BuyerSupplierAssignments.Remove(assignment);
    public void RemoveBuyerSupplierAssignments(IEnumerable<BuyerSupplierAssignment> assignments) => _dbContext.BuyerSupplierAssignments.RemoveRange(assignments);
    public void RemoveBuyerSupplierPermissions(IEnumerable<BuyerSupplierPermission> permissions) => _dbContext.BuyerSupplierPermissions.RemoveRange(permissions);
    public void RemoveActiveSessions(IEnumerable<ActiveSession> sessions) => _dbContext.ActiveSessions.RemoveRange(sessions);
    public void RemoveTokenBlacklistEntries(IEnumerable<TokenBlacklistEntry> entries) => _dbContext.TokenBlacklist.RemoveRange(entries);
    public void RemoveUser(User user) => _dbContext.Users.Remove(user);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public Task<User?> FindUserWithUpdateLockAsync(string userId, string username, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .FromSqlRaw(@"SELECT * FROM users WITH (UPDLOCK, ROWLOCK) WHERE id = {0} OR username = {1}", userId, username)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<User?> FindUserByEmailWithUpdateLockAsync(string email, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .FromSqlRaw(@"SELECT * FROM users WITH (UPDLOCK, ROWLOCK) WHERE email = {0}", email)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
