using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public static class SqlServerHelper
{
    public static async Task<DbConnection> OpenConnectionAsync(
        SupplierSystemDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connectionString = dbContext.Database.GetConnectionString()
            ?? dbContext.Database.GetDbConnection().ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public static List<Dictionary<string, object?>> ReadAll(DbCommand command)
    {
        using var reader = command.ExecuteReader();
        var results = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            results.Add(ReadRow(reader));
        }

        return results;
    }

    public static Dictionary<string, object?>? ReadSingle(DbCommand command)
    {
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return ReadRow(reader);
    }

    public static object? ReadScalar(DbCommand command)
    {
        return command.ExecuteScalar();
    }

    private static Dictionary<string, object?> ReadRow(DbDataReader reader)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            result[name] = value;
        }

        return result;
    }
}
