using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Services;

public static class DocumentAccess
{
    private static readonly string[] StaffPermissions =
    [
        Permissions.PurchaserSegmentManage,
        Permissions.ProcurementManagerRfqReview,
        Permissions.ProcurementDirectorRfqApprove,
        Permissions.ProcurementDirectorReportsView,
        Permissions.FinanceAccountantReconciliation,
        Permissions.FinanceDirectorRiskMonitor,
        Permissions.AdminRoleManage,
        Permissions.AdminSupplierTags
    ];

    private static readonly string[] SupplierDocumentPermissions =
    [
        Permissions.SupplierContractChecklist,
        Permissions.SupplierContractUpload
    ];

    public static bool CanAccessDocuments(AuthUser? user, int supplierId)
    {
        if (user == null)
        {
            return false;
        }

        if (IsOwner(user, supplierId) && HasAnyPermission(user, SupplierDocumentPermissions))
        {
            return true;
        }

        return HasAnyPermission(user, StaffPermissions);
    }

    public static bool CanUploadDocuments(AuthUser? user, int supplierId)
    {
        if (user == null)
        {
            return false;
        }

        if (HasAnyPermission(user, StaffPermissions))
        {
            return true;
        }

        return IsOwner(user, supplierId) && HasAnyPermission(user, Permissions.SupplierContractUpload);
    }

    private static bool IsOwner(AuthUser user, int supplierId)
    {
        return user.SupplierId.HasValue && user.SupplierId.Value == supplierId;
    }

    private static bool HasAnyPermission(AuthUser user, params string[] permissions)
    {
        if (permissions.Length == 0)
        {
            return true;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
    }
}
