using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class ReconciliationController
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: false, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var reconciliations = await QueryReconciliationsAsync(
            supplierId: null,
            status,
            startDate,
            endDate,
            cancellationToken);

        var recent = reconciliations
            .OrderByDescending(r => ResolveSortDate(r.UpdatedAt) ?? ResolveSortDate(r.CreatedAt) ?? DateTime.MinValue)
            .Take(20)
            .ToList();

        var recentDtos = await BuildReconciliationDtosAsync(recent, cancellationToken);
        var stats = BuildStats(reconciliations);
        var supplierStats = await BuildSupplierStatsAsync(reconciliations, cancellationToken);
        var trends = BuildVarianceTrends(reconciliations);

        return Success(new
        {
            stats,
            recentReconciliations = recentDtos,
            supplierStats,
            varianceTrends = trends
        });
    }

    [HttpGet("supplier")]
    public async Task<IActionResult> GetSupplierReconciliations(
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: true, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var supplierId = user?.SupplierId;
        if (!supplierId.HasValue)
        {
            return BadRequest("Supplier ID is required.");
        }

        var reconciliations = await QueryReconciliationsAsync(
            supplierId,
            status,
            startDate,
            endDate,
            cancellationToken);

        var dtos = await BuildReconciliationDtosAsync(reconciliations, cancellationToken);
        var stats = BuildStats(reconciliations);

        var pendingConfirmations = reconciliations.Count(r => !IsConfirmed(r));
        var pendingVariances = reconciliations.Count(r => string.Equals(r.Status, ReconciliationStateMachine.Statuses.Variance, StringComparison.OrdinalIgnoreCase));

        return Success(new
        {
            stats,
            reconciliations = dtos,
            pendingActions = new
            {
                pending_confirmations = pendingConfirmations,
                pending_variances = pendingVariances
            }
        });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStatsOverview(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: true, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        int? supplierId = null;
        if (user?.SupplierId.HasValue == true && !HasAnyPermission(user, StaffPermissions))
        {
            supplierId = user.SupplierId;
        }

        var reconciliations = await QueryReconciliationsAsync(
            supplierId,
            status: null,
            startDate: null,
            endDate: null,
            cancellationToken);

        return Success(BuildStats(reconciliations));
    }

    [HttpGet("warehouse-receipts")]
    public async Task<IActionResult> GetWarehouseReceipts(
        [FromQuery] int? reconciliationId,
        [FromQuery] int? supplierId,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: true, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (user?.SupplierId.HasValue == true && !HasAnyPermission(user, StaffPermissions))
        {
            supplierId = user.SupplierId;
        }

        if (reconciliationId.HasValue)
        {
            var reconciliation = await _dbContext.Reconciliations.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == reconciliationId.Value, cancellationToken);
            if (reconciliation == null || !reconciliation.WarehouseReceiptId.HasValue)
            {
                return Success(new WarehouseReceiptSummaryResponse());
            }

            supplierId = reconciliation.SupplierId;
        }

        var receiptsQuery = _dbContext.WarehouseReceipts.AsNoTracking();
        if (supplierId.HasValue)
        {
            receiptsQuery = receiptsQuery.Where(r => r.SupplierId == supplierId.Value);
        }

        var receipts = await receiptsQuery.ToListAsync(cancellationToken);
        if (reconciliationId.HasValue)
        {
            receipts = receipts.Where(r => r.Id == _dbContext.Reconciliations
                .Where(rec => rec.Id == reconciliationId.Value)
                .Select(rec => rec.WarehouseReceiptId)
                .FirstOrDefault()).ToList();
        }

        if (TryParseDate(startDate, out var start) || TryParseDate(endDate, out var end))
        {
            receipts = receipts.Where(receipt =>
            {
                if (!TryParseDate(receipt.ReceiptDate, out var date))
                {
                    return false;
                }

                if (TryParseDate(startDate, out var s) && date < s)
                {
                    return false;
                }

                if (TryParseDate(endDate, out var e) && date > e)
                {
                    return false;
                }

                return true;
            }).ToList();
        }

        var receiptIds = receipts.Select(r => r.Id).ToList();
        var receiptDetails = await _dbContext.WarehouseReceiptDetails.AsNoTracking()
            .Where(d => receiptIds.Contains(d.WarehouseReceiptId))
            .GroupBy(d => d.WarehouseReceiptId)
            .Select(g => new { WarehouseReceiptId = g.Key, Quantity = g.Sum(x => x.Quantity), LineCount = g.Count() })
            .ToListAsync(cancellationToken);
        var detailMap = receiptDetails.ToDictionary(d => d.WarehouseReceiptId);

        var reconciliationMap = await _dbContext.Reconciliations.AsNoTracking()
            .Where(r => r.WarehouseReceiptId.HasValue && receiptIds.Contains(r.WarehouseReceiptId.Value))
            .ToListAsync(cancellationToken);

        var reconByReceipt = reconciliationMap.ToDictionary(r => r.WarehouseReceiptId!.Value);

        var items = receipts.Select(receipt =>
        {
            detailMap.TryGetValue(receipt.Id, out var details);
            reconByReceipt.TryGetValue(receipt.Id, out var reconciliation);
            return new WarehouseReceiptSummaryItem
            {
                Id = receipt.Id,
                ReconciliationId = reconciliation?.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                ReceiptDate = receipt.ReceiptDate,
                WarehouseLocation = receipt.WarehouseLocation,
                ReceivedBy = receipt.ReceiverName,
                ItemDetails = null,
                TotalItems = details?.LineCount ?? 0
            };
        }).ToList();

        var summary = new WarehouseReceiptSummaryTotals
        {
            TotalReceipts = receipts.Count,
            TotalQuantity = detailMap.Values.Sum(d => d.Quantity),
            MatchedReceipts = reconciliationMap.Count(r => string.Equals(r.Status, ReconciliationStateMachine.Statuses.Matched, StringComparison.OrdinalIgnoreCase) ||
                                                         string.Equals(r.Status, ReconciliationStateMachine.Statuses.Confirmed, StringComparison.OrdinalIgnoreCase)),
            VarianceReceipts = reconciliationMap.Count(r => string.Equals(r.Status, ReconciliationStateMachine.Statuses.Variance, StringComparison.OrdinalIgnoreCase))
        };

        return Success(new WarehouseReceiptSummaryResponse
        {
            WarehouseReceipts = items,
            Summary = summary
        });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports(
        [FromQuery] string reportType,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int? supplierId,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: false, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (string.IsNullOrWhiteSpace(reportType))
        {
            return BadRequest("reportType is required.");
        }

        var reconciliations = await QueryReconciliationsAsync(
            supplierId,
            status: null,
            startDate,
            endDate,
            cancellationToken);

        var stats = BuildStats(reconciliations);

        var response = new Dictionary<string, object?>
        {
            ["reportType"] = reportType,
            ["summary"] = stats
        };

        return Success(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReconciliationById(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: true, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var reconciliation = await _dbContext.Reconciliations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (reconciliation == null)
        {
            return NotFound("Reconciliation not found.");
        }

        if (user?.SupplierId.HasValue == true && !HasAnyPermission(user, StaffPermissions) &&
            reconciliation.SupplierId != user.SupplierId.Value)
        {
            return Forbidden();
        }

        var list = await BuildReconciliationDtosAsync(new[] { reconciliation }, cancellationToken);
        var dto = list.First();

        var variance = await _dbContext.ReconciliationVarianceAnalyses.AsNoTracking()
            .Where(v => v.ReconciliationId == id)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (variance != null)
        {
            dto.VarianceAnalysis = new VarianceAnalysisDto
            {
                VarianceAmount = variance.VarianceAmount,
                VariancePercentage = variance.VariancePercentage,
                VarianceReason = variance.RootCause ?? variance.Notes
            };
        }

        var statusHistory = await _dbContext.ReconciliationStatusHistories.AsNoTracking()
            .Where(h => h.ReconciliationId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
        dto.StatusHistory = statusHistory.Select(history => new StatusHistoryDto
        {
            Status = history.ToStatus,
            ChangedBy = history.ChangedBy,
            ChangeReason = history.Reason ?? history.Notes,
            ChangedAt = history.ChangedAt
        }).ToList();

        var matches = await _dbContext.InvoiceReconciliationMatches.AsNoTracking()
            .Where(m => m.ReconciliationId == id)
            .ToListAsync(cancellationToken);
        dto.InvoiceMatches = matches.Select(match => new InvoiceMatchDto
        {
            Id = match.Id,
            InvoiceId = match.InvoiceId,
            ReconciliationId = match.ReconciliationId,
            MatchedAmount = match.InvoiceAmount,
            VarianceAmount = match.VarianceAmount,
            VariancePercentage = match.VariancePercentage,
            MatchingStatus = match.MatchType
        }).ToList();

        return Success(dto);
    }

    private async Task<List<Reconciliation>> QueryReconciliationsAsync(
        int? supplierId,
        string? status,
        string? startDate,
        string? endDate,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Reconciliations.AsNoTracking();

        if (supplierId.HasValue)
        {
            query = query.Where(r => r.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var list = await query.ToListAsync(cancellationToken);

        if (!TryParseDate(startDate, out var start) && !TryParseDate(endDate, out var end))
        {
            return list;
        }

        return list.Where(r =>
        {
            var value = r.PeriodStart ?? r.CreatedAt ?? r.UpdatedAt;
            if (!TryParseDate(value, out var parsed))
            {
                return false;
            }

            if (TryParseDate(startDate, out var startValue) && parsed < startValue)
            {
                return false;
            }

            if (TryParseDate(endDate, out var endValue) && parsed > endValue)
            {
                return false;
            }

            return true;
        }).ToList();
    }

    private static DateTime? ResolveSortDate(string? value)
    {
        return TryParseDate(value, out var parsed) ? parsed : null;
    }

    private static ReconciliationStatsDto BuildStats(IEnumerable<Reconciliation> reconciliations)
    {
        var list = reconciliations.ToList();
        var matched = list.Count(r => string.Equals(r.Status, ReconciliationStateMachine.Statuses.Matched, StringComparison.OrdinalIgnoreCase));
        var variance = list.Count(r => string.Equals(r.Status, ReconciliationStateMachine.Statuses.Variance, StringComparison.OrdinalIgnoreCase));
        var unmatched = list.Count(r => string.Equals(r.Status, ReconciliationStateMachine.Statuses.Unmatched, StringComparison.OrdinalIgnoreCase));
        var confirmed = list.Count(IsConfirmed);
        var totalVariance = list.Sum(r => r.VarianceAmount ?? 0);
        var avgVariance = list.Count > 0 ? totalVariance / list.Count : 0;

        return new ReconciliationStatsDto
        {
            TotalReconciliations = list.Count,
            MatchedCount = matched,
            VarianceCount = variance,
            UnmatchedCount = unmatched,
            ConfirmedCount = confirmed,
            TotalVarianceAmount = totalVariance,
            AvgVarianceAmount = avgVariance
        };
    }

    private async Task<List<SupplierStatsDto>> BuildSupplierStatsAsync(
        IEnumerable<Reconciliation> reconciliations,
        CancellationToken cancellationToken)
    {
        var list = reconciliations.ToList();
        if (list.Count == 0)
        {
            return new List<SupplierStatsDto>();
        }

        var supplierIds = list.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await _dbContext.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.CompanyName, cancellationToken);

        return list.GroupBy(r => r.SupplierId).Select(group =>
        {
            suppliers.TryGetValue(group.Key, out var name);
            return new SupplierStatsDto
            {
                Id = group.Key,
                CompanyName = name ?? string.Empty,
                ReconciliationCount = group.Count(),
                MatchedCount = group.Count(r => string.Equals(r.Status, ReconciliationStateMachine.Statuses.Matched, StringComparison.OrdinalIgnoreCase)),
                VarianceCount = group.Count(r => string.Equals(r.Status, ReconciliationStateMachine.Statuses.Variance, StringComparison.OrdinalIgnoreCase)),
                TotalReconciliationAmount = group.Sum(r => r.TotalReceiptAmount ?? 0),
                AvgVariance = group.Count() > 0 ? group.Sum(r => r.VarianceAmount ?? 0) / group.Count() : 0
            };
        }).ToList();
    }

    private static List<VarianceTrendDto> BuildVarianceTrends(IEnumerable<Reconciliation> reconciliations)
    {
        return reconciliations
            .Select(r =>
            {
                var value = r.CreatedAt ?? r.UpdatedAt;
                return TryParseDate(value, out var parsed) ? new { parsed.Date, Variance = r.VarianceAmount ?? 0 } : null;
            })
            .Where(entry => entry != null)
            .GroupBy(entry => entry!.Date)
            .OrderByDescending(group => group.Key)
            .Take(30)
            .Select(group =>
            {
                var entries = group.ToList();
                return new VarianceTrendDto
                {
                    Date = group.Key.ToString("yyyy-MM-dd"),
                    Count = entries.Count,
                    AvgVariance = entries.Count > 0 ? entries.Sum(e => e!.Variance) / entries.Count : 0,
                    PositiveVarianceCount = entries.Count(e => e!.Variance > 0),
                    NegativeVarianceCount = entries.Count(e => e!.Variance < 0)
                };
            }).ToList();
    }

    private static bool IsConfirmed(Reconciliation reconciliation)
    {
        return string.Equals(reconciliation.Status, ReconciliationStateMachine.Statuses.Confirmed, StringComparison.OrdinalIgnoreCase)
               || !string.IsNullOrWhiteSpace(reconciliation.ConfirmedAt);
    }
}
