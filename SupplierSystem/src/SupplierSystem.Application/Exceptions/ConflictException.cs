namespace SupplierSystem.Application.Exceptions;

/// <summary>
/// Conflict exception (409 Conflict).
/// </summary>
public sealed class ConflictException : ServiceException
{
    public ConflictException(string message, object? details = null)
        : base(409, message, "CONFLICT", details)
    {
    }
}
