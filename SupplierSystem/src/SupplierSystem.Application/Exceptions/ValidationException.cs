namespace SupplierSystem.Application.Exceptions;

public sealed class ValidationException : ServiceException
{
    public ValidationException(string message, object? details = null)
        : base(400, message, "VALIDATION_ERROR", details)
    {
    }
}
