using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
public sealed class SearchController : NodeControllerBase
{
    private static readonly string[] SearchPermissions =
    {
        Permissions.PurchaserSegmentManage,
        Permissions.ProcurementManagerRfqReview,
        Permissions.ProcurementDirectorReportsView,
        Permissions.FinanceAccountantReconciliation,
        Permissions.FinanceDirectorRiskMonitor,
        Permissions.AdminRoleManage,
    };

    private readonly SupplierSystemDbContext _dbContext;

    public SearchController(SupplierSystemDbContext dbContext, IWebHostEnvironment environment) : base(environment)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Search(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAnyPermission(user, SearchPermissions);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var keyword = Request.Query["q"].ToString();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Ok(new { data = new { suppliers = Array.Empty<object>(), contracts = Array.Empty<object>(), documents = Array.Empty<object>() } });
        }

        var term = $"%{keyword.Trim()}%";

        var suppliers = await _dbContext.Suppliers
            .AsNoTracking()
            .Where(s => EF.Functions.Like(s.CompanyName, term)
                        || EF.Functions.Like(s.CompanyId, term)
                        || EF.Functions.Like(s.ContactPerson ?? string.Empty, term)
                        || EF.Functions.Like(s.ContactEmail ?? string.Empty, term)
                        || EF.Functions.Like(s.Notes ?? string.Empty, term))
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.CompanyName,
                s.CompanyId,
                s.ContactPerson,
                s.Category,
                s.Status,
                s.Region,
                s.Importance,
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        var contracts = await _dbContext.Contracts
            .AsNoTracking()
            .Where(c => EF.Functions.Like(c.Title ?? string.Empty, term)
                        || EF.Functions.Like(c.AgreementNumber ?? string.Empty, term)
                        || EF.Functions.Like(c.Notes ?? string.Empty, term))
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.SupplierId,
                c.Title,
                c.AgreementNumber,
                c.Status,
                c.EffectiveTo,
                c.Amount,
                c.Currency,
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        var documents = await _dbContext.SupplierDocuments
            .AsNoTracking()
            .Where(d => EF.Functions.Like(d.OriginalName ?? string.Empty, term)
                        || EF.Functions.Like(d.DocType ?? string.Empty, term)
                        || EF.Functions.Like(d.Notes ?? string.Empty, term))
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new
            {
                d.Id,
                d.SupplierId,
                d.DocType,
                d.OriginalName,
                d.UploadedAt,
                d.ExpiresAt,
                d.Status,
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        return Ok(new { data = new { suppliers, contracts, documents } });
    }

    private static IActionResult? RequireAnyPermission(AuthUser? user, IEnumerable<string> permissions)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.Any(granted.Contains))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }
}
