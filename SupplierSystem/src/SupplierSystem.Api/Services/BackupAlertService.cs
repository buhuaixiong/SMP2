using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class BackupAlertService
{
    private readonly SupplierSystemDbContext _dbContext;

    public BackupAlertService(SupplierSystemDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<BackupAlert>> GetUnresolvedAsync(int limit, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        return await _dbContext.BackupAlerts
            .AsNoTracking()
            .Where(a => a.ResolvedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ResolveAsync(int id, string resolvedBy, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        var alert = await _dbContext.BackupAlerts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (alert is null) return false;

        alert.ResolvedAt = DateTimeOffset.UtcNow.ToString("o");
        alert.ResolvedBy = resolvedBy;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RecordAsync(string level, string type, string message, object? details, bool emailSent, bool windowsLogWritten, bool auditLogged, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        _dbContext.BackupAlerts.Add(new BackupAlert
        {
            AlertLevel = level,
            AlertType = type,
            Message = message,
            Details = details is null ? null : System.Text.Json.JsonSerializer.Serialize(details),
            EmailSent = emailSent,
            WindowsLogWritten = windowsLogWritten,
            AuditLogged = auditLogged,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
        => await _dbContext.Database.ExecuteSqlRawAsync(Sql.CreateAlertsTable, cancellationToken);
}

file static class Sql
{
    public const string CreateAlertsTable = @"
IF OBJECT_ID('backup_alerts', 'U') IS NULL
BEGIN
    CREATE TABLE backup_alerts (
        id INT IDENTITY(1,1) PRIMARY KEY,
        alert_level NVARCHAR(50) NOT NULL,
        alert_type NVARCHAR(100) NOT NULL,
        message NVARCHAR(MAX) NOT NULL,
        details NVARCHAR(MAX) NULL,
        email_sent INT DEFAULT 0,
        windows_log_written INT DEFAULT 0,
        audit_logged INT DEFAULT 0,
        created_at NVARCHAR(64) NOT NULL,
        resolved_at NVARCHAR(64) NULL,
        resolved_by NVARCHAR(128) NULL
    )
END";
}
