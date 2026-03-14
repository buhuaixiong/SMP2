using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Audit;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class RfqComparisonPrintService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly RfqWorkflowStore _rfqWorkflowStore;
    private readonly AuditReadStore _auditReadStore;

    public RfqComparisonPrintService(
        SupplierSystemDbContext dbContext,
        RfqWorkflowStore rfqWorkflowStore,
        AuditReadStore auditReadStore)
    {
        _dbContext = dbContext;
        _rfqWorkflowStore = rfqWorkflowStore;
        _auditReadStore = auditReadStore;
    }

    public async Task<RfqComparisonPrintData?> BuildAsync(
        long rfqId,
        string? printedBy,
        string scope,
        CancellationToken cancellationToken)
    {
        var normalizedScope = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) ? "all" : "latest";

        var rfq = await _rfqWorkflowStore.FindRfqAsync(rfqId, asNoTracking: true, cancellationToken);
        if (rfq == null)
        {
            return null;
        }

        var lineItems = await _rfqWorkflowStore.LoadOrderedRfqLineItemsAsync(rfqId, cancellationToken);
        var lineItemMap = lineItems.ToDictionary(item => item.Id);
        var approvals = await _rfqWorkflowStore.LoadApprovalsForRfqAsync(rfqId, cancellationToken);
        var approvalIds = approvals.Select(item => (long)item.Id).ToList();
        var rounds = await _rfqWorkflowStore.LoadBidRoundsAsync(rfqId, cancellationToken);
        var latestRound = rounds.FirstOrDefault();
        var currentRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(rfqId, asNoTracking: true, cancellationToken);

        var auditRecords = await _dbContext.RfqPriceAuditRecords.AsNoTracking()
            .Where(record => record.RfqId == rfqId)
            .ToListAsync(cancellationToken);

        var quoteRoundMap = auditRecords
            .Where(record => record.QuoteId.HasValue)
            .GroupBy(record => record.QuoteId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.UpdatedAt)
                    .ThenByDescending(item => item.CreatedAt)
                    .Select(item => item.RoundNumber)
                    .FirstOrDefault());

        var quoteIds = quoteRoundMap.Keys.ToList();
        var auditLogs = await _auditReadStore.QueryAuditLogs()
            .Where(log => log.EntityType == "rfq" || log.EntityType == "rfq_approval" || log.EntityType == "quote")
            .OrderBy(log => log.CreatedAt)
            .ToListAsync(cancellationToken);

        var roundGroups = new List<RfqComparisonPrintRoundGroup>();
        var activeRoundRecords = normalizedScope == "all"
            ? auditRecords
            : FilterRecordsForRound(auditRecords, latestRound?.Id, latestRound?.RoundNumber);

        foreach (var round in normalizedScope == "all" ? rounds : rounds.Take(1))
        {
            var invitedSuppliers = await _rfqWorkflowStore.LoadInvitedSupplierRowsAsync(rfqId, round.Id, cancellationToken);
            var roundRecords = FilterRecordsForRound(auditRecords, round.Id, round.RoundNumber);
            roundGroups.Add(new RfqComparisonPrintRoundGroup
            {
                RoundId = round.Id,
                RoundNumber = round.RoundNumber,
                Status = round.Status,
                BidDeadline = round.BidDeadline,
                InvitedSupplierCount = invitedSuppliers.Count,
                SubmittedSupplierCount = invitedSuppliers.Count(item => IsSubmittedStatus(item.QuoteStatus)),
                WithdrawnSupplierCount = invitedSuppliers.Count(item => IsWithdrawnStatus(item.QuoteStatus)),
                SupplierSummary = BuildSupplierSummary(invitedSuppliers),
                QuoteRows = BuildQuoteRows(roundRecords, lineItemMap),
            });
        }

        var activeInvitedSuppliers = normalizedScope == "all"
            ? roundGroups.SelectMany(group => group.SupplierSummary).ToList()
            : roundGroups.FirstOrDefault()?.SupplierSummary ?? [];

        var activeRoundGroup = roundGroups.FirstOrDefault();
        var activeSelectedSuppliers = activeRoundRecords
            .Where(record => record.SelectedQuoteId.HasValue && record.SelectedQuoteId == record.QuoteId)
            .Select(record => record.SelectedSupplierName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RfqComparisonPrintData
        {
            RfqId = rfq.Id,
            Scope = normalizedScope,
            Title = rfq.Title,
            Status = rfq.Status,
            PrintedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            PrintedBy = printedBy,
            ReviewCompletedAt = rfq.ReviewCompletedAt,
            MaterialType = rfq.MaterialType,
            RfqType = rfq.RfqType,
            Currency = rfq.Currency,
            BudgetAmount = rfq.BudgetAmount,
            ValidUntil = (normalizedScope == "latest" ? latestRound?.BidDeadline : currentRound?.BidDeadline) ?? rfq.ValidUntil,
            CreatedBy = rfq.CreatedBy,
            CreatedAt = rfq.CreatedAt,
            RequestingParty = rfq.RequestingParty,
            RequestingDepartment = rfq.RequestingDepartment,
            Description = rfq.Description,
            SelectedSupplierSummary = activeSelectedSuppliers.Count == 0 ? null : string.Join(", ", activeSelectedSuppliers),
            InvitedSupplierCount = activeRoundGroup?.InvitedSupplierCount ?? 0,
            SubmittedSupplierCount = activeRoundGroup?.SubmittedSupplierCount ?? 0,
            WithdrawnSupplierCount = activeRoundGroup?.WithdrawnSupplierCount ?? 0,
            CurrentRound = currentRound == null ? null : BuildRoundSummary(currentRound, roundGroups),
            LatestRound = latestRound == null ? null : BuildRoundSummary(latestRound, roundGroups),
            SupplierSummary = activeInvitedSuppliers,
            QuoteRows = BuildQuoteRows(activeRoundRecords, lineItemMap),
            AuditRows = BuildAuditRows(
                auditLogs,
                rfqId,
                quoteIds,
                approvalIds,
                quoteRoundMap,
                normalizedScope == "latest" ? latestRound?.RoundNumber : null),
            RoundGroups = roundGroups,
        };
    }

    private static RfqComparisonPrintRoundSummary BuildRoundSummary(
        RfqBidRound round,
        IReadOnlyCollection<RfqComparisonPrintRoundGroup> roundGroups)
    {
        var group = roundGroups.FirstOrDefault(item => item.RoundId == round.Id);
        return new RfqComparisonPrintRoundSummary
        {
            Id = round.Id,
            RoundNumber = round.RoundNumber,
            Status = round.Status,
            BidDeadline = round.BidDeadline,
            OpenedAt = round.OpenedAt,
            ClosedAt = round.ClosedAt,
            InvitedSupplierCount = group?.InvitedSupplierCount ?? 0,
            SubmittedSupplierCount = group?.SubmittedSupplierCount ?? 0,
            WithdrawnSupplierCount = group?.WithdrawnSupplierCount ?? 0,
        };
    }

    private static List<RfqComparisonSupplierSummaryRow> BuildSupplierSummary(
        IEnumerable<RfqInvitedSupplierRow> invitedSuppliers)
    {
        return invitedSuppliers
            .OrderBy(item => item.CompanyName)
            .Select(item => new RfqComparisonSupplierSummaryRow
            {
                SupplierName = item.CompanyName,
                SupplierCode = item.SupplierCode,
                VendorCode = item.VendorCode,
                QuoteStatus = NormalizeQuoteStatus(item.QuoteStatus),
                QuoteSubmittedAt = item.QuoteSubmittedAt,
            })
            .ToList();
    }

    private static List<RfqPriceAuditRecord> FilterRecordsForRound(
        IEnumerable<RfqPriceAuditRecord> auditRecords,
        long? bidRoundId,
        int? roundNumber)
    {
        return auditRecords
            .Where(record =>
                (bidRoundId.HasValue && record.BidRoundId == bidRoundId) ||
                (!bidRoundId.HasValue && roundNumber.HasValue && record.RoundNumber == roundNumber) ||
                (!bidRoundId.HasValue && !roundNumber.HasValue))
            .ToList();
    }

    private static List<RfqComparisonQuoteRow> BuildQuoteRows(
        IEnumerable<RfqPriceAuditRecord> auditRecords,
        IReadOnlyDictionary<long, RfqLineItem> lineItemMap)
    {
        return auditRecords
            .GroupBy(record => new { record.RoundNumber, record.RfqLineItemId, record.SupplierId, record.QuoteId })
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAt)
                .ThenByDescending(item => item.CreatedAt)
                .First())
            .OrderBy(record => record.RoundNumber ?? int.MaxValue)
            .ThenBy(record => record.LineNumber ?? int.MaxValue)
            .ThenBy(record => record.SupplierName)
            .ThenBy(record => record.QuoteSubmittedAt)
            .Select(record =>
            {
                lineItemMap.TryGetValue(record.RfqLineItemId ?? 0, out var lineItem);
                return new RfqComparisonQuoteRow
                {
                    RoundNumber = record.RoundNumber,
                    LineNumber = record.LineNumber,
                    MaterialCode = lineItem?.MaterialCategory,
                    ItemName = lineItem?.ItemName,
                    Specifications = lineItem?.Specifications,
                    Unit = lineItem?.Unit,
                    Quantity = record.Quantity ?? lineItem?.Quantity,
                    SupplierName = record.SupplierName,
                    QuoteCurrency = record.QuoteCurrency,
                    QuotedUnitPrice = record.QuotedUnitPrice,
                    QuotedTotalPrice = record.QuotedTotalPrice,
                    QuoteSubmittedAt = record.QuoteSubmittedAt,
                    IsSelected = record.SelectedQuoteId.HasValue && record.SelectedQuoteId == record.QuoteId,
                    SelectedSupplierName = record.SelectedSupplierName,
                    SelectedCurrency = record.SelectedCurrency,
                    SelectedUnitPrice = record.SelectedUnitPrice,
                };
            })
            .ToList();
    }

    private static List<RfqComparisonAuditRow> BuildAuditRows(
        IEnumerable<AuditLog> auditLogs,
        long rfqId,
        IReadOnlyCollection<long> quoteIds,
        IReadOnlyCollection<long> approvalIds,
        IReadOnlyDictionary<long, int?> quoteRoundMap,
        int? filterRoundNumber)
    {
        return auditLogs
            .Where(log =>
                (log.EntityType == "rfq" && log.EntityId == rfqId.ToString(CultureInfo.InvariantCulture)) ||
                (log.EntityType == "rfq_approval" && log.EntityId != null && approvalIds.Contains(ParseLong(log.EntityId))) ||
                (log.EntityType == "quote" && log.EntityId != null && quoteIds.Contains(ParseLong(log.EntityId))))
            .Select(log =>
            {
                var roundNumber = ResolveAuditRoundNumber(log, quoteRoundMap);
                return new RfqComparisonAuditRow
                {
                    RoundNumber = roundNumber,
                    OccurredAt = log.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ActorName = log.ActorName,
                    EntityType = log.EntityType,
                    Action = log.Action,
                    Detail = BuildAuditDetail(log),
                };
            })
            .Where(row => !filterRoundNumber.HasValue || !row.RoundNumber.HasValue || row.RoundNumber == filterRoundNumber)
            .ToList();
    }

    private static int? ResolveAuditRoundNumber(AuditLog log, IReadOnlyDictionary<long, int?> quoteRoundMap)
    {
        if (log.EntityType == "quote" &&
            log.EntityId != null &&
            quoteRoundMap.TryGetValue(ParseLong(log.EntityId), out var quoteRoundNumber))
        {
            return quoteRoundNumber;
        }

        if (string.IsNullOrWhiteSpace(log.Changes))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(log.Changes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("roundNumber", out var roundNumberElement) && roundNumberElement.TryGetInt32(out var roundNumber))
            {
                return roundNumber;
            }

            if (root.TryGetProperty("previousRoundNumber", out var previousRoundNumberElement) && previousRoundNumberElement.TryGetInt32(out var previousRoundNumber))
            {
                return previousRoundNumber;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? BuildAuditDetail(AuditLog log)
    {
        if (!string.IsNullOrWhiteSpace(log.Summary))
        {
            return log.Summary;
        }

        if (string.IsNullOrWhiteSpace(log.Changes))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(log.Changes);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => property.Value.GetRawText()
                    };

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        parts.Add($"{property.Name}: {value}");
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join("; ", parts);
                }
            }
        }
        catch
        {
        }

        return log.Changes;
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static bool IsSubmittedStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized is "submitted" or "under_review" or "selected" or "rejected";
    }

    private static bool IsWithdrawnStatus(string? status)
    {
        return string.Equals(status?.Trim(), "withdrawn", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeQuoteStatus(string? status)
    {
        if (IsSubmittedStatus(status))
        {
            return "submitted";
        }

        if (IsWithdrawnStatus(status))
        {
            return "withdrawn";
        }

        return "not_submitted";
    }
}
