using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class ExchangeRateHistoryService
{
    private readonly SupplierSystemDbContext _dbContext;

    public ExchangeRateHistoryService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ExchangeRateHistory>> GetHistoryAsync(string? currency, int limit, CancellationToken cancellationToken)
    {
        var query = _dbContext.ExchangeRateHistories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var code = currency.Trim().ToUpperInvariant();
            query = query.Where(h => h.Currency == code);
        }

        return await query
            .OrderByDescending(h => h.EffectiveDate)
            .ThenByDescending(h => h.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetCurrenciesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.ExchangeRateHistories
            .AsNoTracking()
            .Select(h => h.Currency)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<int>> CreateRatesAsync(IEnumerable<ExchangeRateHistory> records, CancellationToken cancellationToken)
    {
        var recordList = records.ToList();
        _dbContext.ExchangeRateHistories.AddRange(recordList);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await _dbContext.ExchangeRateHistories
            .AsNoTracking()
            .OrderByDescending(h => h.Id)
            .Take(recordList.Count)
            .Select(h => h.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExchangeRateHistory?> FindByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.ExchangeRateHistories.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task DeleteAsync(ExchangeRateHistory record, CancellationToken cancellationToken)
    {
        _dbContext.ExchangeRateHistories.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
