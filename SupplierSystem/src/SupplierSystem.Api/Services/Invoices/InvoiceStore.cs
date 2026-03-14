using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Invoices;

public sealed class InvoiceStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public InvoiceStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<Invoice> QueryInvoices(bool asNoTracking = true)
    {
        var query = _dbContext.Invoices.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public Task<Supplier?> FindSupplierAsync(int supplierId, CancellationToken cancellationToken)
    {
        return _dbContext.Suppliers.FindAsync(new object[] { supplierId }, cancellationToken).AsTask();
    }

    public Task<Invoice?> FindInvoiceAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Invoices.FindAsync(new object[] { id }, cancellationToken).AsTask();
    }

    public Task<Invoice?> GetInvoiceWithSupplierAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Invoices
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public void AddInvoice(Invoice invoice)
    {
        _dbContext.Invoices.Add(invoice);
    }

    public void RemoveInvoice(Invoice invoice)
    {
        _dbContext.Invoices.Remove(invoice);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
