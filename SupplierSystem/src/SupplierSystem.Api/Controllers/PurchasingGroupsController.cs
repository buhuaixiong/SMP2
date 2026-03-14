using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Models.Requests;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/purchasing-groups")]
public sealed class PurchasingGroupsController : ControllerBase
{
    private readonly PurchasingGroupDataService _purchasingGroupDataService;
    private readonly ILogger<PurchasingGroupsController> _logger;

    public PurchasingGroupsController(PurchasingGroupDataService purchasingGroupDataService, ILogger<PurchasingGroupsController> logger)
    {
        _purchasingGroupDataService = purchasingGroupDataService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> ListGroups([FromQuery] string? isActive, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && leaderGroupIds.Count == 0)
        {
            return StatusCode(403, new { message = "Access denied to purchasing groups." });
        }

        var query = _purchasingGroupDataService.QueryGroups()
            .Where(group => group.DeletedAt == null);

        if (TryParseBool(isActive, out var active))
        {
            query = query.Where(group => group.IsActive == active);
        }

        if (!hasAdminAccess)
        {
            query = query.Where(group => leaderGroupIds.Contains(group.Id));
        }

        var groups = await query
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);

        return Ok(new { data = groups });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGroup(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && !leaderGroupIds.Contains(id))
        {
            return StatusCode(403, new { message = "Access denied to this purchasing group." });
        }

        var group = await _purchasingGroupDataService.QueryGroups()
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (group == null)
        {
            return NotFound(new { message = "Purchasing group not found." });
        }

        return Ok(new { data = group });
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup(
        [FromBody] CreatePurchasingGroupRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!HasGroupAdminPermission(user))
        {
            return StatusCode(403, new { message = "Access denied to create purchasing group." });
        }

        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        const int maxCodeLength = 50;
        const int maxNameLength = 200;
        const int maxDescriptionLength = 500;
        const int maxCategoryLength = 100;
        const int maxRegionLength = 100;

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Group code is required." });
        }

        var code = request.Code.Trim();
        if (code.Length > maxCodeLength)
        {
            return BadRequest(new { message = $"Group code must be {maxCodeLength} characters or less." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Group name is required." });
        }

        var name = request.Name.Trim();
        if (name.Length > maxNameLength)
        {
            return BadRequest(new { message = $"Group name must be {maxNameLength} characters or less." });
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description != null && description.Length > maxDescriptionLength)
        {
            return BadRequest(new { message = $"Description must be {maxDescriptionLength} characters or less." });
        }

        var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        if (category != null && category.Length > maxCategoryLength)
        {
            return BadRequest(new { message = $"Category must be {maxCategoryLength} characters or less." });
        }

        var region = string.IsNullOrWhiteSpace(request.Region) ? null : request.Region.Trim();
        if (region != null && region.Length > maxRegionLength)
        {
            return BadRequest(new { message = $"Region must be {maxRegionLength} characters or less." });
        }

        var exists = await _purchasingGroupDataService.QueryGroups()
            .AnyAsync(group => group.Code == code && group.DeletedAt == null, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "Purchasing group code already exists." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var actor = user.Name ?? user.Id;

        var group = new PurchasingGroup
        {
            Code = code,
            Name = name,
            Description = description,
            Category = category,
            Region = region,
            IsActive = request.IsActive ?? true,
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor
        };

        _purchasingGroupDataService.AddGroup(group);

        try
        {
            await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to create purchasing group.");
            return Conflict(new
            {
                message = "Database constraint violation while creating purchasing group.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        return StatusCode(201, new { data = group });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateGroup(
        int id,
        [FromBody] UpdatePurchasingGroupRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!HasGroupAdminPermission(user))
        {
            return StatusCode(403, new { message = "Access denied to update purchasing group." });
        }

        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        const int maxNameLength = 200;
        const int maxDescriptionLength = 500;
        const int maxCategoryLength = 100;
        const int maxRegionLength = 100;

        var group = await _purchasingGroupDataService.QueryGroups(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (group == null)
        {
            return NotFound(new { message = "Purchasing group not found." });
        }

        var isActiveChanged = request.IsActive.HasValue && group.IsActive != request.IsActive.Value;

        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Group name is required." });
            }

            var trimmedName = request.Name.Trim();
            if (trimmedName.Length > maxNameLength)
            {
                return BadRequest(new { message = $"Group name must be {maxNameLength} characters or less." });
            }

            group.Name = trimmedName;
        }

        if (request.Description != null)
        {
            var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            if (description != null && description.Length > maxDescriptionLength)
            {
                return BadRequest(new { message = $"Description must be {maxDescriptionLength} characters or less." });
            }

            group.Description = description;
        }

        if (request.Category != null)
        {
            var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
            if (category != null && category.Length > maxCategoryLength)
            {
                return BadRequest(new { message = $"Category must be {maxCategoryLength} characters or less." });
            }

            group.Category = category;
        }

        if (request.Region != null)
        {
            var region = string.IsNullOrWhiteSpace(request.Region) ? null : request.Region.Trim();
            if (region != null && region.Length > maxRegionLength)
            {
                return BadRequest(new { message = $"Region must be {maxRegionLength} characters or less." });
            }

            group.Region = region;
        }

        if (request.IsActive.HasValue)
        {
            group.IsActive = request.IsActive.Value;
        }

        group.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        group.UpdatedBy = user.Name ?? user.Id;

        try
        {
            await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to update purchasing group {GroupId}.", id);
            return Conflict(new
            {
                message = "Database constraint violation while updating purchasing group.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        if (isActiveChanged)
        {
            try
            {
                await RebuildAccessCacheInternal(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rebuild access cache after purchasing group update.");
            }
        }

        return Ok(new { data = group });
    }

    [HttpGet("{id:int}/members")]
    public async Task<IActionResult> GetMembers(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && !leaderGroupIds.Contains(id))
        {
            return StatusCode(403, new { message = "Access denied to purchasing group members." });
        }

        var groupExists = await _purchasingGroupDataService.QueryGroups()
            .AnyAsync(group => group.Id == id && group.DeletedAt == null, cancellationToken);
        if (!groupExists)
        {
            return NotFound(new { message = "Purchasing group not found." });
        }

        var members = await (from member in _purchasingGroupDataService.QueryGroupMembers()
                             join buyer in _purchasingGroupDataService.QueryUsers()
                                 on member.BuyerId equals buyer.Id into buyers
                             from buyer in buyers.DefaultIfEmpty()
                             where member.GroupId == id
                             orderby buyer.Name ?? member.BuyerId
                             select new
                             {
                                 member.Id,
                                 member.GroupId,
                                 member.BuyerId,
                                 member.Role,
                                 member.JoinedAt,
                                 member.AssignedBy,
                                 member.Notes,
                                 BuyerName = buyer != null ? buyer.Name : null,
                                 BuyerRole = buyer != null ? buyer.Role : null
                             })
            .ToListAsync(cancellationToken);

        return Ok(new { data = members });
    }

    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> AddMembers(
        int id,
        [FromBody] AddPurchasingGroupMembersRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && !leaderGroupIds.Contains(id))
        {
            return StatusCode(403, new { message = "Access denied to purchasing group members." });
        }

        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        var buyerIds = request.BuyerIds?
            .Where(buyerId => !string.IsNullOrWhiteSpace(buyerId))
            .Select(buyerId => buyerId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (buyerIds == null || buyerIds.Count == 0)
        {
            return BadRequest(new { message = "Buyer IDs array is required." });
        }

        var groupExists = await _purchasingGroupDataService.QueryGroups()
            .AnyAsync(group => group.Id == id && group.DeletedAt == null, cancellationToken);
        if (!groupExists)
        {
            return NotFound(new { message = "Purchasing group not found." });
        }

        var normalizedRole = string.IsNullOrWhiteSpace(request.Role) ? "member" : request.Role.Trim();
        var normalizedNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var now = DateTimeOffset.UtcNow.ToString("o");
        var assignedBy = user.Name ?? user.Id;

        var existingMembers = await _purchasingGroupDataService.QueryGroupMembers(asNoTracking: false)
            .Where(member => member.GroupId == id && buyerIds.Contains(member.BuyerId))
            .ToListAsync(cancellationToken);
        var existingByBuyerId = existingMembers.ToDictionary(member => member.BuyerId, StringComparer.OrdinalIgnoreCase);

        foreach (var buyerId in buyerIds)
        {
            if (existingByBuyerId.TryGetValue(buyerId, out var member))
            {
                member.Role = normalizedRole;
                member.Notes = normalizedNotes;
                continue;
            }

            _purchasingGroupDataService.AddGroupMember(new PurchasingGroupMember
            {
                GroupId = id,
                BuyerId = buyerId,
                Role = normalizedRole,
                JoinedAt = now,
                AssignedBy = assignedBy,
                Notes = normalizedNotes
            });
        }

        try
        {
            await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to add members to purchasing group {GroupId}.", id);
            return Conflict(new
            {
                message = "Database constraint violation while adding members to purchasing group.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        try
        {
            await RebuildAccessCacheInternal(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild access cache after adding members.");
        }

        return Ok(new
        {
            message = $"Added {buyerIds.Count} buyer(s) to group.",
            data = new { addedCount = buyerIds.Count }
        });
    }

    [HttpDelete("{id:int}/members/{buyerId}")]
    public async Task<IActionResult> RemoveMember(int id, string buyerId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && !leaderGroupIds.Contains(id))
        {
            return StatusCode(403, new { message = "Access denied to purchasing group members." });
        }

        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return BadRequest(new { message = "Buyer ID is required." });
        }

        var normalizedBuyerId = buyerId.Trim();
        var member = await _purchasingGroupDataService.QueryGroupMembers(asNoTracking: false)
            .FirstOrDefaultAsync(m => m.GroupId == id && m.BuyerId == normalizedBuyerId, cancellationToken);
        if (member == null)
        {
            return NoContent();
        }

        _purchasingGroupDataService.RemoveGroupMember(member);
        await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);

        try
        {
            await RebuildAccessCacheInternal(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild access cache after removing member.");
        }

        return NoContent();
    }

    [HttpGet("{id:int}/suppliers")]
    public async Task<IActionResult> GetSuppliers(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && !leaderGroupIds.Contains(id))
        {
            return StatusCode(403, new { message = "Access denied to purchasing group suppliers." });
        }

        var groupExists = await _purchasingGroupDataService.QueryGroups()
            .AnyAsync(group => group.Id == id && group.DeletedAt == null, cancellationToken);
        if (!groupExists)
        {
            return NotFound(new { message = "Purchasing group not found." });
        }

        var suppliers = await (from link in _purchasingGroupDataService.QueryGroupSuppliers()
                               join supplier in _purchasingGroupDataService.QuerySuppliers()
                                    on link.SupplierId equals supplier.Id into suppliersJoin
                               from supplier in suppliersJoin.DefaultIfEmpty()
                               where link.GroupId == id
                               orderby supplier.CompanyName ?? supplier.CompanyId ?? string.Empty
                               select new
                               {
                                   link.Id,
                                   link.GroupId,
                                   link.SupplierId,
                                   CompanyName = supplier != null ? supplier.CompanyName : null,
                                   CompanyId = supplier != null ? supplier.CompanyId : null,
                                   Category = supplier != null ? supplier.Category : null,
                                   Region = supplier != null ? supplier.Region : null,
                                   Status = supplier != null ? supplier.Status : null,
                                   IsPrimary = link.IsPrimary.HasValue ? (link.IsPrimary.Value ? 1 : 0) : 0,
                                   link.AssignedAt,
                                   link.AssignedBy,
                                   link.Notes
                               })
            .ToListAsync(cancellationToken);

        return Ok(new { data = suppliers });
    }

    [HttpPost("{id:int}/suppliers")]
    public async Task<IActionResult> AddSuppliers(
        int id,
        [FromBody] AddPurchasingGroupSuppliersRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!HasGroupAdminPermission(user))
        {
            return StatusCode(403, new { message = "Access denied to purchasing group suppliers." });
        }

        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        var supplierIds = request.SupplierIds?
            .Where(supplierId => supplierId > 0)
            .Distinct()
            .ToList();
        if (supplierIds == null || supplierIds.Count == 0)
        {
            return BadRequest(new { message = "Supplier IDs array is required." });
        }

        var groupExists = await _purchasingGroupDataService.QueryGroups()
            .AnyAsync(group => group.Id == id && group.DeletedAt == null, cancellationToken);
        if (!groupExists)
        {
            return NotFound(new { message = "Purchasing group not found." });
        }

        var normalizedNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var isPrimary = request.IsPrimary ?? false;
        var now = DateTimeOffset.UtcNow.ToString("o");
        var assignedBy = user.Name ?? user.Id;

        var existingLinks = await _purchasingGroupDataService.QueryGroupSuppliers(asNoTracking: false)
            .Where(link => link.GroupId == id && supplierIds.Contains(link.SupplierId))
            .ToListAsync(cancellationToken);
        var existingBySupplierId = existingLinks.ToDictionary(link => link.SupplierId);

        foreach (var supplierId in supplierIds)
        {
            if (existingBySupplierId.TryGetValue(supplierId, out var link))
            {
                link.IsPrimary = isPrimary;
                link.Notes = normalizedNotes;
                continue;
            }

            _purchasingGroupDataService.AddGroupSupplier(new PurchasingGroupSupplier
            {
                GroupId = id,
                SupplierId = supplierId,
                AssignedAt = now,
                AssignedBy = assignedBy,
                IsPrimary = isPrimary,
                Notes = normalizedNotes
            });
        }

        try
        {
            await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to add suppliers to purchasing group {GroupId}.", id);
            return Conflict(new
            {
                message = "Database constraint violation while adding suppliers to purchasing group.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        try
        {
            await RebuildAccessCacheInternal(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild access cache after adding suppliers.");
        }

        return Ok(new
        {
            message = $"Added {supplierIds.Count} supplier(s) to group.",
            data = new { addedCount = supplierIds.Count }
        });
    }

    [HttpDelete("{id:int}/suppliers/{supplierId:int}")]
    public async Task<IActionResult> RemoveSupplier(int id, int supplierId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!HasGroupAdminPermission(user))
        {
            return StatusCode(403, new { message = "Access denied to purchasing group suppliers." });
        }

        if (supplierId <= 0)
        {
            return BadRequest(new { message = "Supplier ID is required." });
        }

        var link = await _purchasingGroupDataService.QueryGroupSuppliers(asNoTracking: false)
            .FirstOrDefaultAsync(s => s.GroupId == id && s.SupplierId == supplierId, cancellationToken);
        if (link == null)
        {
            return NoContent();
        }

        _purchasingGroupDataService.RemoveGroupSupplier(link);
        await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);

        try
        {
            await RebuildAccessCacheInternal(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild access cache after removing supplier.");
        }

        return NoContent();
    }

    [HttpGet("{id:int}/buyer-assignments")]
    public async Task<IActionResult> GetBuyerAssignments(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && !leaderGroupIds.Contains(id))
        {
            return StatusCode(403, new { message = "Access denied to this purchasing group." });
        }

        var assignments = await (from assignment in _purchasingGroupDataService.QueryBuyerSupplierAssignments()
                                 join member in _purchasingGroupDataService.QueryGroupMembers()
                                      on new { GroupId = id, BuyerId = assignment.BuyerId }
                                      equals new { member.GroupId, member.BuyerId }
                                 join groupSupplier in _purchasingGroupDataService.QueryGroupSuppliers()
                                      on new { GroupId = id, SupplierId = assignment.SupplierId }
                                      equals new { groupSupplier.GroupId, groupSupplier.SupplierId }
                                 join buyer in _purchasingGroupDataService.QueryUsers()
                                      on assignment.BuyerId equals buyer.Id into buyers
                                     from buyer in buyers.DefaultIfEmpty()
                                 join supplier in _purchasingGroupDataService.QuerySuppliers()
                                      on assignment.SupplierId equals supplier.Id into suppliers
                                 from supplier in suppliers.DefaultIfEmpty()
                                 orderby buyer.Name ?? assignment.BuyerId, supplier.CompanyName ?? supplier.CompanyId ?? string.Empty
                                 select new
                                 {
                                     assignment.Id,
                                     assignment.BuyerId,
                                     BuyerName = buyer != null ? buyer.Name : null,
                                     BuyerRole = buyer != null ? buyer.Role : null,
                                     assignment.SupplierId,
                                     CompanyName = supplier != null ? supplier.CompanyName : null,
                                     CompanyId = supplier != null ? supplier.CompanyId : null,
                                     Category = supplier != null ? supplier.Category : null,
                                     Region = supplier != null ? supplier.Region : null,
                                     GroupIsPrimary = groupSupplier.IsPrimary,
                                     GroupNotes = groupSupplier.Notes,
                                     assignment.CreatedAt,
                                     assignment.CreatedBy
                                 })
            .ToListAsync(cancellationToken);

        return Ok(new { data = assignments });
    }

    [HttpPost("{id:int}/assign-to-buyer")]
    public async Task<IActionResult> AssignSuppliersToBuyer(
        int id,
        [FromBody] AssignSuppliersToBuyerRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && !leaderGroupIds.Contains(id))
        {
            return StatusCode(403, new { message = "Access denied to this purchasing group." });
        }

        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.BuyerId))
        {
            return BadRequest(new { message = "Buyer ID is required." });
        }

        if (request.SupplierIds == null || request.SupplierIds.Count == 0)
        {
            return BadRequest(new { message = "Supplier IDs array is required." });
        }

        var groupExists = await _purchasingGroupDataService.QueryGroups()
            .AnyAsync(group => group.Id == id && group.DeletedAt == null, cancellationToken);
        if (!groupExists)
        {
            return NotFound(new { message = "Purchasing group not found." });
        }

        var buyerId = request.BuyerId.Trim();
        var isMember = await _purchasingGroupDataService.QueryGroupMembers()
            .AnyAsync(member => member.GroupId == id && member.BuyerId == buyerId, cancellationToken);
        if (!isMember)
        {
            return BadRequest(new { message = "Buyer is not a member of this purchasing group." });
        }

        var supplierIds = request.SupplierIds
            .Where(supplierId => supplierId > 0)
            .Distinct()
            .ToList();

        if (supplierIds.Count == 0)
        {
            return BadRequest(new { message = "Valid supplier IDs are required." });
        }

        var allowedSupplierIds = await _purchasingGroupDataService.QueryGroupSuppliers()
            .Where(link => link.GroupId == id && supplierIds.Contains(link.SupplierId))
            .Select(link => link.SupplierId)
            .ToListAsync(cancellationToken);

        var invalidSupplierIds = supplierIds.Except(allowedSupplierIds).ToList();
        if (invalidSupplierIds.Count > 0)
        {
            return BadRequest(new
            {
                message = "Some suppliers are not part of this purchasing group.",
                details = new { invalidSupplierIds }
            });
        }

        var existingSupplierIds = await _purchasingGroupDataService.QueryBuyerSupplierAssignments()
            .Where(assignment => assignment.BuyerId == buyerId && allowedSupplierIds.Contains(assignment.SupplierId))
            .Select(assignment => assignment.SupplierId)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow.ToString("o");
        var createdBy = user.Name ?? user.Id;
        var assignedCount = 0;

        await using var transaction = await _purchasingGroupDataService.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var supplierId in allowedSupplierIds.Except(existingSupplierIds))
            {
                _purchasingGroupDataService.AddBuyerSupplierAssignment(new BuyerSupplierAssignment
                {
                    BuyerId = buyerId,
                    SupplierId = supplierId,
                    CreatedAt = now,
                    CreatedBy = createdBy,
                });
                assignedCount++;
            }

            await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to assign suppliers for purchasing group {GroupId}.", id);
            return Conflict(new
            {
                message = "Database constraint violation while assigning suppliers to buyer.",
                details = ex.InnerException?.Message ?? ex.Message
            });
        }

        if (assignedCount > 0)
        {
            try
            {
                await RebuildAccessCacheInternal(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rebuild access cache after assigning suppliers.");
            }
        }

        return Ok(new
        {
            message = $"Assigned {assignedCount} supplier(s) to buyer.",
            data = new
            {
                assignedCount,
                totalRequested = supplierIds.Count
            }
        });
    }

    [HttpDelete("{id:int}/buyer-assignments/{assignmentId:int}")]
    public async Task<IActionResult> RemoveBuyerAssignment(
        int id,
        int assignmentId,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var hasAdminAccess = HasGroupAdminPermission(user);
        var leaderGroupIds = GetLeaderGroupIds(user);
        if (!hasAdminAccess && !leaderGroupIds.Contains(id))
        {
            return StatusCode(403, new { message = "Access denied to this purchasing group." });
        }

        var assignment = await _purchasingGroupDataService.QueryBuyerSupplierAssignments(asNoTracking: false)
            .FirstOrDefaultAsync(entry => entry.Id == assignmentId, cancellationToken);

        if (assignment == null)
        {
            return NotFound(new { message = "Assignment not found." });
        }

        var membershipExists = await _purchasingGroupDataService.QueryGroupMembers()
            .AnyAsync(member => member.GroupId == id && member.BuyerId == assignment.BuyerId, cancellationToken);
        var supplierLinked = await _purchasingGroupDataService.QueryGroupSuppliers()
            .AnyAsync(link => link.GroupId == id && link.SupplierId == assignment.SupplierId, cancellationToken);

        if (!membershipExists || !supplierLinked)
        {
            return StatusCode(403, new { message = "Access denied to this assignment." });
        }

        _purchasingGroupDataService.RemoveBuyerSupplierAssignment(assignment);
        await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);

        try
        {
            await RebuildAccessCacheInternal(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild access cache after removing assignment.");
        }

        return NoContent();
    }

    [HttpPost("admin/rebuild-cache")]
    public async Task<IActionResult> RebuildAccessCache(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!HasGroupAdminPermission(user))
        {
            return StatusCode(403, new { message = "Access denied to rebuild access cache." });
        }

        try
        {
            var (directCount, groupCount) = await RebuildAccessCacheInternal(cancellationToken);

            return Ok(new
            {
                message = "Access cache rebuilt successfully.",
                data = new
                {
                    direct = directCount,
                    group = groupCount,
                    total = directCount + groupCount
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to rebuild access cache.", error = ex.Message });
        }
    }

    private async Task<(int directCount, int groupCount)> RebuildAccessCacheInternal(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");

        await using var transaction = await _purchasingGroupDataService.BeginTransactionAsync(cancellationToken);
        await _purchasingGroupDataService.ClearAccessCacheAsync(cancellationToken);

        var directEntries = await _purchasingGroupDataService.QueryBuyerSupplierAssignments()
            .Select(assignment => new { assignment.BuyerId, assignment.SupplierId })
            .Distinct()
            .Select(assignment => new BuyerSupplierAccessCache
            {
                BuyerId = assignment.BuyerId,
                SupplierId = assignment.SupplierId,
                AccessType = "direct",
                LastUpdated = now
            })
            .ToListAsync(cancellationToken);

        var groupLinks = await (from member in _purchasingGroupDataService.QueryGroupMembers()
                                join purchasingGroup in _purchasingGroupDataService.QueryGroups()
                                    on member.GroupId equals purchasingGroup.Id
                                join supplier in _purchasingGroupDataService.QueryGroupSuppliers()
                                    on purchasingGroup.Id equals supplier.GroupId
                                where purchasingGroup.IsActive && purchasingGroup.DeletedAt == null
                                select new
                                {
                                    member.BuyerId,
                                    supplier.SupplierId,
                                    GroupId = purchasingGroup.Id,
                                    GroupName = purchasingGroup.Name
                                })
            .ToListAsync(cancellationToken);

        var groupEntries = groupLinks
            .GroupBy(link => new { link.BuyerId, link.SupplierId })
            .Select(group => group.OrderBy(link => link.GroupId).First())
            .Select(link => new BuyerSupplierAccessCache
            {
                BuyerId = link.BuyerId,
                SupplierId = link.SupplierId,
                AccessType = "group",
                GroupId = link.GroupId,
                GroupName = link.GroupName,
                LastUpdated = now
            })
            .ToList();

        if (directEntries.Count > 0)
        {
            _purchasingGroupDataService.AddAccessCacheEntries(directEntries);
        }

        if (groupEntries.Count > 0)
        {
            _purchasingGroupDataService.AddAccessCacheEntries(groupEntries);
        }

        await _purchasingGroupDataService.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (directEntries.Count, groupEntries.Count);
    }

    private static bool HasGroupAdminPermission(AuthUser user)
    {
        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return granted.Contains(Permissions.AdminPurchasingGroupsManage);
    }

    private static List<int> GetLeaderGroupIds(AuthUser user)
    {
        if (user.PurchasingGroups == null || user.PurchasingGroups.Count == 0)
        {
            return new List<int>();
        }

        return user.PurchasingGroups
            .Where(group => group.Id > 0)
            .Where(group =>
                string.Equals(group.MemberRole, "lead", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(group.MemberRole, "leader", StringComparison.OrdinalIgnoreCase))
            .Select(group => group.Id)
            .Distinct()
            .ToList();
    }

    private static bool TryParseBool(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (value == "1")
        {
            result = true;
            return true;
        }

        if (value == "0")
        {
            result = false;
            return true;
        }

        return false;
    }
}
