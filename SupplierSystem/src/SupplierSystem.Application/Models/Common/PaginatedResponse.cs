using System.Text.Json.Serialization;

namespace SupplierSystem.Application.Models.Common;

public class PaginatedResponse<T> : ApiResponse<T>
{
    [JsonPropertyName("page")]
    public int Page { get; set; }
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
    [JsonPropertyName("total")]
    public int Total { get; set; }
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}

public sealed class PaginatedResponse : PaginatedResponse<object?>
{
}
