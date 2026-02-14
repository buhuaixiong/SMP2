using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class PermissionsController
{
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string? scope, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var query = _dbContext.Users.AsNoTracking();
        if (!string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
        {
            var supplierRoles = new[] { "temp_supplier", "formal_supplier", "supplier" };
            query = query.Where(u => !supplierRoles.Contains(u.Role));
        }

        var users = await query
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);

        var payload = users.Select(BuildUserResponse).ToList();
        return Ok(new { data = payload });
    }

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUserById(string userId, CancellationToken cancellationToken)
    {
        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!string.Equals(actor.Id, userId, StringComparison.OrdinalIgnoreCase))
        {
            var permissionResult = RequirePermission(actor, Permissions.AdminRoleManage);
            if (permissionResult != null)
            {
                return permissionResult;
            }
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(new { data = BuildUserResponseWithPermissions(user) });
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var id = GetString(body, "id");
        var name = GetString(body, "name");
        var username = GetString(body, "username");
        var password = GetString(body, "password");
        var role = GetString(body, "role");
        var email = GetString(body, "email");
        var supplierId = GetInt(body, "supplierId", "supplier_id");
        var status = GetString(body, "status") ?? "active";

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(role))
        {
            return BadRequest(new { message = "name, username, password, and role are required." });
        }

        var normalizedRole = NormalizeRole(role);
        if (normalizedRole == null)
        {
            return BadRequest(new { message = $"Invalid role: {role}" });
        }

        var userId = string.IsNullOrWhiteSpace(id) ? username!.Trim() : id.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { message = "User ID is required." });
        }

        if (password.Length < 6)
        {
            return BadRequest(new { message = "Password must be at least 6 characters long." });
        }

        if (IsSupplierRole(normalizedRole) && !supplierId.HasValue)
        {
            return BadRequest(new { message = "Supplier roles require supplierId." });
        }

        if (!IsSupplierRole(normalizedRole) && supplierId.HasValue)
        {
            return BadRequest(new { message = "Non-supplier roles should not have supplierId." });
        }

        // 使用数据库事务和锁防止竞态条件
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 锁定潜在冲突的行，防止并发插入
            var existingUser = await _dbContext.Users
                .FromSqlRaw(@"SELECT * FROM users WITH (UPDLOCK, ROWLOCK) WHERE id = {0} OR username = {1}", userId, username.Trim())
                .FirstOrDefaultAsync(cancellationToken);

            if (existingUser != null)
            {
                if (string.Equals(existingUser.Id, userId, StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return BadRequest(new { message = $"User ID {userId} already exists." });
                }
                if (string.Equals(existingUser.Username, username.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return BadRequest(new { message = $"Username {username} already exists." });
                }
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var existingEmail = await _dbContext.Users
                    .FromSqlRaw(@"SELECT * FROM users WITH (UPDLOCK, ROWLOCK) WHERE email = {0}", email.Trim())
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingEmail != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return BadRequest(new { message = $"Email {email} already exists." });
                }
            }

            var hashed = BCrypt.Net.BCrypt.HashPassword(password, 12);
            var now = DateTimeOffset.UtcNow.ToString("o");

            var user = new User
            {
                Id = userId,
                Name = name!.Trim(),
                Username = username!.Trim(),
                Password = hashed,
                Role = normalizedRole,
                SupplierId = supplierId,
                Email = string.IsNullOrWhiteSpace(email) ? "" : email.Trim(),
                Status = status,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync(
                "user",
                user.Id,
                "create",
                new
                {
                    id = user.Id,
                    name = user.Name,
                    username = user.Username,
                    role = user.Role,
                    email = user.Email,
                    supplierId = user.SupplierId,
                    status = user.Status,
                },
                actor);

            return StatusCode(201, new { data = BuildUserResponse(user) });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    [HttpPut("users/{userId}")]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        var name = GetString(body, "name");
        var email = GetString(body, "email");
        var status = GetString(body, "status");

        if (!string.IsNullOrWhiteSpace(email) &&
            !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _dbContext.Users.AnyAsync(u => u.Email == email && u.Id != userId, cancellationToken);
            if (exists)
            {
                return BadRequest(new { message = $"Email {email} already exists." });
            }

            user.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            user.Name = name;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var validStatuses = new[] { "active", "frozen", "deleted" };
            if (!validStatuses.Contains(status))
            {
                return BadRequest(new { message = $"Invalid status: {status}" });
            }

            user.Status = status;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "user",
            user.Id,
            "update",
            new { name = user.Name, email = user.Email, status = user.Status },
            actor);

        return Ok(new { data = BuildUserResponseWithPermissions(user) });
    }

    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(string userId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (string.Equals(actor.Id, userId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Cannot change your own role." });
        }

        var newRole = GetString(body, "role");
        if (string.IsNullOrWhiteSpace(newRole))
        {
            return BadRequest(new { message = "Role is required." });
        }

        var normalizedRole = NormalizeRole(newRole);
        if (normalizedRole == null)
        {
            return BadRequest(new { message = $"Invalid role: {newRole}" });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (IsSupplierRole(user.Role) || IsSupplierRole(normalizedRole))
        {
            return BadRequest(new { message = "Cannot change supplier user roles through this endpoint." });
        }

        user.Role = normalizedRole;
        user.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "user",
            user.Id,
            "update_role",
            new { role = normalizedRole },
            actor);

        return Ok(new { data = BuildUserResponseWithPermissions(user) });
    }

    [HttpPut("users/{userId}/password")]
    public async Task<IActionResult> UpdateUserPassword(string userId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!string.Equals(actor.Id, userId, StringComparison.OrdinalIgnoreCase))
        {
            var permissionResult = RequirePermission(actor, Permissions.AdminRoleManage);
            if (permissionResult != null)
            {
                return permissionResult;
            }
        }

        var password = GetString(body, "password");
        if (string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "Password is required." });
        }

        if (password.Length < 6)
        {
            return BadRequest(new { message = "Password must be at least 6 characters long." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(password, 12);
        user.AuthVersion = user.AuthVersion <= 0 ? 1 : user.AuthVersion + 1;
        user.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "user",
            user.Id,
            "update_password",
            new { passwordChanged = true },
            actor);

        return Ok(new { data = BuildUserResponseWithPermissions(user) });
    }

    [HttpPost("users/{userId}/freeze")]
    public async Task<IActionResult> FreezeUser(string userId, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (string.Equals(actor.Id, userId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Cannot freeze your own account." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Cannot freeze admin users." });
        }

        if (string.Equals(user.Status, "frozen", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "User is already frozen." });
        }

        user.Status = "frozen";
        user.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "user",
            user.Id,
            "freeze",
            new { status = "frozen" },
            actor);

        return Ok(new { data = BuildUserResponse(user) });
    }

    [HttpPost("users/{userId}/unfreeze")]
    public async Task<IActionResult> UnfreezeUser(string userId, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (!string.Equals(user.Status, "frozen", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "User is not frozen." });
        }

        user.Status = "active";
        user.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "user",
            user.Id,
            "unfreeze",
            new { status = "active" },
            actor);

        return Ok(new { data = BuildUserResponse(user) });
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId, [FromQuery] bool hard, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (string.Equals(actor.Id, userId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Cannot delete your own account." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Cannot delete admin users." });
        }

        var isTrackingAccount = string.Equals(user.Role, "tracking", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(user.AccountType, "tracking", StringComparison.OrdinalIgnoreCase);
        var shouldHardDelete = hard || isTrackingAccount;

        if (shouldHardDelete)
        {
            var sessions = await _dbContext.ActiveSessions
                .Where(s => s.UserId == user.Id)
                .ToListAsync(cancellationToken);
            var blacklistEntries = await _dbContext.TokenBlacklist
                .Where(entry => entry.UserId == user.Id)
                .ToListAsync(cancellationToken);

            if (sessions.Count > 0)
            {
                _dbContext.ActiveSessions.RemoveRange(sessions);
            }

            if (blacklistEntries.Count > 0)
            {
                _dbContext.TokenBlacklist.RemoveRange(blacklistEntries);
            }

            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogAuditAsync(
                "user",
                user.Id,
                "hard_delete",
                new
                {
                    deleted = true,
                    trackingAccount = isTrackingAccount,
                    userData = new { user.Name, user.Username, user.Role }
                },
                actor);
        }
        else
        {
            user.Status = "deleted";
            user.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogAuditAsync(
                "user",
                user.Id,
                "soft_delete",
                new { status = "deleted" },
                actor);
        }

        return Ok(new
        {
            success = true,
            userId,
            hard = shouldHardDelete,
            message = shouldHardDelete ? "User permanently deleted" : "User soft deleted"
        });
    }
}
