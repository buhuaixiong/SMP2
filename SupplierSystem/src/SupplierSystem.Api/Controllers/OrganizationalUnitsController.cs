using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Authorization;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Services;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.OrgUnitsManage)]
[TenantContextRequired]
[Route("api/org-units")]
public sealed partial class OrganizationalUnitsController : ControllerBase
{
    private const int MaxLevel = 5;
    private const int MaxCodeLength = 50;
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 500;
    private const int MaxCategoryLength = 100;
    private const int MaxRegionLength = 100;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "general",
        "department",
        "division",
        "procurement"
    };
    private readonly OrganizationalUnitDataService _organizationalUnitDataService;

    public OrganizationalUnitsController(OrganizationalUnitDataService organizationalUnitDataService)
    {
        _organizationalUnitDataService = organizationalUnitDataService;
    }

    [HttpGet]
    public async Task<IActionResult> ListUnits(
        [FromQuery] string? type,
        [FromQuery] string? isActive,
        [FromQuery] string? format,
        [FromQuery] string? parentId,
        CancellationToken cancellationToken)
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

        var query = _organizationalUnitDataService.QueryUnits()
            .Where(unit => unit.TenantId == tenantId && unit.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(unit => unit.Type == type);
        }

        if (TryParseBool(isActive, out var active))
        {
            query = query.Where(unit => unit.IsActive == active);
        }

        if (TryParseInt(parentId, out var parentIdValue))
        {
            query = query.Where(unit => unit.ParentId == parentIdValue);
        }

        var units = await query
            .OrderBy(unit => unit.Level)
            .ThenBy(unit => unit.Name)
            .ToListAsync(cancellationToken);

        if (units.Count == 0)
        {
            return Ok(new { data = Array.Empty<OrgUnitResponse>() });
        }

        var unitIds = units.Select(unit => unit.Id).ToList();
        var memberCounts = await _organizationalUnitDataService.QueryMembers()
            .Where(member => member.TenantId == tenantId && unitIds.Contains(member.UnitId))
            .GroupBy(member => member.UnitId)
            .Select(group => new { UnitId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var memberCountMap = memberCounts.ToDictionary(entry => entry.UnitId, entry => entry.Count);

        var childCounts = await _organizationalUnitDataService.QueryUnits()
            .Where(unit => unit.TenantId == tenantId && unit.DeletedAt == null && unit.ParentId != null && unitIds.Contains(unit.ParentId.Value))
            .GroupBy(unit => unit.ParentId!.Value)
            .Select(group => new { ParentId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var childCountMap = childCounts.ToDictionary(entry => entry.ParentId, entry => entry.Count);

        var payload = units
            .Select(unit => ToResponse(
                unit,
                memberCountMap.TryGetValue(unit.Id, out var memberCount) ? memberCount : 0,
                childCountMap.TryGetValue(unit.Id, out var childCount) ? childCount : 0))
            .ToList();

        return Ok(new { data = payload });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUnit(int id, CancellationToken cancellationToken)
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

        var unit = await _organizationalUnitDataService.QueryUnits()
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && item.DeletedAt == null, cancellationToken);

        if (unit == null)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        var members = await (from member in _organizationalUnitDataService.QueryMembers()
                             join userEntry in _organizationalUnitDataService.QueryUsers()
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

        var childCount = await _organizationalUnitDataService.QueryUnits()
            .CountAsync(item => item.ParentId == id && item.TenantId == tenantId && item.DeletedAt == null, cancellationToken);

        var descendantCount = 0;
        if (!string.IsNullOrWhiteSpace(unit.Path))
        {
            descendantCount = await _organizationalUnitDataService.QueryUnits()
                .CountAsync(item => item.TenantId == tenantId
                                    && item.DeletedAt == null
                                    && item.Path != null
                                    && item.Path.StartsWith(unit.Path)
                                    && item.Id != unit.Id,
                    cancellationToken);
        }

        var ancestors = await LoadAncestorsAsync(unit, tenantId, cancellationToken);

        var response = new OrgUnitDetailResponse
        {
            Id = unit.Id,
            Code = unit.Code,
            Name = unit.Name,
            Type = unit.Type,
            ParentId = unit.ParentId,
            Level = unit.Level,
            Path = unit.Path,
            Description = unit.Description,
            AdminIds = ParseAdminIds(unit.AdminIds),
            Function = unit.Function,
            Category = unit.Category,
            Region = unit.Region,
            IsActive = unit.IsActive ? 1 : 0,
            CreatedAt = unit.CreatedAt,
            CreatedBy = unit.CreatedBy,
            UpdatedAt = unit.UpdatedAt,
            UpdatedBy = unit.UpdatedBy,
            DeletedAt = unit.DeletedAt,
            DeletedBy = unit.DeletedBy,
            MemberCount = members.Count,
            SupplierCount = 0,
            ContractCount = 0,
            ChildCount = childCount,
            Members = members,
            Suppliers = new List<OrgUnitSupplierResponse>(),
            Contracts = new List<OrgUnitContractResponse>(),
            Ancestors = ancestors,
            DescendantCount = descendantCount
        };

        return Ok(new { data = response });
    }

}
