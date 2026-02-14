namespace SupplierSystem.Api.Services.TempSuppliers;

public sealed class UpgradeApplicationRecord
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CurrentStep { get; set; }
    public string? SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public string? DueAt { get; set; }
    public int? WorkflowId { get; set; }
    public string? RejectionReason { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}

public sealed class UpgradeDocumentRecord
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public int FileId { get; set; }
    public string? UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpgradeReviewRecord
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public string? DecidedById { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecidedAt { get; set; }
}

public sealed class UpgradeDecisionResult
{
    public string Status { get; set; } = string.Empty;
    public string? CurrentStep { get; set; }
    public string? NextStep { get; set; }
    public bool IsFinal { get; set; }
}

public sealed class AutoUpgradeResult
{
    public bool Triggered { get; set; }
    public int? ApplicationId { get; set; }
}

public sealed class UpgradeRequirementDefinition
{
    public UpgradeRequirementDefinition(string code, string name, string description, bool required)
    {
        Code = code;
        Name = name;
        Description = description;
        Required = required;
    }

    public string Code { get; }
    public string Name { get; }
    public string Description { get; }
    public bool Required { get; }
}

public sealed class UpgradeRequirementItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public object? Template { get; set; }
    public bool? Fulfilled { get; set; }
    public int? DocumentId { get; set; }
}

public sealed class UpgradeWorkflowStepItem
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public int Order { get; set; }
}

public sealed class UpgradeWorkflowStepView
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Permission { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? DueAt { get; set; }
    public string? CompletedAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class WorkflowInstanceView
{
    public int Id { get; set; }
    public string WorkflowType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? CurrentStep { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public List<UpgradeWorkflowStepView> Steps { get; set; } = new();
}

public sealed class UpgradeDocumentDetail
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public int? FileId { get; set; }
    public string? UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? OriginalName { get; set; }
    public string? StoredName { get; set; }
    public string? FileType { get; set; }
    public string? UploadTime { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
}

public sealed class PendingUpgradeApplicationRecord
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CurrentStep { get; set; }
    public string? SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public string? DueAt { get; set; }
    public int? WorkflowId { get; set; }
    public string? RejectionReason { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? SupplierName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactEmail { get; set; }
}

public sealed class SupplierSummary
{
    public int Id { get; set; }
    public string? CompanyName { get; set; }
    public string? Stage { get; set; }
    public string? Status { get; set; }
    public string? CurrentApprover { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactEmail { get; set; }
}
