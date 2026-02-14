using System.Data;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class DashboardController
{
    private async Task<int> GetPendingRfqCountAsync(int supplierId, CancellationToken cancellationToken)
    {
        if (!await EnsureTablesAsync(new[] { "supplier_rfq_invitations", "quotes" }, cancellationToken))
        {
            return 0;
        }

        try
        {
            var count = await (
                from invitation in _context.SupplierRfqInvitations.AsNoTracking()
                where invitation.SupplierId == supplierId
                where invitation.Status == null || !SupplierDeclinedInvitationStatuses.Contains(invitation.Status.ToLower())
                join quote in _context.Quotes.AsNoTracking().Where(q => q.SupplierId == supplierId && q.IsLatest)
                    on invitation.RfqId equals quote.RfqId into quoteGroup
                from quote in quoteGroup.DefaultIfEmpty()
                where quote == null || quote.Status == null || SupplierPendingQuoteStatuses.Contains(quote.Status.ToLower())
                select invitation.Id
            ).CountAsync(cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count pending RFQs for supplier {SupplierId}", supplierId);
            return 0;
        }
    }

    private async Task<int> GetExpiringUploadsCountAsync(CancellationToken cancellationToken)
    {
        if (!await EnsureTablesAsync(new[] { "supplier_file_uploads" }, cancellationToken))
        {
            return 0;
        }

        try
        {
            var values = await _context.SupplierFileUploads.AsNoTracking()
                .Where(u => u.Status == "approved" && u.ValidTo != null)
                .Select(u => u.ValidTo)
                .ToListAsync(cancellationToken);

            return CountExpiringDates(values, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count expiring supplier file uploads");
            return 0;
        }
    }

    private async Task<int> GetIncompleteSupplierCountAsync(CancellationToken cancellationToken)
    {
        if (!await EnsureTablesAsync(new[] { "suppliers", "supplier_documents" }, cancellationToken))
        {
            return 0;
        }

        try
        {
            var incompleteSuppliers = await _context.Suppliers.AsNoTracking()
                .Where(s => s.Status != null && SupplierActiveStatuses.Contains(s.Status.ToLower()))
                .Where(s =>
                    _context.SupplierDocuments.AsNoTracking()
                        .Where(d => d.SupplierId == s.Id && d.DocType != null)
                        .Select(d => d.DocType!.ToLower())
                        .Where(docType => RequiredComplianceDocumentKeys.Contains(docType))
                        .Distinct()
                        .Count() < RequiredComplianceDocumentKeys.Length)
                .CountAsync(cancellationToken);

            return incompleteSuppliers;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count incomplete suppliers");
            return 0;
        }
    }

    private async Task<SupplierSummaryContext?> BuildSupplierSummaryContextAsync(int supplierId, CancellationToken cancellationToken)
    {
        if (!await EnsureTablesAsync(new[] { "suppliers" }, cancellationToken))
        {
            return null;
        }

        Supplier? supplier;
        try
        {
            supplier = await _context.Suppliers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load supplier {SupplierId} for dashboard", supplierId);
            return null;
        }

        if (supplier == null)
        {
            return null;
        }

        var documents = new List<SupplierDocument>();
        if (await EnsureTablesAsync(new[] { "supplier_documents" }, cancellationToken))
        {
            try
            {
                documents = await _context.SupplierDocuments.AsNoTracking()
                    .Where(d => d.SupplierId == supplierId)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load supplier documents for supplier {SupplierId}", supplierId);
                documents = new List<SupplierDocument>();
            }
        }

        var whitelist = await GetWhitelistedDocumentTypesAsync(supplierId, cancellationToken);
        var summary = BuildComplianceSummary(supplier, documents, whitelist);

        return new SupplierSummaryContext(supplier, documents, summary);
    }

    private async Task<IReadOnlyList<string>> GetWhitelistedDocumentTypesAsync(int supplierId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync("supplier_document_whitelist", cancellationToken))
        {
            return Array.Empty<string>();
        }

        var provider = _context.Database.ProviderName ?? string.Empty;
        var expirationSql = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            ? "expires_at IS NULL OR expires_at > CURRENT_TIMESTAMP"
            : "expires_at IS NULL OR expires_at > SYSUTCDATETIME()";
        var sql = $"SELECT document_type FROM supplier_document_whitelist WHERE supplier_id = @supplierId AND is_active = 1 AND ({expirationSql})";

        var results = new List<string>();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@supplierId";
            parameter.Value = supplierId;
            command.Parameters.Add(parameter);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    results.Add(reader.GetString(0));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load supplier document whitelist for supplier {SupplierId}", supplierId);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return results;
    }

    private async Task<int> SafeCountAsync(
        IReadOnlyList<string> tables,
        Func<Task<int>> countAction,
        CancellationToken cancellationToken)
    {
        if (tables.Count > 0 && !await EnsureTablesAsync(tables, cancellationToken))
        {
            return 0;
        }

        try
        {
            return await countAction();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard count query failed");
            return 0;
        }
    }

    private async Task<bool> EnsureTablesAsync(IReadOnlyList<string> tables, CancellationToken cancellationToken)
    {
        foreach (var table in tables)
        {
            if (!await TableExistsAsync(table, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        if (TableExistenceCache.TryGetValue(tableName, out var exists))
        {
            return exists;
        }

        var provider = _context.Database.ProviderName ?? string.Empty;
        if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            TableExistenceCache[tableName] = true;
            return true;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            using var command = connection.CreateCommand();
            command.CommandText = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
                ? "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @table"
                : "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @table";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@table";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            exists = result != null && result != DBNull.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check table existence for {TableName}", tableName);
            exists = false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        TableExistenceCache[tableName] = exists;
        return exists;
    }
}
