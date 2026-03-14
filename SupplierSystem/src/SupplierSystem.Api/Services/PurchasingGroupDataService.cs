using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class PurchasingGroupDataService
{
    private readonly SupplierSystemDbContext _dbContext;

    public PurchasingGroupDataService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<PurchasingGroup> QueryGroups(bool asNoTracking = true)
    {
        var query = _dbContext.PurchasingGroups.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<PurchasingGroupMember> QueryGroupMembers(bool asNoTracking = true)
    {
        var query = _dbContext.PurchasingGroupMembers.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<PurchasingGroupSupplier> QueryGroupSuppliers(bool asNoTracking = true)
    {
        var query = _dbContext.PurchasingGroupSuppliers.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<BuyerSupplierAssignment> QueryBuyerSupplierAssignments(bool asNoTracking = true)
    {
        var query = _dbContext.BuyerSupplierAssignments.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<BuyerSupplierAccessCache> QueryAccessCache(bool asNoTracking = true)
    {
        var query = _dbContext.BuyerSupplierAccessCaches.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
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

    public void AddGroup(PurchasingGroup group) => _dbContext.PurchasingGroups.Add(group);
    public void AddGroupMember(PurchasingGroupMember member) => _dbContext.PurchasingGroupMembers.Add(member);
    public void AddGroupSupplier(PurchasingGroupSupplier supplier) => _dbContext.PurchasingGroupSuppliers.Add(supplier);
    public void AddBuyerSupplierAssignment(BuyerSupplierAssignment assignment) => _dbContext.BuyerSupplierAssignments.Add(assignment);
    public void AddAccessCacheEntries(IEnumerable<BuyerSupplierAccessCache> entries) => _dbContext.BuyerSupplierAccessCaches.AddRange(entries);
    public void RemoveGroupMember(PurchasingGroupMember member) => _dbContext.PurchasingGroupMembers.Remove(member);
    public void RemoveGroupSupplier(PurchasingGroupSupplier supplier) => _dbContext.PurchasingGroupSuppliers.Remove(supplier);
    public void RemoveBuyerSupplierAssignment(BuyerSupplierAssignment assignment) => _dbContext.BuyerSupplierAssignments.Remove(assignment);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public Task<int> ClearAccessCacheAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM buyer_supplier_access_cache", cancellationToken);
    }
}
