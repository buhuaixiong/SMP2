namespace SupplierSystem.Application.Exceptions;

public sealed class ForbiddenException : ServiceException
{
    public ForbiddenException(string message = "Forbidden", string? errorCode = "FORBIDDEN")
        : base(403, message, errorCode)
    {
    }
}
