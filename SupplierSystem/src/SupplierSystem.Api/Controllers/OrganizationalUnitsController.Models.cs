namespace SupplierSystem.Api.Controllers;

public sealed partial class OrganizationalUnitsController
{
    private class OrgUnitResponse
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Type { get; set; }
        public int? ParentId { get; set; }
        public int Level { get; set; }
        public string? Path { get; set; }
        public string? Description { get; set; }
        public List<string> AdminIds { get; set; } = new();
        public string? Function { get; set; }
        public string? Category { get; set; }
        public string? Region { get; set; }
        public int IsActive { get; set; }
        public string? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public string? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public int? MemberCount { get; set; }
        public int? SupplierCount { get; set; }
        public int? ContractCount { get; set; }
        public int? ChildCount { get; set; }
    }

    private sealed class OrgUnitDetailResponse : OrgUnitResponse
    {
        public List<OrgUnitMemberResponse> Members { get; set; } = new();
        public List<OrgUnitSupplierResponse> Suppliers { get; set; } = new();
        public List<OrgUnitContractResponse> Contracts { get; set; } = new();
        public List<OrgUnitResponse> Ancestors { get; set; } = new();
        public int DescendantCount { get; set; }
    }

    private sealed class OrgUnitMemberResponse
    {
        public int Id { get; set; }
        public int UnitId { get; set; }
        public string UserId { get; set; } = null!;
        public string? UserName { get; set; }
        public string? UserRole { get; set; }
        public string? Role { get; set; }
        public string? JoinedAt { get; set; }
        public string? AssignedBy { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class OrgUnitSupplierResponse
    {
        public int Id { get; set; }
        public int UnitId { get; set; }
        public int SupplierId { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyId { get; set; }
        public string? Category { get; set; }
        public string? Region { get; set; }
        public string? Status { get; set; }
        public string? AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
        public int IsPrimary { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class OrgUnitContractResponse
    {
        public int Id { get; set; }
        public int UnitId { get; set; }
        public int ContractId { get; set; }
        public string? Title { get; set; }
        public string? AgreementNumber { get; set; }
        public string? Status { get; set; }
        public string? EffectiveFrom { get; set; }
        public string? EffectiveTo { get; set; }
        public string? AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
        public string? Notes { get; set; }
    }
}
