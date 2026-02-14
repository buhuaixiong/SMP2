using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.StateMachines;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.StateMachines;

public sealed class RfqStateMachine : StateMachine<Rfq, RfqStatusHistory>
{
    public static class Statuses
    {
        public const string Draft = "draft";
        public const string Published = "published";
        public const string InProgress = "in_progress";
        public const string Closed = "closed";
        public const string Cancelled = "cancelled";
        public const string Confirmed = "confirmed";
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> TransitionMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Statuses.Draft] = new[] { Statuses.Published, Statuses.Cancelled },
            [Statuses.Published] = new[] { Statuses.InProgress, Statuses.Closed, Statuses.Cancelled },
            [Statuses.InProgress] = new[] { Statuses.Confirmed, Statuses.Closed, Statuses.Cancelled },
            [Statuses.Closed] = Array.Empty<string>(),
            [Statuses.Cancelled] = Array.Empty<string>(),
            [Statuses.Confirmed] = new[] { Statuses.Closed },
        };

    private static readonly IReadOnlyDictionary<string, string> LabelMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Statuses.Draft] = "Draft",
            [Statuses.Published] = "Published",
            [Statuses.InProgress] = "In progress",
            [Statuses.Closed] = "Closed",
            [Statuses.Cancelled] = "Cancelled",
            [Statuses.Confirmed] = "Confirmed",
        };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly ILogger<RfqStateMachine> _logger;

    public RfqStateMachine(SupplierSystemDbContext dbContext, ILogger<RfqStateMachine> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    protected override IReadOnlyDictionary<string, IReadOnlyList<string>> Transitions => TransitionMap;

    protected override IReadOnlyDictionary<string, string> StatusLabels => LabelMap;

    protected override string? GetStatus(Rfq entity) => entity.Status;

    protected override void SetStatus(Rfq entity, string status) => entity.Status = status;

    protected override async Task BeforeTransitionAsync(
        Rfq rfq,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(newStatus, Statuses.Published, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(rfq.ValidUntil))
        {
            throw new InvalidOperationException("Cannot publish RFQ without deadline");
        }

        if (DateTime.TryParse(rfq.ValidUntil, out var deadline) && deadline < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Cannot publish RFQ with deadline in the past");
        }

        if (!rfq.IsLineItemMode)
        {
            return;
        }

        var itemCount = await _dbContext.RfqLineItems.AsNoTracking()
            .CountAsync(li => li.RfqId == rfq.Id, cancellationToken)
            .ConfigureAwait(false);

        if (itemCount == 0)
        {
            throw new InvalidOperationException("Cannot publish RFQ without line items");
        }
    }

    protected override async Task AfterTransitionAsync(
        Rfq rfq,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (string.Equals(newStatus, Statuses.Published, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("RFQ {RfqId} published; notifications should be sent.", rfq.Id);
        }

        await RecordStatusHistoryAsync(rfq.Id, oldStatus, newStatus, user, reason, cancellationToken)
            .ConfigureAwait(false);
    }

    public override async Task<IReadOnlyList<RfqStatusHistory>> GetStatusHistoryAsync(
        int entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.RfqStatusHistories.AsNoTracking()
                .Where(history => history.RfqId == entityId)
                .OrderByDescending(history => history.ChangedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get RFQ status history for RFQ {RfqId}", entityId);
            return Array.Empty<RfqStatusHistory>();
        }
    }

    private async Task RecordStatusHistoryAsync(
        long rfqId,
        string? fromStatus,
        string toStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.RfqStatusHistories.Add(new RfqStatusHistory
            {
                RfqId = rfqId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ChangedBy = user?.Id,
                ChangedAt = DateTime.UtcNow.ToString("o"),
                Reason = reason,
            });

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record RFQ status history for RFQ {RfqId}", rfqId);
        }
    }
}
