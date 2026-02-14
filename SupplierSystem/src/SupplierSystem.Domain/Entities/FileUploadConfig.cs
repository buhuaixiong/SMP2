namespace SupplierSystem.Domain.Entities;

public sealed class FileUploadConfig
{
    public int Id { get; set; }
    public string Scenario { get; set; } = null!;
    public string ScenarioName { get; set; } = null!;
    public string? ScenarioDescription { get; set; }
    public string AllowedFormats { get; set; } = null!;
    public int MaxFileSize { get; set; }
    public int MaxFileCount { get; set; }
    public bool? EnableVirusScan { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
