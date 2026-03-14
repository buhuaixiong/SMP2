using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Rfq;

public static class QuoteVisibility
{
    public const string QuoteVisibilityLockMessage =
        "Quotes are hidden until every invited supplier has submitted a quote or the RFQ deadline has passed.";

    private static readonly string[] ProcurementPermissions =
    {
        Permissions.PurchaserRfqTarget,
        Permissions.ProcurementManagerRfqReview,
        Permissions.ProcurementDirectorRfqApprove,
    };

    public static async Task<QuoteVisibilityResult> GetVisibilityAsync(
        SupplierSystemDbContext dbContext,
        int rfqId,
        AuthUser? user,
        CancellationToken cancellationToken = default)
    {
        var rfq = await dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            return new QuoteVisibilityResult(false, new QuoteVisibilityContext
            {
                RfqExists = false,
                InvitedCount = 0,
                SubmittedCount = 0,
                DeadlinePassed = false,
                AllSubmitted = false,
                Unlocked = false,
                Opened = false,
                Deadline = null,
            });
        }

        var currentRound = await dbContext.RfqBidRounds.AsNoTracking()
            .Where(round => round.RfqId == rfqId)
            .OrderByDescending(round => round.RoundNumber)
            .ThenByDescending(round => round.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var currentRoundId = currentRound?.Id;

        var invitedSupplierIds = await dbContext.SupplierRfqInvitations.AsNoTracking()
            .Where(inv => inv.RfqId == rfqId
                          && (currentRoundId == null ? inv.BidRoundId == null : inv.BidRoundId == currentRoundId)
                          && inv.SupplierId.HasValue
                          && (inv.Status == null ||
                              !new[] { "declined", "revoked", "cancelled", "expired" }
                                  .Contains(inv.Status.ToLower())))
            .Select(inv => inv.SupplierId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var submittedSupplierIds = await dbContext.Quotes.AsNoTracking()
            .Where(q => q.RfqId == rfqId
                        && (currentRoundId == null ? q.BidRoundId == null : q.BidRoundId == currentRoundId)
                        && q.IsLatest
                        && q.Status != null
                        && q.Status != "draft"
                        && q.Status != "withdrawn")
            .Select(q => q.SupplierId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var invitedCount = invitedSupplierIds.Count;
        var submittedCount = submittedSupplierIds.Count;

        var deadlinePassed = false;
        DateTime? deadline = null;
        var deadlineValue = currentRound?.BidDeadline ?? rfq.ValidUntil;
        if (!string.IsNullOrWhiteSpace(deadlineValue) &&
            DateTime.TryParse(deadlineValue, out var parsed))
        {
            deadline = parsed;
            deadlinePassed = DateTime.UtcNow >= parsed;
        }

        var hasInvitations = invitedCount > 0;
        var allSubmitted = hasInvitations ? submittedCount >= invitedCount : submittedCount > 0;
        var roundClosed = string.Equals(currentRound?.Status, "closed", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(currentRound?.Status, "cancelled", StringComparison.OrdinalIgnoreCase);
        var opened = roundClosed ||
                     !string.IsNullOrWhiteSpace(currentRound?.OpenedAt) ||
                     deadlinePassed ||
                     allSubmitted;
        var unlocked = opened;

        var context = new QuoteVisibilityContext
        {
            RfqExists = true,
            BidRoundId = currentRoundId,
            RoundNumber = currentRound?.RoundNumber,
            InvitedCount = invitedCount,
            SubmittedCount = submittedCount,
            DeadlinePassed = deadlinePassed,
            AllSubmitted = allSubmitted,
            Unlocked = unlocked,
            Opened = opened,
            Deadline = deadlineValue,
        };

        if (!IsProcurementUser(user))
        {
            return new QuoteVisibilityResult(false, context);
        }

        return new QuoteVisibilityResult(!unlocked, context);
    }

    public static bool IsProcurementUser(AuthUser? user)
    {
        if (user == null)
        {
            return false;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return ProcurementPermissions.Any(granted.Contains);
    }
}

public sealed class QuoteVisibilityContext
{
    public bool RfqExists { get; set; }
    public long? BidRoundId { get; set; }
    public int? RoundNumber { get; set; }
    public int InvitedCount { get; set; }
    public int SubmittedCount { get; set; }
    public bool DeadlinePassed { get; set; }
    public bool AllSubmitted { get; set; }
    public bool Unlocked { get; set; }
    public bool Opened { get; set; }
    public string? Deadline { get; set; }
}

public sealed record QuoteVisibilityResult(bool Locked, QuoteVisibilityContext Context);
