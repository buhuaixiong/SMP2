namespace SupplierSystem.Application.Exceptions;

/// <summary>
/// Compatibility exception for Node-style service errors.
/// </summary>
public sealed class ServiceErrorException : ServiceException
{
    public ServiceErrorException(int status, string message, string? code = null, object? details = null)
        : base(status, message, code ?? "SERVICE_ERROR", details)
    {
    }
}
