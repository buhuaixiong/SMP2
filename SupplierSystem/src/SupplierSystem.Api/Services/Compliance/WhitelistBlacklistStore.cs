using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Compliance;

public sealed class WhitelistBlacklistStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public WhitelistBlacklistStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<Supplier> QuerySuppliers()
    {
        return _dbContext.Suppliers.AsNoTracking();
    }

    public IQueryable<WhitelistExemptionRow> QueryExemptions()
    {
        return from exemption in _dbContext.SupplierDocumentWhitelists.AsNoTracking()
               join supplier in _dbContext.Suppliers.AsNoTracking()
                   on exemption.SupplierId equals supplier.Id into supplierGroup
               from supplier in supplierGroup.DefaultIfEmpty()
               select new WhitelistExemptionRow
               {
                   Exemption = exemption,
                   SupplierName = supplier != null ? supplier.CompanyName : null,
                   SupplierCode = supplier != null ? supplier.CompanyId : null,
                   SupplierStage = supplier != null ? supplier.Stage : null,
               };
    }

    public Task<Supplier?> FindSupplierAsync(int supplierId, CancellationToken cancellationToken)
    {
        return _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
    }

    public Task<SupplierDocumentWhitelist?> FindExemptionBySupplierAndTypeAsync(int supplierId, string documentType, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierDocumentWhitelists
            .FirstOrDefaultAsync(item => item.SupplierId == supplierId && item.DocumentType == documentType, cancellationToken);
    }

    public Task<SupplierDocumentWhitelist?> FindExemptionAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierDocumentWhitelists
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public void AddExemption(SupplierDocumentWhitelist exemption)
    {
        _dbContext.SupplierDocumentWhitelists.Add(exemption);
    }

    public void RemoveExemption(SupplierDocumentWhitelist exemption)
    {
        _dbContext.SupplierDocumentWhitelists.Remove(exemption);
    }

    public IQueryable<SupplierRegistrationBlacklist> QueryBlacklistEntries()
    {
        return _dbContext.SupplierRegistrationBlacklist.AsNoTracking();
    }

    public Task<bool> BlacklistEntryExistsAsync(string type, string value, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierRegistrationBlacklist.AnyAsync(entry =>
            entry.BlacklistType == type && entry.BlacklistValue == value, cancellationToken);
    }

    public void AddBlacklistEntry(SupplierRegistrationBlacklist entry)
    {
        _dbContext.SupplierRegistrationBlacklist.Add(entry);
    }

    public Task<SupplierRegistrationBlacklist?> FindBlacklistEntryAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierRegistrationBlacklist
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public void RemoveBlacklistEntry(SupplierRegistrationBlacklist entry)
    {
        _dbContext.SupplierRegistrationBlacklist.Remove(entry);
    }

    public Task<SupplierRegistrationBlacklist?> FindActiveBlacklistEntryAsync(string type, string value, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierRegistrationBlacklist.AsNoTracking()
            .Where(entry => entry.BlacklistType == type && entry.BlacklistValue == value && entry.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}

public sealed class WhitelistExemptionRow
{
    public required SupplierDocumentWhitelist Exemption { get; init; }
    public string? SupplierName { get; init; }
    public string? SupplierCode { get; init; }
    public string? SupplierStage { get; init; }
}
