using System.Text.Json.Serialization;

namespace SupplierSystem.Application.Models.Common;

public sealed class ApiErrorResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = false;
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    [JsonPropertyName("details")]
    public object? Details { get; set; }
    [JsonPropertyName("stack")]
    public string? Stack { get; set; }
}
