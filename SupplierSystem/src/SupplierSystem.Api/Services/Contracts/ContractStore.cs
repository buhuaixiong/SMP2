using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Contracts;

public sealed class ContractStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public ContractStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<Contract> QueryContracts()
    {
        return _dbContext.Contracts.AsNoTracking();
    }

    public Task<Contract?> FindContractAsync(int id, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.Contracts.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<bool> SupplierExistsAsync(int supplierId, CancellationToken cancellationToken)
    {
        return _dbContext.Suppliers.AnyAsync(s => s.Id == supplierId, cancellationToken);
    }

    public Task<bool> ContractAgreementExistsAsync(string agreementNumber, CancellationToken cancellationToken)
    {
        return _dbContext.Contracts.AnyAsync(c => c.AgreementNumber == agreementNumber, cancellationToken);
    }

    public void AddContract(Contract contract)
    {
        _dbContext.Contracts.Add(contract);
    }

    public void RemoveContract(Contract contract)
    {
        _dbContext.Contracts.Remove(contract);
    }

    public Task<List<ContractVersion>> LoadContractVersionsAsync(IReadOnlyCollection<int> contractIds, CancellationToken cancellationToken)
    {
        return _dbContext.ContractVersions.AsNoTracking()
            .Where(v => contractIds.Contains(v.ContractId))
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ContractVersion>> LoadContractVersionsAsync(int contractId, CancellationToken cancellationToken)
    {
        return _dbContext.ContractVersions.AsNoTracking()
            .Where(v => v.ContractId == contractId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetMaxVersionNumberAsync(int contractId, CancellationToken cancellationToken)
    {
        var max = await _dbContext.ContractVersions
            .Where(v => v.ContractId == contractId)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken);
        return max ?? 0;
    }

    public void AddContractVersion(ContractVersion version)
    {
        _dbContext.ContractVersions.Add(version);
    }

    public Task<ContractVersion?> FindContractVersionAsync(int contractId, int versionId, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.ContractVersions.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(v => v.Id == versionId && v.ContractId == contractId, cancellationToken);
    }

    public void RemoveContractVersion(ContractVersion version)
    {
        _dbContext.ContractVersions.Remove(version);
    }

    public Task<ContractReminderSetting?> FindGlobalReminderSettingAsync(bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.ContractReminderSettings.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(r => r.Scope == "global", cancellationToken);
    }

    public void AddReminderSetting(ContractReminderSetting setting)
    {
        _dbContext.ContractReminderSettings.Add(setting);
    }

    public Task<Dictionary<int, string?>> LoadSupplierNamesAsync(IReadOnlyCollection<int> supplierIds, CancellationToken cancellationToken)
    {
        return _dbContext.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (string?)s.CompanyName, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
