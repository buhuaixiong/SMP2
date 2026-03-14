namespace SupplierSystem.Application.Exceptions;

public sealed class NotFoundException : ServiceException
{
    public NotFoundException(string message = "Resource not found", string? errorCode = "NOT_FOUND")
        : base(404, message, errorCode)
    {
    }
}
