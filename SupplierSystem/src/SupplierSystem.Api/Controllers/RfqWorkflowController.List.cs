using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListRfqs(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove,
            Permissions.DepartmentRequisitionManage,
            Permissions.SupplierRfqShort,
            Permissions.SupplierRfqLong,
            Permissions.SupplierRfqReview);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var user = HttpContext.GetAuthUser();
        var page = ParseInt(Request.Query["page"], 1);
        var pageSize = ParseInt(Request.Query["pageSize"], ParseInt(Request.Query["limit"], 20));
        page = Math.Max(1, page);
        pageSize = Math.Min(100, Math.Max(1, pageSize));

        var keyword = GetSearchKeyword(Request.Query);
        var createdBy = string.IsNullOrWhiteSpace(Request.Query["createdBy"]) ? null : Request.Query["createdBy"].ToString();
        var status = string.IsNullOrWhiteSpace(Request.Query["status"]) ? null : Request.Query["status"].ToString();
        var rfqType = string.IsNullOrWhiteSpace(Request.Query["rfqType"]) ? null : Request.Query["rfqType"].ToString();
        var materialType = string.IsNullOrWhiteSpace(Request.Query["materialType"]) ? null : Request.Query["materialType"].ToString();
        var distributionCategory = string.IsNullOrWhiteSpace(Request.Query["distributionCategory"]) ? null : Request.Query["distributionCategory"].ToString();
        var distributionSubcategory = string.IsNullOrWhiteSpace(Request.Query["distributionSubcategory"]) ? null : Request.Query["distributionSubcategory"].ToString();

        var query = _rfqWorkflowStore.QueryRfqs();

        if (user != null)
        {
            var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var hasViewAll = granted.Contains(Permissions.RfqViewAll) ||
                             string.Equals(user.Role, "procurement_manager", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(user.Role, "procurement_director", StringComparison.OrdinalIgnoreCase);

            if (!hasViewAll)
            {
                if (user.SupplierId != null)
                {
                    var invitedRfqIds = await _rfqWorkflowStore.LoadInvitedRfqIdsAsync(user.SupplierId.Value, cancellationToken);
                    query = query.Where(r => invitedRfqIds.Contains(r.Id));
                }
                else if (!string.Equals(user.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r => r.CreatedBy == user.Id);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(rfqType))
        {
            query = query.Where(r => r.RfqType == rfqType);
        }

        if (!string.IsNullOrWhiteSpace(materialType))
        {
            query = query.Where(r =>
                r.MaterialCategoryType == materialType ||
                r.MaterialType == materialType);
        }

        if (!string.IsNullOrWhiteSpace(distributionCategory))
        {
            query = query.Where(r => r.DistributionCategory == distributionCategory);
        }

        if (!string.IsNullOrWhiteSpace(distributionSubcategory))
        {
            query = query.Where(r => r.DistributionSubcategory == distributionSubcategory);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(r => (r.Title ?? string.Empty).Contains(keyword) ||
                                     (r.Description ?? string.Empty).Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(createdBy))
        {
            query = query.Where(r => r.CreatedBy == createdBy);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var data = rows.Select(NodeCaseMapper.ToCamelCaseDictionary).ToList();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        return Ok(new
        {
            data,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages,
                hasNext = page < totalPages,
                hasPrev = page > 1,
            },
        });
    }
}
