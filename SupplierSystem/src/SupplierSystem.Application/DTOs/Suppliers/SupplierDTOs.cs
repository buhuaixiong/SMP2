using System.ComponentModel.DataAnnotations;

namespace SupplierSystem.Application.DTOs.Suppliers;

/// <summary>
/// 供应商创�?更新请求
/// </summary>
public class CreateSupplierRequest
{
    public string? CompanyName { get; set; }
    public string? CompanyId { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Category { get; set; }
    public string? Address { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? BankAccount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? CreditRating { get; set; }
    public string? ServiceCategory { get; set; }
    public string? Region { get; set; }
    public string? Importance { get; set; }
    public string? ComplianceStatus { get; set; }
    public string? ComplianceNotes { get; set; }
    public string? ComplianceOwner { get; set; }
    public string? FinancialContact { get; set; }
    public string? PaymentCurrency { get; set; }
    public string? FaxNumber { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    public string? SupplierCode { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// 供应商列表查询参�?
/// </summary>
public class SupplierListQuery
{
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string? Region { get; set; }
    public string? Stage { get; set; }
    public string? Importance { get; set; }
    public string? Query { get; set; }
    public string? Tag { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
    public bool ForRfq { get; set; }
}

/// <summary>
/// 供应商列表响�?
/// </summary>
public class SupplierListResponse
{
    public List<SupplierResponse> Suppliers { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>
/// 供应商详情响�?
/// </summary>
public class SupplierResponse
{
    public int Id { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyId { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Category { get; set; }
    public string? Address { get; set; }
    public string? Status { get; set; }
    public string? CurrentApprover { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? Notes { get; set; }
    public string? BankAccount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? CreditRating { get; set; }
    public string? ServiceCategory { get; set; }
    public string? Region { get; set; }
    public string? Importance { get; set; }
    public string? ComplianceStatus { get; set; }
    public string? ComplianceNotes { get; set; }
    public string? ComplianceOwner { get; set; }
    public string? ComplianceReviewedAt { get; set; }
    public string? FinancialContact { get; set; }
    public string? PaymentCurrency { get; set; }
    public string? FaxNumber { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    public string? Stage { get; set; }
    public decimal? ProfileCompletion { get; set; }
    public decimal? DocumentCompletion { get; set; }
    public decimal? CompletionScore { get; set; }
    public string? CompletionStatus { get; set; }
    public string? SupplierCode { get; set; }
    public List<TagResponse>? Tags { get; set; }
    public List<SupplierDocumentResponse>? Documents { get; set; }
    public SupplierStatsResponse? Stats { get; set; }
    public SupplierStatsResponse? RatingsSummary { get; set; }
    public SupplierRatingResponse? LatestRating { get; set; }
    public List<SupplierContractResponse>? Contracts { get; set; }
    public List<SupplierApprovalHistoryResponse>? ApprovalHistory { get; set; }
    public List<SupplierFileApprovalResponse>? FileApprovals { get; set; }
    public List<SupplierFileResponse>? Files { get; set; }
    public SupplierComplianceSummaryResponse? ComplianceSummary { get; set; }
    public List<SupplierMissingRequirement>? MissingRequirements { get; set; }
}

/// <summary>
/// 供应商统计信�?
/// </summary>
public class SupplierStatsResponse
{
    public int TotalEvaluations { get; set; }
    public decimal? OverallAverage { get; set; }
    public decimal? AvgOnTimeDelivery { get; set; }
    public decimal? AvgQualityScore { get; set; }
    public decimal? AvgServiceScore { get; set; }
    public decimal? AvgCostScore { get; set; }
}

public class SupplierRatingResponse
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? PeriodStart { get; set; }
    public string? PeriodEnd { get; set; }
    public decimal? OnTimeDelivery { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? ServiceScore { get; set; }
    public decimal? CostScore { get; set; }
    public decimal? OverallScore { get; set; }
    public string? Notes { get; set; }
    public string? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class SupplierContractResponse
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? AgreementNumber { get; set; }
    public string? Status { get; set; }
    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? PaymentCycle { get; set; }
    public bool AutoRenew { get; set; }
}

public class SupplierApprovalHistoryResponse
{
    public string? Step { get; set; }
    public string? Approver { get; set; }
    public string? Result { get; set; }
    public string? Date { get; set; }
    public string? Comments { get; set; }
    public string? Source { get; set; }
    public string? DecidedAt { get; set; }
    public string? DecidedByName { get; set; }
}

public class SupplierFileApprovalResponse
{
    public int Id { get; set; }
    public int UploadId { get; set; }
    public string? StepKey { get; set; }
    public string? StepName { get; set; }
    public string? Decision { get; set; }
    public string? Comments { get; set; }
    public string? DecidedById { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecidedAt { get; set; }
    public string? FileName { get; set; }
    public string? FileDescription { get; set; }
    public string? RiskLevel { get; set; }
    public string? Source { get; set; }
}

public class SupplierFileResponse
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

public class SupplierMissingRequirement
{
    public string? Type { get; set; }
    public string? Key { get; set; }
    public string? Label { get; set; }
}

public class SupplierComplianceField
{
    public string? Key { get; set; }
    public string? Label { get; set; }
    public object? Value { get; set; }
    public bool Complete { get; set; }
}

public class SupplierMissingField
{
    public string? Key { get; set; }
    public string? Label { get; set; }
}

public class SupplierDocumentRequirementStatus
{
    public string? Type { get; set; }
    public string? Label { get; set; }
    public bool Uploaded { get; set; }
    public bool Exempted { get; set; }
}

public class SupplierMissingDocumentType
{
    public string? Type { get; set; }
    public string? Label { get; set; }
}

public class SupplierComplianceSummaryResponse
{
    public List<SupplierComplianceField> RequiredProfileFields { get; set; } = new();
    public List<SupplierMissingField> MissingProfileFields { get; set; } = new();
    public List<SupplierDocumentRequirementStatus> RequiredDocumentTypes { get; set; } = new();
    public List<SupplierMissingDocumentType> MissingDocumentTypes { get; set; } = new();
    public List<SupplierMissingDocumentType> ExemptedDocumentTypes { get; set; } = new();
    public bool IsProfileComplete { get; set; }
    public bool IsDocumentComplete { get; set; }
    public bool IsComplete { get; set; }
    public int ProfileScore { get; set; }
    public int DocumentScore { get; set; }
    public int OverallScore { get; set; }
    public string? CompletionCategory { get; set; }
    public List<SupplierMissingRequirement> MissingItems { get; set; } = new();
}

/// <summary>
/// 供应商状态更新请�?
/// </summary>
public class UpdateSupplierStatusRequest
{
    [Required]
    public string? Status { get; set; }
    public string? CurrentApprover { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// 供应商审批请�?
/// </summary>
public class ApproveSupplierRequest
{
    [Required]
    public string? Decision { get; set; } // approved, rejected
    public string? Comments { get; set; }
}

/// <summary>
/// 供应商标签更新请�?
/// </summary>
public class UpdateSupplierTagsRequest
{
    public List<string>? Tags { get; set; }
}

/// <summary>
/// 标签响应
/// </summary>
public class TagResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
}

/// <summary>
/// 创建标签请求
/// </summary>
public class CreateTagRequest
{
    [Required]
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
}

/// <summary>
/// 更新标签请求
/// </summary>
public class UpdateTagRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
}

/// <summary>
/// 批量分配标签请求
/// </summary>
public class BatchAssignTagRequest
{
    public List<int> SupplierIds { get; set; } = new();
}

/// <summary>
/// 供应商文档上传请�?
/// </summary>
public class UploadDocumentRequest
{
    public string? DocType { get; set; }
    public string? Category { get; set; }
    public string? ValidFrom { get; set; }
    public string? ExpiresAt { get; set; }
    public string? Notes { get; set; }
    public bool IsRequired { get; set; }
}

/// <summary>
/// 供应商文档响�?
/// </summary>
public class SupplierDocumentResponse
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? DocType { get; set; }
    public string? StoredName { get; set; }
    public string? OriginalName { get; set; }
    public string? UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
    public string? ValidFrom { get; set; }
    public string? ExpiresAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public long? FileSize { get; set; }
    public string? Category { get; set; }
    public bool IsRequired { get; set; }
}

/// <summary>
/// 供应商草稿请�?
/// </summary>
public class SaveDraftRequest
{
    public object? DraftData { get; set; }
}

/// <summary>
/// 供应商统计概�?
/// </summary>
public class SupplierStatsOverviewResponse
{
    public int TotalSuppliers { get; set; }
    public int PendingApproval { get; set; }
    public int ActiveSuppliers { get; set; }
    public int BlockedSuppliers { get; set; }
    public int NewThisMonth { get; set; }
}

public class UpdateDocumentRequest
{
    public string? DocType { get; set; }
    public string? Category { get; set; }
    public string? ValidFrom { get; set; }
    public string? ExpiresAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? UploadedBy { get; set; }
    public bool? IsRequired { get; set; }
}

public sealed class SupplierCompletenessUpdateResult
{
    public int Total { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public List<SupplierCompletenessUpdateError> Errors { get; set; } = new();
}

public sealed class SupplierCompletenessUpdateError
{
    public int SupplierId { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 供应商导入响应
/// </summary>
public class SupplierImportResponse
{
    public SupplierImportSummary Summary { get; set; } = new();
    public List<SupplierImportResult> Results { get; set; } = new();
}

public class SupplierImportSummary
{
    public string? SheetName { get; set; }
    public int ScannedRows { get; set; }
    public int ImportedRows { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int PasswordResets { get; set; }
    public List<SupplierImportError> Errors { get; set; } = new();
}

public class SupplierImportResult
{
    public int RowNumber { get; set; }
    public string Action { get; set; } = string.Empty; // created, updated
    public int SupplierId { get; set; }
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? DefaultPassword { get; set; }
}

public class SupplierImportError
{
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 导入的供应商数据行
/// </summary>
public class SupplierImportRow
{
    public int RowNumber { get; set; }
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Category { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Region { get; set; }
    public string? PaymentCurrency { get; set; }
    public string? FaxNumber { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
}
