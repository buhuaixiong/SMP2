using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private static readonly string[] SupplierRoles = { "temp_supplier", "formal_supplier", "supplier" };
    private readonly UserDirectoryService _userDirectoryService;

    public UsersController(UserDirectoryService userDirectoryService)
    {
        _userDirectoryService = userDirectoryService;
    }

    [HttpGet]
    public async Task<IActionResult> ListUsers(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var hasAccess =
            string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase) ||
            user.IsOrgUnitAdmin ||
            string.Equals(user.Role, "purchaser", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Role, "procurement_manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Role, "procurement_director", StringComparison.OrdinalIgnoreCase) ||
            granted.Contains(Permissions.AdminRoleManage) ||
            granted.Contains(Permissions.AdminPurchasingGroupsManage);

        if (!hasAccess)
        {
            return StatusCode(403, new { message = "Access denied for current role." });
        }

        var role = Request.Query["role"].ToString();
        var includeSuppliers = string.Equals(Request.Query["includeSuppliers"], "true", StringComparison.OrdinalIgnoreCase);

        var users = await _userDirectoryService.ListUsersAsync(role, includeSuppliers, SupplierRoles, cancellationToken);

        var payload = users
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Role,
                u.SupplierId,
                u.Email
            })
            .ToList();

        return Ok(new { data = payload });
    }
}
