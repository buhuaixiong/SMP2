namespace SupplierSystem.Application.Models.Auth;

public sealed class AuthUser
{
    public string SchemaVersion { get; set; } = "v1";
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int AuthVersion { get; set; }
    public int? SupplierId { get; set; }
    public string? TenantId { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public int? TempAccountId { get; set; }
    public string? AccountType { get; set; }
    public int? RelatedApplicationId { get; set; }
    public List<string> Permissions { get; set; } = new();
    public List<AuthOrgUnit> OrgUnits { get; set; } = new();
    public List<AuthOrgUnit> AdminUnits { get; set; } = new();
    public List<string> Functions { get; set; } = new();
    public List<AuthPurchasingGroup> PurchasingGroups { get; set; } = new();
    public bool IsOrgUnitAdmin { get; set; }
    public bool IsPurchasingGroupLeader { get; set; }
}

public sealed class AuthOrgUnit
{
    public int UnitId { get; set; }
    public string? UnitName { get; set; }
    public string? UnitCode { get; set; }
    public string? MemberRole { get; set; }
    public string? Function { get; set; }
}

public sealed class AuthPurchasingGroup
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? MemberRole { get; set; }
}
