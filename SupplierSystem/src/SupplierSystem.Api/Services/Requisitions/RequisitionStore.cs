using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Requisitions;

public sealed class RequisitionStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public RequisitionStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<MaterialRequisition> QueryRequisitions(bool asNoTracking = true)
    {
        var query = _dbContext.MaterialRequisitions.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<MaterialRequisitionItem> QueryItems(bool asNoTracking = true)
    {
        var query = _dbContext.MaterialRequisitionItems.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<MaterialRequisitionAttachment> QueryAttachments(bool asNoTracking = true)
    {
        var query = _dbContext.MaterialRequisitionAttachments.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<User> QueryUsers(bool asNoTracking = true)
    {
        var query = _dbContext.Users.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public void AddRequisition(MaterialRequisition requisition) => _dbContext.MaterialRequisitions.Add(requisition);
    public void AddItem(MaterialRequisitionItem item) => _dbContext.MaterialRequisitionItems.Add(item);
    public void AddAttachment(MaterialRequisitionAttachment attachment) => _dbContext.MaterialRequisitionAttachments.Add(attachment);
    public void UpdateRequisition(MaterialRequisition requisition) => _dbContext.MaterialRequisitions.Update(requisition);
    public void RemoveItems(IEnumerable<MaterialRequisitionItem> items) => _dbContext.MaterialRequisitionItems.RemoveRange(items);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}
