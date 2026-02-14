using Microsoft.AspNetCore.Http;

namespace SupplierSystem.Api.Models.Rfq;

public sealed class ImportExcelRequest
{
    public IFormFile? File { get; init; }
    public string? SheetName { get; init; }
    public int? HeaderRow { get; init; }
}

public sealed record ImportExcelRequirement(
    string ItemName,
    decimal Quantity,
    string Unit,
    string? ItemCode,
    decimal? EstimatedUnitPrice,
    decimal? EstimatedAmount,
    string? Notes);

public sealed record ImportExcelMetadata(
    string ImportedAt,
    int RowCount,
    string SheetName);

public sealed record ImportExcelResult(
    string Title,
    string RequestingDepartment,
    string RequirementDate,
    string Currency,
    decimal BudgetAmount,
    string Description,
    List<ImportExcelRequirement> Requirements,
    List<string> RecommendedSuppliers,
    string PrNumber,
    ImportExcelMetadata Metadata);
