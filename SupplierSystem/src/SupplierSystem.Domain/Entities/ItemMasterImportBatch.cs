namespace SupplierSystem.Domain.Entities;

public sealed class ItemMasterImportBatch
{
    public long Id { get; set; }
    public string FileName { get; set; } = null!;
    public string SheetScope { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string StartedAt { get; set; } = null!;
    public string? FinishedAt { get; set; }
    public string ImportedByUserId { get; set; } = null!;
    public string? ImportedByName { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public string? SummaryJson { get; set; }
    public string? WarningsJson { get; set; }
    public string? ErrorsJson { get; set; }
}
