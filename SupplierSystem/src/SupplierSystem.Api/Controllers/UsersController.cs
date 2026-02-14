using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private static readonly string[] SupplierRoles = { "temp_supplier", "formal_supplier", "supplier" };
    private readonly SupplierSystemDbContext _dbContext;

    public UsersController(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
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

        var query = _dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (!includeSuppliers)
        {
            query = query.Where(u => !SupplierRoles.Contains(u.Role));
        }

        var users = await query
            .OrderBy(u => u.Name)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Role,
                u.SupplierId,
                u.Email
            })
            .ToListAsync(cancellationToken);

        return Ok(new { data = users });
    }
}
