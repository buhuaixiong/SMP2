using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class CountryFreightRateDataService
{
    private readonly SupplierSystemDbContext _dbContext;

    public CountryFreightRateDataService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CountryRateRow>> LoadCountryRatesAsync(CancellationToken cancellationToken)
    {
        var rows = new List<CountryRateRow>();
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = false;

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                shouldClose = true;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT country_code, country_name, country_name_zh, product_group, rate_2025, rate_2024, rate_2023, is_active
FROM tariff_rates
WHERE product_group != 'FREIGHT' AND is_active = 1
ORDER BY country_code, product_group";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new CountryRateRow
                {
                    CountryCode = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    CountryName = reader.IsDBNull(1) ? null : reader.GetString(1),
                    CountryNameZh = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ProductGroup = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Rate2025 = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4)),
                    Rate2024 = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5)),
                    Rate2023 = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6)),
                    IsActive = !reader.IsDBNull(7) && reader.GetInt32(7) == 1,
                });
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return rows;
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertCountryRateAsync(CountryFreightRateUpdate update, int year, string rateColumn, CancellationToken cancellationToken)
    {
        var countryName = string.IsNullOrWhiteSpace(update.CountryName) ? update.CountryCode : update.CountryName;
        var countryNameZh = update.CountryNameZh;

        var updateSql = $@"
UPDATE tariff_rates
SET {rateColumn} = @rate,
    country_name = COALESCE(@name, country_name),
    country_name_zh = COALESCE(@nameZh, country_name_zh),
    is_active = 1
WHERE country_code = @code AND product_group = @group";

        var updateParams = new[]
        {
            new SqlParameter("@rate", update.Rate),
            new SqlParameter("@name", (object?)countryName ?? DBNull.Value),
            new SqlParameter("@nameZh", (object?)countryNameZh ?? DBNull.Value),
            new SqlParameter("@code", update.CountryCode),
            new SqlParameter("@group", update.ProductGroup),
        };

        var affected = await _dbContext.Database.ExecuteSqlRawAsync(updateSql, updateParams, cancellationToken);
        if (affected > 0)
        {
            return;
        }

        var insertSql = @"
INSERT INTO tariff_rates
    (country_code, country_name, country_name_zh, product_group, is_active, rate_2025, rate_2024, rate_2023)
VALUES
    (@code, @name, @nameZh, @group, 1, @rate2025, @rate2024, @rate2023)";

        var rate2025 = year == 2025 ? update.Rate : 0m;
        var rate2024 = year == 2024 ? update.Rate : 0m;
        var rate2023 = year == 2023 ? update.Rate : 0m;

        var insertParams = new[]
        {
            new SqlParameter("@code", update.CountryCode),
            new SqlParameter("@name", (object?)countryName ?? DBNull.Value),
            new SqlParameter("@nameZh", (object?)countryNameZh ?? DBNull.Value),
            new SqlParameter("@group", update.ProductGroup),
            new SqlParameter("@rate2025", rate2025),
            new SqlParameter("@rate2024", rate2024),
            new SqlParameter("@rate2023", rate2023),
        };

        await _dbContext.Database.ExecuteSqlRawAsync(insertSql, insertParams, cancellationToken);
    }
}

public sealed class CountryRateRow
{
    public string CountryCode { get; set; } = string.Empty;
    public string? CountryName { get; set; }
    public string? CountryNameZh { get; set; }
    public string ProductGroup { get; set; } = string.Empty;
    public decimal Rate2025 { get; set; }
    public decimal Rate2024 { get; set; }
    public decimal Rate2023 { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CountryFreightRateUpdate
{
    public string CountryCode { get; set; } = string.Empty;
    public string ProductGroup { get; set; } = string.Empty;
    public string? CountryName { get; set; }
    public string? CountryNameZh { get; set; }
    public decimal Rate { get; set; }
}
