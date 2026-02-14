using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Workflows;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Services.TempSuppliers;

public sealed partial class TempSupplierUpgradeService
{
    private static DateTimeOffset AddWorkingDays(DateTimeOffset date, int days)
    {
        return date.AddWorkingDays(days);
    }

    private static string? NormalizeDecision(string decision)
    {
        return DecisionExtensions.NormalizeDecision(decision);
    }

    private static bool HasPermission(AuthUser user, string permission)
    {
        if (user == null)
        {
            return false;
        }

        var permissions = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return permissions.Contains(permission);
    }

    private static bool HasAnyPermission(AuthUser user, IEnumerable<string> permissions)
    {
        if (user == null)
        {
            return false;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
    }

    private static void EnsureUpgradePermission(int supplierId, AuthUser? user)
    {
        if (user == null)
        {
            throw new TempSupplierUpgradeException(401, "Authentication required");
        }

        if (HasPermission(user, Permissions.PurchaserUpgradeInit))
        {
            return;
        }

        if (user.SupplierId.HasValue && user.SupplierId.Value == supplierId)
        {
            return;
        }

        throw new TempSupplierUpgradeException(403, "You do not have permission to manage this supplier upgrade");
    }

    private static bool IsPurchaser(AuthUser user)
    {
        return string.Equals(user.Role, "purchaser", StringComparison.OrdinalIgnoreCase);
    }

    private static List<int> GetPurchasingGroupIds(AuthUser user)
    {
        if (user.PurchasingGroups == null || user.PurchasingGroups.Count == 0)
        {
            return new List<int>();
        }

        return user.PurchasingGroups
            .Select(group => group.Id)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private async Task EnsureAssignedPurchaserAsync(
        AuthUser user,
        int supplierId,
        CancellationToken cancellationToken)
    {
        if (!IsPurchaser(user))
        {
            return;
        }

        var assigned = await _dbContext.BuyerSupplierAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.BuyerId == user.Id && assignment.SupplierId == supplierId,
                cancellationToken);

        if (!assigned)
        {
            var groupIds = GetPurchasingGroupIds(user);
            if (groupIds.Count > 0)
            {
                assigned = await _dbContext.PurchasingGroupSuppliers
                    .AsNoTracking()
                    .AnyAsync(
                        assignment => groupIds.Contains(assignment.GroupId) && assignment.SupplierId == supplierId,
                        cancellationToken);
            }
        }

        if (!assigned)
        {
            throw new TempSupplierUpgradeException(403, "Supplier is not assigned to this purchaser");
        }
    }

    private async Task EnsureViewPermissionAsync(int supplierId, AuthUser? user, CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new TempSupplierUpgradeException(401, "Authentication required");
        }

        var approvalPermissions = TemporarySupplierUpgradeWorkflow.Definition.Steps
            .Select(step => step.Permission);

        if (HasAnyPermission(user, approvalPermissions))
        {
            await EnsureAssignedPurchaserAsync(user, supplierId, cancellationToken);
            return;
        }

        if (HasPermission(user, Permissions.PurchaserUpgradeInit))
        {
            await EnsureAssignedPurchaserAsync(user, supplierId, cancellationToken);
            return;
        }

        if (user.SupplierId.HasValue && user.SupplierId.Value == supplierId)
        {
            return;
        }

        throw new TempSupplierUpgradeException(403, "You do not have permission to view this upgrade status");
    }
}
