namespace SupplierSystem.Domain.Entities;

public sealed class FreightRateHistory
{
    public int Id { get; set; }
    public string RouteCode { get; set; } = null!;
    public string? RouteName { get; set; }
    public string? RouteNameZh { get; set; }
    public decimal Rate { get; set; }
    public int Year { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
}
