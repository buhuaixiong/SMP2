namespace SupplierSystem.Domain.Entities;

public sealed class ItemMasterRecord
{
    public long Id { get; set; }
    public string Fac { get; set; } = null!;
    public string ItemNumber { get; set; } = null!;
    public string Vendor { get; set; } = null!;
    public string? SourcingName { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerUsernameSnapshot { get; set; }
    public string? ItemDescription { get; set; }
    public string? Unit { get; set; }
    public double? Moq { get; set; }
    public double? Spq { get; set; }
    public string? Currency { get; set; }
    public double? PriceBreak1 { get; set; }
    public double? ExchangeRate { get; set; }
    public string? VendorName { get; set; }
    public string? Terms { get; set; }
    public string? TermsDesc { get; set; }
    public string? Company { get; set; }
    public string? Class { get; set; }
    public string? RawPayload { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public long? LastImportBatchId { get; set; }
}
