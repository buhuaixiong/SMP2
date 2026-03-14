namespace SupplierSystem.Application.Exceptions;

/// <summary>
/// Locked exception (423 Locked).
/// </summary>
public sealed class LockedException : ServiceException
{
    public LockedException(string message, object? details = null)
        : base(423, message, "ACCOUNT_LOCKED", details)
    {
    }
}
