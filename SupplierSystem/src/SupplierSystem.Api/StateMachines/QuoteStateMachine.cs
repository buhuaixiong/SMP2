using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.StateMachines;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.StateMachines;

public sealed class QuoteStateMachine : StateMachine<Quote, QuoteStatusHistory>
{
    public static class Statuses
    {
        public const string Draft = "draft";
        public const string Submitted = "submitted";
        public const string Selected = "selected";
        public const string Rejected = "rejected";
        public const string Withdrawn = "withdrawn";
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> TransitionMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Statuses.Draft] = new[] { Statuses.Submitted },
            [Statuses.Submitted] = new[] { Statuses.Selected, Statuses.Rejected, Statuses.Withdrawn },
            [Statuses.Selected] = Array.Empty<string>(),
            [Statuses.Rejected] = Array.Empty<string>(),
            [Statuses.Withdrawn] = Array.Empty<string>(),
        };

    private static readonly IReadOnlyDictionary<string, string> LabelMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Statuses.Draft] = "Draft",
            [Statuses.Submitted] = "Submitted",
            [Statuses.Selected] = "Selected",
            [Statuses.Rejected] = "Rejected",
            [Statuses.Withdrawn] = "Withdrawn",
        };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly ILogger<QuoteStateMachine> _logger;

    public QuoteStateMachine(SupplierSystemDbContext dbContext, ILogger<QuoteStateMachine> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    protected override IReadOnlyDictionary<string, IReadOnlyList<string>> Transitions => TransitionMap;

    protected override IReadOnlyDictionary<string, string> StatusLabels => LabelMap;

    protected override string? GetStatus(Quote entity) => entity.Status;

    protected override void SetStatus(Quote entity, string status) => entity.Status = status;

    protected override Task BeforeTransitionAsync(
        Quote quote,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(newStatus, Statuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (!quote.TotalAmount.HasValue)
        {
            throw new InvalidOperationException("Cannot submit quote without total price");
        }

        return Task.CompletedTask;
    }

    protected override async Task AfterTransitionAsync(
        Quote quote,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        await RecordStatusHistoryAsync(quote.Id, oldStatus, newStatus, user, reason, cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(newStatus, Statuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Quote {QuoteId} submitted; should notify buyer.", quote.Id);
        }
        else if (string.Equals(newStatus, Statuses.Selected, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Quote {QuoteId} selected as winner.", quote.Id);
        }
        else if (string.Equals(newStatus, Statuses.Withdrawn, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Quote {QuoteId} withdrawn by supplier.", quote.Id);
        }
    }

    public override async Task<IReadOnlyList<QuoteStatusHistory>> GetStatusHistoryAsync(
        int entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.QuoteStatusHistories.AsNoTracking()
                .Where(history => history.QuoteId == entityId)
                .OrderByDescending(history => history.ChangedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get quote status history for quote {QuoteId}", entityId);
            return Array.Empty<QuoteStatusHistory>();
        }
    }

    private async Task RecordStatusHistoryAsync(
        long quoteId,
        string? fromStatus,
        string toStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.QuoteStatusHistories.Add(new QuoteStatusHistory
            {
                QuoteId = quoteId,
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
            _logger.LogWarning(ex, "Failed to record quote status history for quote {QuoteId}", quoteId);
        }
    }
}
