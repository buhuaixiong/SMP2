using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    private static bool TryParseBidRoundDate(string? value, out DateTime parsed)
    {
        if (!string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, out parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }

    private static string FormatBidRoundDate(DateTime value)
    {
        return value.ToUniversalTime().ToString("o");
    }

    private static bool IsClosedBidRound(RfqBidRound? bidRound)
    {
        return string.Equals(bidRound?.Status, "closed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(bidRound?.Status, "cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpenedBidRound(RfqBidRound? bidRound, QuoteVisibilityContext? visibilityContext = null)
    {
        return visibilityContext?.Opened == true ||
               IsClosedBidRound(bidRound) ||
               !string.IsNullOrWhiteSpace(bidRound?.OpenedAt);
    }

    private async Task<Dictionary<string, object?>?> BuildBidRoundSummaryAsync(
        long rfqId,
        RfqBidRound? bidRound,
        QuoteVisibilityContext? visibilityContext,
        bool isLatest,
        CancellationToken cancellationToken)
    {
        if (bidRound == null)
        {
            return null;
        }

        int invitedCount;
        int submittedCount;
        bool deadlinePassed;
        bool allSubmitted;

        if (visibilityContext?.BidRoundId == bidRound.Id)
        {
            invitedCount = visibilityContext.InvitedCount;
            submittedCount = visibilityContext.SubmittedCount;
            deadlinePassed = visibilityContext.DeadlinePassed;
            allSubmitted = visibilityContext.AllSubmitted;
        }
        else
        {
            var invitedSuppliers = await _rfqWorkflowStore.LoadInvitedSupplierRowsAsync(rfqId, bidRound.Id, cancellationToken);
            invitedCount = invitedSuppliers.Count;

            submittedCount = await _rfqWorkflowStore.QueryQuoteListRows(rfqId, bidRound.Id)
                .Where(row => row.Quote.Status != null &&
                              row.Quote.Status != "draft" &&
                              row.Quote.Status != "withdrawn")
                .Select(row => row.Quote.SupplierId)
                .Distinct()
                .CountAsync(cancellationToken);

            deadlinePassed = TryParseBidRoundDate(bidRound.BidDeadline, out var deadline)
                ? DateTime.UtcNow >= deadline
                : false;
            allSubmitted = invitedCount > 0 ? submittedCount >= invitedCount : submittedCount > 0;
        }

        var opened = IsOpenedBidRound(bidRound, visibilityContext);
        var pendingCount = Math.Max(invitedCount - submittedCount, 0);

        var round = NodeCaseMapper.ToCamelCaseDictionary(bidRound);
        round["invitedCount"] = invitedCount;
        round["submittedCount"] = submittedCount;
        round["pendingCount"] = pendingCount;
        round["allSubmitted"] = allSubmitted;
        round["deadlinePassed"] = deadlinePassed;
        round["opened"] = opened;
        round["isLatest"] = isLatest;
        return round;
    }
}
