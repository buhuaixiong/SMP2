using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.StateMachines;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.StateMachines;

public sealed class ReconciliationStateMachine : StateMachine<Reconciliation, ReconciliationStatusHistory>
{
    public static class Statuses
    {
        public const string Pending = "pending";
        public const string Matched = "matched";
        public const string Variance = "variance";
        public const string Unmatched = "unmatched";
        public const string Confirmed = "confirmed";
        public const string Disputed = "disputed";
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> TransitionMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Statuses.Pending] = new[] { Statuses.Matched, Statuses.Variance, Statuses.Unmatched },
            [Statuses.Matched] = new[] { Statuses.Confirmed, Statuses.Variance, Statuses.Disputed },
            [Statuses.Variance] = new[] { Statuses.Confirmed, Statuses.Disputed, Statuses.Matched },
            [Statuses.Unmatched] = new[] { Statuses.Matched, Statuses.Variance },
            [Statuses.Confirmed] = Array.Empty<string>(),
            [Statuses.Disputed] = new[] { Statuses.Variance, Statuses.Confirmed },
        };

    private static readonly IReadOnlyDictionary<string, string> LabelMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Statuses.Pending] = "Pending",
            [Statuses.Matched] = "Matched",
            [Statuses.Variance] = "Variance",
            [Statuses.Unmatched] = "Unmatched",
            [Statuses.Confirmed] = "Confirmed",
            [Statuses.Disputed] = "Disputed",
        };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly ILogger<ReconciliationStateMachine> _logger;

    public ReconciliationStateMachine(SupplierSystemDbContext dbContext, ILogger<ReconciliationStateMachine> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    protected override IReadOnlyDictionary<string, IReadOnlyList<string>> Transitions => TransitionMap;

    protected override IReadOnlyDictionary<string, string> StatusLabels => LabelMap;

    protected override string? GetStatus(Reconciliation entity) => entity.Status;

    protected override void SetStatus(Reconciliation entity, string status) => entity.Status = status;

    protected override Task BeforeTransitionAsync(
        Reconciliation reconciliation,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (string.Equals(newStatus, Statuses.Matched, StringComparison.OrdinalIgnoreCase))
        {
            if (!reconciliation.WarehouseReceiptId.HasValue)
            {
                throw new InvalidOperationException("Cannot mark as matched without warehouse receipt");
            }
        }

        if (string.Equals(newStatus, Statuses.Variance, StringComparison.OrdinalIgnoreCase))
        {
            var hasVarianceAmount = reconciliation.VarianceAmount.HasValue;
            if (!hasVarianceAmount && string.IsNullOrWhiteSpace(reconciliation.Notes))
            {
                throw new InvalidOperationException("Cannot mark as variance without variance details");
            }
        }

        if (string.Equals(newStatus, Statuses.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            var validConfirmStates = new[]
            {
                Statuses.Matched,
                Statuses.Variance,
                Statuses.Disputed,
            };

            if (oldStatus == null || !validConfirmStates.Contains(oldStatus, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Can only confirm from matched, variance, or disputed state. Current state: {oldStatus}");
            }
        }

        if (string.Equals(newStatus, Statuses.Disputed, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("Dispute reason is required");
            }

            var validDisputeStates = new[]
            {
                Statuses.Variance,
                Statuses.Matched,
            };

            if (oldStatus == null || !validDisputeStates.Contains(oldStatus, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Can only dispute from variance or matched state. Current state: {oldStatus}");
            }
        }

        return Task.CompletedTask;
    }

    protected override async Task AfterTransitionAsync(
        Reconciliation reconciliation,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        await RecordStatusHistoryAsync(reconciliation.Id, oldStatus, newStatus, user, reason, cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(newStatus, Statuses.Variance, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Variance detected for reconciliation {ReconciliationId}", reconciliation.Id);
        }
        else if (string.Equals(newStatus, Statuses.Disputed, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Dispute raised for reconciliation {ReconciliationId}", reconciliation.Id);
        }
        else if (string.Equals(newStatus, Statuses.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Reconciliation {ReconciliationId} confirmed", reconciliation.Id);
        }
    }

    public override async Task<IReadOnlyList<ReconciliationStatusHistory>> GetStatusHistoryAsync(
        int entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.ReconciliationStatusHistories.AsNoTracking()
                .Where(history => history.ReconciliationId == entityId)
                .OrderByDescending(history => history.ChangedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get reconciliation status history for reconciliation {ReconciliationId}", entityId);
            return Array.Empty<ReconciliationStatusHistory>();
        }
    }

    private async Task RecordStatusHistoryAsync(
        int reconciliationId,
        string? fromStatus,
        string toStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        int? changedBy = null;
        if (user != null && int.TryParse(user.Id, out var parsedId))
        {
            changedBy = parsedId;
        }

        try
        {
            _dbContext.ReconciliationStatusHistories.Add(new ReconciliationStatusHistory
            {
                ReconciliationId = reconciliationId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ChangedBy = changedBy,
                ChangedAt = DateTime.UtcNow.ToString("o"),
                Reason = reason,
            });

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record reconciliation status history for reconciliation {ReconciliationId}", reconciliationId);
        }
    }
}
