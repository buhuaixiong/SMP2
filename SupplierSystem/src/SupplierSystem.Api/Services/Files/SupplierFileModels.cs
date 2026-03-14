namespace SupplierSystem.Api.Services.Files;

public sealed class SupplierFileRecord
{
    public int Id { get; set; }
    public string? AgreementNumber { get; set; }
    public string? FileType { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
    public int SupplierId { get; set; }
    public string? Status { get; set; }
    public string? UploadTime { get; set; }
    public string? UploaderName { get; set; }
    public string? OriginalName { get; set; }
    public string? StoredName { get; set; }
}
