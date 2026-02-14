using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupplierSystem.Domain.Entities;

public sealed class Invoice
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SupplierId { get; set; }

    public int? RfqId { get; set; }

    public int? OrderId { get; set; }

    [Required]
    [MaxLength(100)]
    public string InvoiceNumber { get; set; } = null!;

    [Required]
    public DateTime InvoiceDate { get; set; }

    [Required]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string? Currency { get; set; }

    [MaxLength(50)]
    public string? TaxRate { get; set; }

    [MaxLength(50)]
    public string? InvoiceType { get; set; }

    [MaxLength(50)]
    public string? Type { get; set; } // temporary_supplier | formal_supplier

    [MaxLength(500)]
    public string? PrePaymentProof { get; set; }

    public bool SignatureSeal { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; } // pending | verified | rejected | exception

    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public bool? AssistanceRequested { get; set; }

    [MaxLength(100)]
    public string? AssistanceType { get; set; }

    [MaxLength(1000)]
    public string? VerificationPoints { get; set; }

    public DateTime? AssistanceDeadline { get; set; }

    [MaxLength(100)]
    public string? AssistanceStatus { get; set; }

    public bool? DirectorApproved { get; set; }

    public int? DirectorApproverId { get; set; }

    public DateTime? DirectorApprovedAt { get; set; }

    [MaxLength(1000)]
    public string? DirectorApprovalNotes { get; set; }

    [MaxLength(1024)]
    public string? CreditReportUrl { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(255)]
    public string? StoredFileName { get; set; }

    [MaxLength(500)]
    public string? FilePath { get; set; }

    public long? FileSize { get; set; }

    [MaxLength(100)]
    public string? FileType { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int CreatedBy { get; set; }

    // Navigation properties
    [ForeignKey(nameof(SupplierId))]
    public Supplier? Supplier { get; set; }
}
