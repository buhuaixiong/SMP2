using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

public sealed partial class PermissionsController
{
    [HttpGet("roles")]
    public IActionResult GetAllRoles()
    {
        var roles = RolePermissions.Roles.Keys
            .Select(role => new
            {
                role,
                label = BuildRoleLabel(role),
                permissions = RolePermissions.GetPermissionsForRole(role),
            })
            .ToList();

        return Ok(new { data = roles });
    }

    [HttpGet("roles/{roleId}")]
    public IActionResult GetRoleById(string roleId)
    {
        var key = NormalizeRole(roleId);
        if (key == null)
        {
            return NotFound(new { message = "Role not found." });
        }

        return Ok(new
        {
            data = new
            {
                role = key,
                label = BuildRoleLabel(key),
                permissions = RolePermissions.GetPermissionsForRole(key),
            }
        });
    }

    [HttpGet("roles-in-use")]
    public async Task<IActionResult> GetRolesInUse(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var roles = await _dbContext.Users
            .AsNoTracking()
            .Select(u => u.Role)
            .Distinct()
            .OrderBy(role => role)
            .ToListAsync(cancellationToken);

        return Ok(new { data = roles });
    }

    [HttpGet("role-stats")]
    public async Task<IActionResult> GetRoleStats(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var stats = await _dbContext.Users
            .AsNoTracking()
            .GroupBy(u => u.Role)
            .Select(group => new { role = group.Key, count = group.Count() })
            .OrderByDescending(entry => entry.count)
            .ToListAsync(cancellationToken);

        return Ok(new { data = stats });
    }
}
