using System.Data.Common;
using System.Linq;
using SupplierSystem.Api.Services;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Files;

public sealed class SupplierFileRepository
{
    private readonly SupplierSystemDbContext _dbContext;

    public SupplierFileRepository(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> CreateFileAsync(SupplierFileRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO files (
  agreementNumber, fileType, validFrom, validTo,
  supplier_id, status, uploadTime, uploaderName,
  originalName, storedName
) VALUES (
  @agreementNumber, @fileType, @validFrom, @validTo,
  @supplierId, @status, @uploadTime, @uploaderName,
  @originalName, @storedName
);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameter(command, "@agreementNumber", record.AgreementNumber);
        AddParameter(command, "@fileType", record.FileType);
        AddParameter(command, "@validFrom", record.ValidFrom);
        AddParameter(command, "@validTo", record.ValidTo);
        AddParameter(command, "@supplierId", record.SupplierId);
        AddParameter(command, "@status", record.Status);
        AddParameter(command, "@uploadTime", record.UploadTime);
        AddParameter(command, "@uploaderName", record.UploaderName);
        AddParameter(command, "@originalName", record.OriginalName);
        AddParameter(command, "@storedName", record.StoredName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    public async Task<SupplierFileRecord?> GetFileByIdAsync(int fileId, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, agreementNumber, fileType, validFrom, validTo, supplier_id, status,
       uploadTime, uploaderName, originalName, storedName
FROM files
WHERE id = @id;";
        AddParameter(command, "@id", fileId);
        var row = SqlServerHelper.ReadSingle(command);
        return row == null ? null : MapFile(row);
    }

    public async Task<bool> DeleteFileAsync(int fileId, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM files WHERE id = @id;";
        AddParameter(command, "@id", fileId);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<List<SupplierFileRecord>> ListFilesBySupplierAsync(
        int supplierId,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, agreementNumber, fileType, validFrom, validTo, supplier_id, status,
       uploadTime, uploaderName, originalName, storedName
FROM files
WHERE supplier_id = @supplierId
ORDER BY uploadTime DESC;";
        AddParameter(command, "@supplierId", supplierId);
        var rows = SqlServerHelper.ReadAll(command);
        return rows.Select(MapFile).ToList();
    }

    private static SupplierFileRecord MapFile(Dictionary<string, object?> row)
    {
        return new SupplierFileRecord
        {
            Id = GetInt(row, "id"),
            AgreementNumber = GetString(row, "agreementNumber"),
            FileType = GetString(row, "fileType"),
            ValidFrom = GetString(row, "validFrom"),
            ValidTo = GetString(row, "validTo"),
            SupplierId = GetInt(row, "supplier_id"),
            Status = GetString(row, "status"),
            UploadTime = GetString(row, "uploadTime"),
            UploaderName = GetString(row, "uploaderName"),
            OriginalName = GetString(row, "originalName"),
            StoredName = GetString(row, "storedName")
        };
    }

    private static int GetInt(Dictionary<string, object?> row, string key)
    {
        return GetNullableInt(row, key) ?? 0;
    }

    private static int? GetNullableInt(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            decimal decimalValue => (int)decimalValue,
            double doubleValue => (int)doubleValue,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private static string? GetString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DateTimeOffset offsetValue)
        {
            return offsetValue.ToString("o");
        }

        if (value is DateTime dateValue)
        {
            return new DateTimeOffset(dateValue).ToString("o");
        }

        return value.ToString();
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
