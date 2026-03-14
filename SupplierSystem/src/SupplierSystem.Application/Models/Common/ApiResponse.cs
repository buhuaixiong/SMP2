using System.Text.Json.Serialization;

namespace SupplierSystem.Application.Models.Common;

public class ApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;
    [JsonPropertyName("data")]
    public object? Data { get; set; }
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;
    [JsonPropertyName("data")]
    public T? Data { get; set; }
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
