namespace SupplierSystem.Application.Models.Requests;

public sealed class CreatePurchasingGroupRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Region { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class UpdatePurchasingGroupRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Region { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class AddPurchasingGroupMembersRequest
{
    public List<string> BuyerIds { get; set; } = new();
    public string? Role { get; set; }
    public string? Notes { get; set; }
}

public sealed class AddPurchasingGroupSuppliersRequest
{
    public List<int> SupplierIds { get; set; } = new();
    public bool? IsPrimary { get; set; }
    public string? Notes { get; set; }
}

public sealed class AssignSuppliersToBuyerRequest
{
    public string? BuyerId { get; set; }
    public List<int> SupplierIds { get; set; } = new();
}
