namespace SupplierSystem.Application.Exceptions;

public sealed class HttpResponseException : Exception
{
    public int Status { get; }
    public object? Value { get; }

    public HttpResponseException(int status, object? value = null)
        : base(value?.ToString())
    {
        Status = status;
        Value = value;
    }
}
