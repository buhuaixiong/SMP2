using System;

namespace SupplierSystem.Application.Exceptions;

/// <summary>
/// Base service exception.
/// </summary>
public class ServiceException : ApplicationException
{
    public int StatusCode { get; }
    public int Status => StatusCode;
    public string? ErrorCode { get; }
    public string? Code => ErrorCode;
    public object? Details { get; }

    public ServiceException(int statusCode, string message, string? errorCode = null, object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
    }
}
