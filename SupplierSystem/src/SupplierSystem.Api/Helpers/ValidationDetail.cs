namespace SupplierSystem.Api.Helpers;

public sealed class ValidationDetail
{
    public string Field { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}
