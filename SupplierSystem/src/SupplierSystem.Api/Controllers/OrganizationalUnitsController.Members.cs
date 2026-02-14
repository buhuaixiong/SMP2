using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Controllers;

public sealed partial class OrganizationalUnitsController
{
    [HttpGet("{id:int}/members")]
    public async Task<IActionResult> GetMembers(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var tenantId = HttpContext.GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = "Tenant context is required." });
        }

        var unitExists = await _dbContext.OrganizationalUnits.AsNoTracking()
            .AnyAsync(unit => unit.Id == id && unit.TenantId == tenantId && unit.DeletedAt == null, cancellationToken);
        if (!unitExists)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        var members = await (from member in _dbContext.OrganizationalUnitMembers.AsNoTracking()
                             join userEntry in _dbContext.Users.AsNoTracking()
                                 on member.UserId equals userEntry.Id into users
                             from userEntry in users.DefaultIfEmpty()
                             where member.UnitId == id && member.TenantId == tenantId
                             orderby userEntry.Name ?? member.UserId
                             select new OrgUnitMemberResponse
                             {
                                 Id = member.Id,
                                 UnitId = member.UnitId,
                                 UserId = member.UserId,
                                 UserName = userEntry != null ? userEntry.Name : null,
                                 UserRole = userEntry != null ? userEntry.Role : null,
                                 Role = member.Role,
                                 JoinedAt = member.JoinedAt,
                                 AssignedBy = member.AssignedBy,
                                 Notes = member.Notes
                             })
            .ToListAsync(cancellationToken);

        return Ok(new { data = members });
    }

    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> AddMembers(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var tenantId = HttpContext.GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = "Tenant context is required." });
        }

        if (!TryReadStringList(body, out var userIds, "userIds", "user_ids") || userIds.Count == 0)
        {
            return BadRequest(new { message = "User IDs array is required." });
        }

        var unitExists = await _dbContext.OrganizationalUnits.AsNoTracking()
            .AnyAsync(unit => unit.Id == id && unit.TenantId == tenantId && unit.DeletedAt == null, cancellationToken);
        if (!unitExists)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        var normalizedUserIds = userIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Select(userId => userId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedUserIds.Count == 0)
        {
            return BadRequest(new { message = "User IDs array is required." });
        }

        TryReadString(body, out var rawRole, "role");
        var role = string.IsNullOrWhiteSpace(rawRole) ? "member" : rawRole.Trim();
        TryReadString(body, out var rawNotes, "notes");
        var notes = string.IsNullOrWhiteSpace(rawNotes) ? null : rawNotes.Trim();

        var now = DateTimeOffset.UtcNow.ToString("o");
        var assignedBy = user.Name ?? user.Id;

        var existingMembers = await _dbContext.OrganizationalUnitMembers
            .Where(member => member.UnitId == id && member.TenantId == tenantId && normalizedUserIds.Contains(member.UserId))
            .ToListAsync(cancellationToken);
        var existingMap = existingMembers.ToDictionary(member => member.UserId, StringComparer.OrdinalIgnoreCase);

        foreach (var userId in normalizedUserIds)
        {
            if (existingMap.TryGetValue(userId, out var member))
            {
                member.Role = role;
                member.Notes = notes;
                continue;
            }

            _dbContext.OrganizationalUnitMembers.Add(new SupplierSystem.Domain.Entities.OrganizationalUnitMember
            {
                TenantId = tenantId,
                UnitId = id,
                UserId = userId,
                Role = role,
                JoinedAt = now,
                AssignedBy = assignedBy,
                Notes = notes
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new
            {
                message = "Database constraint violation while adding members.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        return Ok(new
        {
            message = $"Added {normalizedUserIds.Count} member(s) to organizational unit.",
            data = new { addedCount = normalizedUserIds.Count }
        });
    }

    [HttpDelete("{id:int}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int id, string userId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var tenantId = HttpContext.GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = "Tenant context is required." });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { message = "User ID is required." });
        }

        var normalizedUserId = userId.Trim();
        var member = await _dbContext.OrganizationalUnitMembers
            .FirstOrDefaultAsync(entry => entry.UnitId == id && entry.TenantId == tenantId && entry.UserId == normalizedUserId, cancellationToken);
        if (member == null)
        {
            return NoContent();
        }

        _dbContext.OrganizationalUnitMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
