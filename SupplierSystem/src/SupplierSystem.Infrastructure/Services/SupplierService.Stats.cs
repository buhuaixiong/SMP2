using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.DTOs.Suppliers;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 统计信息

    public async Task<SupplierStatsOverviewResponse> GetStatsOverviewAsync(CancellationToken cancellationToken)
    {
        var totalSuppliers = await _context.Suppliers.CountAsync(cancellationToken);
        var pendingApproval = await _context.Suppliers.CountAsync(s => s.Status == "pending", cancellationToken);
        var activeSuppliers = await _context.Suppliers.CountAsync(s => s.Status == "approved", cancellationToken);
        var blockedSuppliers = await _context.Suppliers.CountAsync(s => s.Status == "blocked" || s.Status == "rejected", cancellationToken);

        var now = DateTime.Now;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayStr = firstDayOfMonth.ToString("yyyy-MM-dd HH:mm:ss");

        var newThisMonth = await _context.Suppliers
            .CountAsync(s => s.CreatedAt != null && s.CreatedAt.CompareTo(firstDayStr) >= 0, cancellationToken);

        return new SupplierStatsOverviewResponse
        {
            TotalSuppliers = totalSuppliers,
            PendingApproval = pendingApproval,
            ActiveSuppliers = activeSuppliers,
            BlockedSuppliers = blockedSuppliers,
            NewThisMonth = newThisMonth
        };
    }

    public async Task<SupplierStatsResponse?> GetSupplierStatsAsync(int supplierId, CancellationToken cancellationToken)
    {
        // 检查是否有评分表
        var ratingsType = _context.Model.FindEntityType("SupplierSystem.Domain.Entities.SupplierRating");
        if (ratingsType == null)
            return null;

        // 动态查询评分统计
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    COUNT(*) AS totalEvaluations,
                    AVG(overallScore) AS overallAverage,
                    AVG(onTimeDelivery) AS avgOnTimeDelivery,
                    AVG(qualityScore) AS avgQualityScore,
                    AVG(serviceScore) AS avgServiceScore,
                    AVG(costScore) AS avgCostScore
                FROM supplier_ratings
                WHERE supplier_id = @supplierId";

            var param = command.CreateParameter();
            param.ParameterName = "@supplierId";
            param.Value = supplierId;
            command.Parameters.Add(param);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var idxTotal = reader.GetOrdinal("totalEvaluations");
                var idxOverall = reader.GetOrdinal("overallAverage");
                var idxOnTime = reader.GetOrdinal("avgOnTimeDelivery");
                var idxQuality = reader.GetOrdinal("avgQualityScore");
                var idxService = reader.GetOrdinal("avgServiceScore");
                var idxCost = reader.GetOrdinal("avgCostScore");

                return new SupplierStatsResponse
                {
                    TotalEvaluations = reader.IsDBNull(idxTotal) ? 0 : reader.GetInt32(idxTotal),
                    OverallAverage = reader.IsDBNull(idxOverall) ? null : reader.GetDecimal(idxOverall),
                    AvgOnTimeDelivery = reader.IsDBNull(idxOnTime) ? null : reader.GetDecimal(idxOnTime),
                    AvgQualityScore = reader.IsDBNull(idxQuality) ? null : reader.GetDecimal(idxQuality),
                    AvgServiceScore = reader.IsDBNull(idxService) ? null : reader.GetDecimal(idxService),
                    AvgCostScore = reader.IsDBNull(idxCost) ? null : reader.GetDecimal(idxCost)
                };
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        return null;
    }

    #endregion
}
