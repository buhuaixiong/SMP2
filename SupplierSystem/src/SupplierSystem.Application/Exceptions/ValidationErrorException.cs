namespace SupplierSystem.Application.Exceptions;

/// <summary>
/// Compatibility exception for validation errors.
/// </summary>
public sealed class ValidationErrorException : ServiceException
{
    public ValidationErrorException(string message, object? details = null)
        : base(400, message, "VALIDATION_ERROR", details)
    {
    }
}
