using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class BuyerAssignmentDataService
{
    private readonly SupplierSystemDbContext _dbContext;

    public BuyerAssignmentDataService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<User> QueryUsers(bool asNoTracking = true)
    {
        var query = _dbContext.Users.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<SupplierTag> QuerySupplierTags(bool asNoTracking = true)
    {
        var query = _dbContext.SupplierTags.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<Supplier> QuerySuppliers(bool asNoTracking = true)
    {
        var query = _dbContext.Suppliers.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<BuyerSupplierAssignment> QueryAssignments(bool asNoTracking = true)
    {
        var query = _dbContext.BuyerSupplierAssignments.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public void AddAssignment(BuyerSupplierAssignment assignment) => _dbContext.BuyerSupplierAssignments.Add(assignment);
    public void RemoveAssignment(BuyerSupplierAssignment assignment) => _dbContext.BuyerSupplierAssignments.Remove(assignment);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}
