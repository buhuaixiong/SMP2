using System.Data.Common;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Files;

public sealed class FileAccessTokenStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public FileAccessTokenStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InsertAsync(
        string token,
        string userId,
        string resourceType,
        string resourceId,
        string category,
        string storagePath,
        string? originalName,
        long? fileSize,
        string? mimeType,
        DateTimeOffset expiresAt,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO file_access_tokens (
  token, userId, resourceType, resourceId, category, storagePath,
  originalName, fileSize, mimeType, expiresAt, ipAddress
) VALUES (
  @token, @userId, @resourceType, @resourceId, @category, @storagePath,
  @originalName, @fileSize, @mimeType, @expiresAt, @ipAddress
);";
        AddParameter(command, "@token", token);
        AddParameter(command, "@userId", userId);
        AddParameter(command, "@resourceType", resourceType);
        AddParameter(command, "@resourceId", resourceId);
        AddParameter(command, "@category", category);
        AddParameter(command, "@storagePath", storagePath);
        AddParameter(command, "@originalName", originalName);
        AddParameter(command, "@fileSize", fileSize);
        AddParameter(command, "@mimeType", mimeType);
        AddParameter(command, "@expiresAt", expiresAt.UtcDateTime);
        AddParameter(command, "@ipAddress", ipAddress);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<FileAccessTokenStoreRecord?> ConsumeAsync(
        string token,
        string resourceType,
        string resourceId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);

        await using var select = connection.CreateCommand();
        select.CommandText = @"
SELECT token, userId, resourceType, resourceId, category, storagePath,
       originalName, fileSize, mimeType, expiresAt
FROM file_access_tokens
WHERE token = @token AND resourceType = @resourceType AND resourceId = @resourceId AND usedAt IS NULL;";
        AddParameter(select, "@token", token);
        AddParameter(select, "@resourceType", resourceType);
        AddParameter(select, "@resourceId", resourceId);

        var row = SqlServerHelper.ReadSingle(select);
        if (row == null)
        {
            return null;
        }

        var expiresAt = GetDateTimeOffset(row, "expiresAt");
        if (expiresAt.HasValue && expiresAt.Value < DateTimeOffset.UtcNow)
        {
            return null;
        }

        await using var update = connection.CreateCommand();
        update.CommandText = @"
UPDATE file_access_tokens
SET usedAt = SYSUTCDATETIME(), usedIpAddress = @usedIpAddress
WHERE token = @token AND usedAt IS NULL;";
        AddParameter(update, "@token", token);
        AddParameter(update, "@usedIpAddress", ipAddress);

        var updated = await update.ExecuteNonQueryAsync(cancellationToken);
        if (updated == 0)
        {
            return null;
        }

        return new FileAccessTokenStoreRecord(
            GetString(row, "token") ?? token,
            GetString(row, "userId") ?? string.Empty,
            GetString(row, "resourceType") ?? resourceType,
            GetString(row, "resourceId") ?? resourceId,
            GetString(row, "category") ?? string.Empty,
            GetString(row, "storagePath") ?? string.Empty,
            GetString(row, "originalName"),
            GetLong(row, "fileSize"),
            GetString(row, "mimeType"),
            expiresAt ?? DateTimeOffset.UtcNow);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? GetString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value.ToString();
    }

    private static long? GetLong(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue => (long)decimalValue,
            _ => long.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DateTimeOffset offsetValue)
        {
            return offsetValue;
        }

        if (value is DateTime dateValue)
        {
            return new DateTimeOffset(dateValue, TimeSpan.Zero);
        }

        return DateTimeOffset.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }
}

public sealed record FileAccessTokenStoreRecord(
    string Token,
    string UserId,
    string ResourceType,
    string ResourceId,
    string Category,
    string StoragePath,
    string? OriginalName,
    long? FileSize,
    string? MimeType,
    DateTimeOffset ExpiresAt);
