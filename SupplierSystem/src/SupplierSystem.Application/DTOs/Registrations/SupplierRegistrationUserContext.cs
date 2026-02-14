namespace SupplierSystem.Application.DTOs.Registrations;

public sealed class SupplierRegistrationUserContext
{
    public string? Role { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public sealed class RegistrationWorkflowStep
{
    public string Status { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public sealed class SupplierRegistrationStatusResponse
{
    public int ApplicationId { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SupplierStatus { get; set; }
    public string? SupplierStage { get; set; }
    public string? CurrentApprover { get; set; }
    public string? SubmittedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public SupplierRegistrationTempAccountInfo? TempAccount { get; set; }
    public List<SupplierRegistrationStatusHistoryEntry> History { get; set; } = new();
}

public sealed class SupplierRegistrationApprovalListItem
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? SupplierCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? ContactEmail { get; set; }
    public string? ProcurementEmail { get; set; }
}

public sealed class SupplierRegistrationStatusHistoryEntry
{
    public string Type { get; set; } = string.Empty;
    public string? Step { get; set; }
    public string? Status { get; set; }
    public string? Result { get; set; }
    public string? Approver { get; set; }
    public string? OccurredAt { get; set; }
    public string? Comments { get; set; }
}

public sealed class SupplierRegistrationTempAccountInfo
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? ExpiresAt { get; set; }
    public string? LastLoginAt { get; set; }
    public string? CreatedAt { get; set; }
}

public static class RegistrationConstants
{
    public const string RegistrationStatusPendingPurchaser = "pending_purchaser";
    public const string RegistrationStatusPendingQualityManager = "pending_quality_manager";
    public const string RegistrationStatusPendingProcurementManager = "pending_procurement_manager";
    public const string RegistrationStatusPendingProcurementDirector = "pending_procurement_director";
    public const string RegistrationStatusPendingFinanceDirector = "pending_finance_director";
    public const string RegistrationStatusPendingAccountant = "pending_accountant";
    public const string RegistrationStatusPendingCashier = "pending_cashier";
    public const string RegistrationStatusPendingCodeBinding = "pending_code_binding";
    public const string RegistrationStatusActivated = "activated";
    public const string RegistrationStatusRejected = "rejected";
}
