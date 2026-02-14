using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Application.StateMachines;

public abstract class StateMachine<TEntity, TStatusHistory>
{
    private static readonly IReadOnlyDictionary<string, string> EmptyLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    protected abstract IReadOnlyDictionary<string, IReadOnlyList<string>> Transitions { get; }

    protected virtual IReadOnlyDictionary<string, string> StatusLabels => EmptyLabels;

    protected abstract string? GetStatus(TEntity entity);

    protected abstract void SetStatus(TEntity entity, string status);

    public bool CanTransition(string? fromStatus, string? toStatus)
    {
        if (string.IsNullOrWhiteSpace(fromStatus) || string.IsNullOrWhiteSpace(toStatus))
        {
            return false;
        }

        return Transitions.TryGetValue(fromStatus, out var allowed) &&
               allowed.Any(status => string.Equals(status, toStatus, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> GetAvailableTransitions(string? fromStatus)
    {
        return fromStatus != null && Transitions.TryGetValue(fromStatus, out var allowed)
            ? allowed
            : Array.Empty<string>();
    }

    public void ValidateTransition(string? fromStatus, string toStatus)
    {
        if (!CanTransition(fromStatus, toStatus))
        {
            var fromLabel = ResolveLabel(fromStatus);
            var toLabel = ResolveLabel(toStatus);
            var allowed = GetAvailableTransitions(fromStatus);
            var allowedLabels = allowed.Select(ResolveLabel).Where(label => label.Length > 0).ToList();
            var allowedText = allowedLabels.Count == 0 ? "none" : string.Join(", ", allowedLabels);

            throw new InvalidOperationException(
                $"Invalid state transition: Cannot change from \"{fromLabel}\" to \"{toLabel}\". " +
                $"Allowed transitions: {allowedText}");
        }
    }

    protected virtual void BeforeTransition(
        TEntity entity,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason)
    {
    }

    protected virtual Task BeforeTransitionAsync(
        TEntity entity,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual void AfterTransition(
        TEntity entity,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason)
    {
    }

    protected virtual Task AfterTransitionAsync(
        TEntity entity,
        string? oldStatus,
        string newStatus,
        AuthUser? user,
        string? reason,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public TEntity TransitionSync(
        TEntity entity,
        string newStatus,
        AuthUser? user,
        string? reason = null)
    {
        var oldStatus = GetStatus(entity);
        ValidateTransition(oldStatus, newStatus);

        BeforeTransition(entity, oldStatus, newStatus, user, reason);

        SetStatus(entity, newStatus);

        AfterTransition(entity, oldStatus, newStatus, user, reason);

        return entity;
    }

    public async Task<TEntity> TransitionAsync(
        TEntity entity,
        string newStatus,
        AuthUser? user,
        string? reason = null,
        Func<TEntity, string, CancellationToken, Task<TEntity?>>? updateAsync = null,
        CancellationToken cancellationToken = default)
    {
        var oldStatus = GetStatus(entity);
        ValidateTransition(oldStatus, newStatus);

        BeforeTransition(entity, oldStatus, newStatus, user, reason);
        await BeforeTransitionAsync(entity, oldStatus, newStatus, user, reason, cancellationToken).ConfigureAwait(false);

        var updatedEntity = entity;
        if (updateAsync != null)
        {
            var persisted = await updateAsync(entity, newStatus, cancellationToken).ConfigureAwait(false);
            if (persisted != null)
            {
                updatedEntity = persisted;
            }
        }

        SetStatus(updatedEntity, newStatus);

        AfterTransition(updatedEntity, oldStatus, newStatus, user, reason);
        await AfterTransitionAsync(updatedEntity, oldStatus, newStatus, user, reason, cancellationToken).ConfigureAwait(false);

        return updatedEntity;
    }

    public virtual Task<IReadOnlyList<TStatusHistory>> GetStatusHistoryAsync(
        int entityId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TStatusHistory>>(Array.Empty<TStatusHistory>());
    }

    private string ResolveLabel(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return status ?? string.Empty;
        }

        return StatusLabels.TryGetValue(status, out var label) ? label : status;
    }
}
