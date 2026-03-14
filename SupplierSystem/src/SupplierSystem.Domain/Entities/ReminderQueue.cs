namespace SupplierSystem.Domain.Entities;

public sealed class ReminderQueue
{
    public int Id { get; set; }
    public string? Type { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? DueAt { get; set; }
    public string? Status { get; set; }
    public string? Payload { get; set; }
    public string? CreatedAt { get; set; }
    public string? SentAt { get; set; }
}
