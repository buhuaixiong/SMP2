using Microsoft.AspNetCore.Authorization;

namespace SupplierSystem.Api.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(IReadOnlyCollection<string> permissions, bool allowAdminRole = false)
    {
        Permissions = permissions;
        AllowAdminRole = allowAdminRole;
    }

    public IReadOnlyCollection<string> Permissions { get; }
    public bool AllowAdminRole { get; }
}
