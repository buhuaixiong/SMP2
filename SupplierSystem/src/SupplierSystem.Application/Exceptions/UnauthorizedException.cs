namespace SupplierSystem.Application.Exceptions;

public sealed class UnauthorizedException : ServiceException
{
    public UnauthorizedException(string message = "Unauthorized", string? errorCode = "UNAUTHORIZED")
        : base(401, message, errorCode)
    {
    }
}
