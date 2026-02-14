using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class OrganizationalUnitsController
{
    [HttpPost]
    public async Task<IActionResult> CreateUnit([FromBody] JsonElement body, CancellationToken cancellationToken)
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

        if (!TryReadString(body, out var rawCode, "code") || string.IsNullOrWhiteSpace(rawCode))
        {
            return BadRequest(new { message = "Code is required." });
        }

        if (!TryReadString(body, out var rawName, "name") || string.IsNullOrWhiteSpace(rawName))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var code = NormalizeRequiredString(rawCode, MaxCodeLength);
        if (code == null)
        {
            return BadRequest(new { message = $"Code must be {MaxCodeLength} characters or less." });
        }

        var name = NormalizeRequiredString(rawName, MaxNameLength);
        if (name == null)
        {
            return BadRequest(new { message = $"Name must be {MaxNameLength} characters or less." });
        }

        var description = NormalizeOptionalString(
            TryReadString(body, out var rawDescription, "description") ? rawDescription : null,
            MaxDescriptionLength);
        if (description == string.Empty)
        {
            return BadRequest(new { message = $"Description must be {MaxDescriptionLength} characters or less." });
        }

        var category = NormalizeOptionalString(
            TryReadString(body, out var rawCategory, "category") ? rawCategory : null,
            MaxCategoryLength);
        if (category == string.Empty)
        {
            return BadRequest(new { message = $"Category must be {MaxCategoryLength} characters or less." });
        }

        var region = NormalizeOptionalString(
            TryReadString(body, out var rawRegion, "region") ? rawRegion : null,
            MaxRegionLength);
        if (region == string.Empty)
        {
            return BadRequest(new { message = $"Region must be {MaxRegionLength} characters or less." });
        }

        TryReadString(body, out var rawType, "type");
        var type = string.IsNullOrWhiteSpace(rawType) ? null : rawType.Trim();
        if (!string.IsNullOrWhiteSpace(type) && !AllowedTypes.Contains(type))
        {
            return BadRequest(new { message = "Invalid organization unit type." });
        }

        TryReadInt(body, out var parentId, "parentId", "parent_id");

        var adminIds = ResolveAdminIds(body, out _, out _);

        var codeExists = await _dbContext.OrganizationalUnits.AsNoTracking()
            .AnyAsync(unit => unit.TenantId == tenantId && unit.Code == code && unit.DeletedAt == null, cancellationToken);
        if (codeExists)
        {
            return Conflict(new { message = "Organizational unit code already exists." });
        }

        OrganizationalUnit? parent = null;
        var parentLevel = 0;
        string? parentPath = null;
        if (parentId.HasValue)
        {
            parent = await _dbContext.OrganizationalUnits.AsNoTracking()
                .FirstOrDefaultAsync(unit => unit.Id == parentId.Value && unit.TenantId == tenantId && unit.DeletedAt == null, cancellationToken);
            if (parent == null)
            {
                return BadRequest(new { message = "Parent organizational unit not found." });
            }

            parentLevel = parent.Level;
            parentPath = BuildPath(parent, null);
        }

        if (parentLevel + 1 > MaxLevel)
        {
            return BadRequest(new { message = $"Organizational unit depth cannot exceed {MaxLevel} levels." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var actor = user.Name ?? user.Id;

        var unit = new OrganizationalUnit
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            Type = type,
            ParentId = parentId,
            Level = parentLevel + 1,
            Description = description,
            Category = category,
            Region = region,
            AdminIds = NormalizeAdminIds(adminIds),
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor
        };

        _dbContext.OrganizationalUnits.Add(unit);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new
            {
                message = "Database constraint violation while creating organizational unit.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        var resolvedParentPath = parentPath ?? "/";
        unit.Path = $"{resolvedParentPath}{unit.Id}/";

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new
            {
                message = "Failed to finalize organizational unit path.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        var response = ToResponse(unit, 0, 0);
        return StatusCode(201, new { data = response });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUnit(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
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

        var unit = await _dbContext.OrganizationalUnits
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && item.DeletedAt == null, cancellationToken);
        if (unit == null)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        if (TryReadString(body, out var rawName, "name"))
        {
            var name = NormalizeRequiredString(rawName, MaxNameLength);
            if (name == null)
            {
                return BadRequest(new { message = $"Name must be {MaxNameLength} characters or less." });
            }

            unit.Name = name;
        }

        if (TryReadString(body, out var rawType, "type"))
        {
            var type = string.IsNullOrWhiteSpace(rawType) ? null : rawType.Trim();
            if (!string.IsNullOrWhiteSpace(type) && !AllowedTypes.Contains(type))
            {
                return BadRequest(new { message = "Invalid organization unit type." });
            }

            unit.Type = type;
        }

        if (TryReadString(body, out var rawDescription, "description"))
        {
            var description = NormalizeOptionalString(rawDescription, MaxDescriptionLength);
            if (description == string.Empty)
            {
                return BadRequest(new { message = $"Description must be {MaxDescriptionLength} characters or less." });
            }

            unit.Description = description;
        }

        if (TryReadString(body, out var rawCategory, "category"))
        {
            var category = NormalizeOptionalString(rawCategory, MaxCategoryLength);
            if (category == string.Empty)
            {
                return BadRequest(new { message = $"Category must be {MaxCategoryLength} characters or less." });
            }

            unit.Category = category;
        }

        if (TryReadString(body, out var rawRegion, "region"))
        {
            var region = NormalizeOptionalString(rawRegion, MaxRegionLength);
            if (region == string.Empty)
            {
                return BadRequest(new { message = $"Region must be {MaxRegionLength} characters or less." });
            }

            unit.Region = region;
        }

        if (TryReadBool(body, out var isActive, "isActive", "is_active"))
        {
            unit.IsActive = isActive ?? unit.IsActive;
        }

        var adminIds = ResolveAdminIds(body, out var adminIdsSpecified, out var adminIdSpecified);
        if (adminIdsSpecified || adminIdSpecified)
        {
            unit.AdminIds = NormalizeAdminIds(adminIds);
        }

        unit.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        unit.UpdatedBy = user.Name ?? user.Id;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new
            {
                message = "Database constraint violation while updating organizational unit.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        var memberCount = await _dbContext.OrganizationalUnitMembers.AsNoTracking()
            .CountAsync(member => member.UnitId == id && member.TenantId == tenantId, cancellationToken);
        var childCount = await _dbContext.OrganizationalUnits.AsNoTracking()
            .CountAsync(item => item.ParentId == id && item.TenantId == tenantId && item.DeletedAt == null, cancellationToken);

        var response = ToResponse(unit, memberCount, childCount);
        return Ok(new { data = response });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUnit(int id, CancellationToken cancellationToken)
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

        var unit = await _dbContext.OrganizationalUnits
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && item.DeletedAt == null, cancellationToken);
        if (unit == null)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        var childCount = await _dbContext.OrganizationalUnits.AsNoTracking()
            .CountAsync(item => item.ParentId == id && item.TenantId == tenantId && item.DeletedAt == null, cancellationToken);
        if (childCount > 0)
        {
            return Conflict(new { message = "Organizational unit has child units and cannot be deleted." });
        }

        var memberCount = await _dbContext.OrganizationalUnitMembers.AsNoTracking()
            .CountAsync(member => member.UnitId == id && member.TenantId == tenantId, cancellationToken);
        if (memberCount > 0)
        {
            return Conflict(new { message = "Organizational unit has members and cannot be deleted." });
        }

        unit.DeletedAt = DateTimeOffset.UtcNow.ToString("o");
        unit.DeletedBy = user.Name ?? user.Id;
        unit.IsActive = false;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { data = new { deletedIds = new[] { id } } });
    }

    [HttpGet("{id:int}/cascade-check")]
    public async Task<IActionResult> CheckDeletion(int id, CancellationToken cancellationToken)
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

        var unit = await _dbContext.OrganizationalUnits.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && item.DeletedAt == null, cancellationToken);
        if (unit == null)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        var memberCount = await _dbContext.OrganizationalUnitMembers.AsNoTracking()
            .CountAsync(member => member.UnitId == id && member.TenantId == tenantId, cancellationToken);

        var childUnits = await _dbContext.OrganizationalUnits.AsNoTracking()
            .Where(item => item.ParentId == id && item.TenantId == tenantId && item.DeletedAt == null)
            .Select(item => new { id = item.Id, name = item.Name, level = item.Level })
            .ToListAsync(cancellationToken);

        var canDelete = memberCount == 0 && childUnits.Count == 0;
        var message = canDelete
            ? "Organizational unit can be deleted."
            : "Organizational unit has dependent members or child units.";

        return Ok(new
        {
            data = new
            {
                canDelete,
                message,
                blockers = new
                {
                    members = memberCount,
                    suppliers = 0,
                    contracts = 0,
                    childUnits = childUnits.Count,
                    affectedUnits = childUnits
                }
            }
        });
    }

    [HttpPost("{id:int}/move")]
    public async Task<IActionResult> MoveUnit(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
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

        if (!TryReadInt(body, out var newParentId, "newParentId", "new_parent_id"))
        {
            return BadRequest(new { message = "New parent ID is required." });
        }

        if (newParentId.HasValue && newParentId.Value == id)
        {
            return BadRequest(new { message = "Cannot move a unit under itself." });
        }

        var allUnits = await _dbContext.OrganizationalUnits
            .Where(item => item.TenantId == tenantId && item.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var unitMap = allUnits.ToDictionary(item => item.Id);
        if (!unitMap.TryGetValue(id, out var targetUnit))
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        if (newParentId.HasValue && !unitMap.ContainsKey(newParentId.Value))
        {
            return BadRequest(new { message = "New parent organizational unit not found." });
        }

        var childrenMap = BuildChildrenMap(allUnits);
        var subtreeIds = new HashSet<int>();
        CollectSubtreeIds(id, childrenMap, subtreeIds);

        if (newParentId.HasValue && subtreeIds.Contains(newParentId.Value))
        {
            return BadRequest(new { message = "Cannot move a unit under its own descendant." });
        }

        var parentLevel = 0;
        var parentPath = "/";
        if (newParentId.HasValue)
        {
            var parentUnit = unitMap[newParentId.Value];
            parentLevel = parentUnit.Level;
            parentPath = BuildPath(parentUnit, unitMap);
        }

        var maxDepth = GetMaxDepth(id, childrenMap);
        if (parentLevel + maxDepth > MaxLevel)
        {
            return BadRequest(new { message = $"Organizational unit depth cannot exceed {MaxLevel} levels." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var actor = user.Name ?? user.Id;

        targetUnit.ParentId = newParentId;
        UpdateSubtree(targetUnit, childrenMap, parentPath, parentLevel, now, actor);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new
            {
                message = "Database constraint violation while moving organizational unit.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        var response = ToResponse(targetUnit, 0, childrenMap.TryGetValue(targetUnit.Id, out var children)
            ? children.Count
            : 0);
        return Ok(new { data = response });
    }
}
