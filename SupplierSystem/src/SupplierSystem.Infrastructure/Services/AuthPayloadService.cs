using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Services;

public sealed class AuthPayloadService : IAuthPayloadService
{
    private const string SchemaVersion = "v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthPayloadService> _logger;

    public AuthPayloadService(SupplierSystemDbContext dbContext, IMemoryCache cache, ILogger<AuthPayloadService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AuthUser?> BuildAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var cacheKey = $"auth:user:{userId}";
        if (_cache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is AuthUser cached)
        {
            return cached;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId)
            .ConfigureAwait(false);

        if (user == null)
        {
            return null;
        }

        var normalizedRole = RolePermissions.GetRoleKey(user.Role) ?? user.Role;
        var basePermissions = RolePermissions.GetPermissionsForRole(normalizedRole).ToList();
        var isFinanceCashier = string.Equals(normalizedRole, "finance_cashier", StringComparison.OrdinalIgnoreCase);

        var memberships = await (from member in _dbContext.OrganizationalUnitMembers.AsNoTracking()
                                 join unit in _dbContext.OrganizationalUnits.AsNoTracking()
                                     on member.UnitId equals unit.Id
                                 where member.UserId == userId && unit.DeletedAt == null
                                 select new { member, unit })
            .ToListAsync()
            .ConfigureAwait(false);

        var orgUnits = new List<AuthOrgUnit>();
        var functions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in memberships)
        {
            orgUnits.Add(new AuthOrgUnit
            {
                UnitId = entry.member.UnitId,
                UnitName = entry.unit.Name,
                UnitCode = entry.unit.Code,
                MemberRole = entry.member.Role,
                Function = entry.unit.Function,
            });

            if (!string.IsNullOrWhiteSpace(entry.unit.Function))
            {
                functions.Add(entry.unit.Function);
            }
        }

        var functionalPermissions = DepartmentPermissions.GetPermissionsByFunctions(functions);
        if (isFinanceCashier)
        {
            functionalPermissions = Array.Empty<string>();
        }

        var purchasingGroups = await (from member in _dbContext.PurchasingGroupMembers.AsNoTracking()
                                      join purchasingGroup in _dbContext.PurchasingGroups.AsNoTracking()
                                          on member.GroupId equals purchasingGroup.Id
                                      where member.BuyerId == userId && purchasingGroup.DeletedAt == null
                                      select new AuthPurchasingGroup
                                      {
                                          Id = purchasingGroup.Id,
                                          Code = purchasingGroup.Code,
                                          Name = purchasingGroup.Name,
                                          MemberRole = member.Role,
                                      })
            .ToListAsync()
            .ConfigureAwait(false);

        var isPurchasingGroupLeader = purchasingGroups.Any(pg =>
            string.Equals(pg.MemberRole, "lead", StringComparison.OrdinalIgnoreCase));

        var adminUnits = await _dbContext.OrganizationalUnits
            .AsNoTracking()
            .Where(unit => unit.DeletedAt == null && unit.AdminIds != null && unit.AdminIds.Contains(userId))
            .ToListAsync()
            .ConfigureAwait(false);

        var isOrgUnitAdmin = adminUnits.Count > 0;
        if (isOrgUnitAdmin && !isFinanceCashier)
        {
            functionalPermissions = functionalPermissions
                .Concat(new[] { "org.unit.supplier.manage", "org.unit.member.manage" })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var permissions = basePermissions
            .Concat(functionalPermissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = new AuthUser
        {
            SchemaVersion = SchemaVersion,
            Id = user.Id,
            Name = user.Name,
            Role = normalizedRole,
            AuthVersion = user.AuthVersion,
            SupplierId = user.SupplierId,
            TenantId = user.TenantId,
            Email = user.Email,
            Department = user.Department,
            TempAccountId = user.TempAccountId,
            AccountType = user.AccountType,
            RelatedApplicationId = user.RelatedApplicationId,
            Permissions = permissions,
            OrgUnits = orgUnits,
            AdminUnits = adminUnits.Select(unit => new AuthOrgUnit
            {
                UnitId = unit.Id,
                UnitName = unit.Name,
                UnitCode = unit.Code,
                Function = unit.Function,
            }).ToList(),
            Functions = functions.ToList(),
            PurchasingGroups = purchasingGroups,
            IsOrgUnitAdmin = isOrgUnitAdmin,
            IsPurchasingGroupLeader = isPurchasingGroupLeader,
        };

        _cache.Set(cacheKey, payload, CacheTtl);
        return payload;
    }
}
