namespace SupplierSystem.Domain.Entities;

public sealed class ContractVersion
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int VersionNumber { get; set; }
    public string? StoredName { get; set; }
    public string? OriginalName { get; set; }
    public string? ChangeLog { get; set; }
    public string? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public long? FileSize { get; set; }
}
