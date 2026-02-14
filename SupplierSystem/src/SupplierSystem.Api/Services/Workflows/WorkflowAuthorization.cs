using System.Linq;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Services.Workflows;

public static class WorkflowAuthorization
{
    public static bool HasStepPermission(AuthUser? user, WorkflowStepDefinition step)
    {
        if (user == null)
        {
            return false;
        }

        var permissions = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (permissions.Contains(step.Permission))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            return false;
        }

        return step.Roles.Any(role => string.Equals(role, user.Role, StringComparison.OrdinalIgnoreCase));
    }
}
