namespace SupplierSystem.Application.Security;

public static class DepartmentPermissions
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> FunctionPermissions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["procurement"] = new[]
            {
                "supplier.view",
                "supplier.edit",
                "supplier.create",
                "supplier.profile.view",
                "supplier.profile.edit",
                "rfq.create",
                "rfq.send",
                "rfq.view",
                "rfq.manage",
                "rfq.short.participate",
                "rfq.short.manage",
                "supplier.document.remind",
                "supplier.profile.remind",
                "supplier.data.complete.remind",
                "supplier.documents.view",
                "supplier.contracts.view",
                "supplier.upgrade.init",
                "supplier.segment.manage",
            },
            ["finance"] = new[]
            {
                "supplier.view",
                "supplier.profile.view",
                "invoice.view",
                "invoice.upload",
                "invoice.delete",
                "invoice.audit",
                "invoice.approve",
                "finance.invoice.audit",
                "finance.invoice.support",
                "supplier.payment_terms.edit",
                "supplier.payment_terms.view",
                "supplier.bank_account.view",
                "supplier.bank_account.edit",
                "finance.reconciliation.view",
                "finance.reconciliation.manage",
                "finance.risk.monitor",
            },
            ["quality"] = new[]
            {
                "supplier.view",
                "supplier.profile.view",
                "supplier.documents.view",
                "supplier.documents.audit",
                "supplier.documents.approve",
                "supplier.documents.reject",
                "supplier.profile.audit",
                "supplier.profile.verify",
                "supplier.qualifications.review",
                "supplier.rating.create",
                "supplier.rating.view",
                "supplier.rating.edit",
                "supplier.upgrade.approve.quality",
                "supplier.upgrade.review",
            },
            ["general"] = new[]
            {
                "supplier.view",
                "supplier.profile.view",
            },
        };

    public static IReadOnlyList<string> GetPermissionsByFunctions(IEnumerable<string> functions)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var function in functions)
        {
            if (FunctionPermissions.TryGetValue(function, out var permissions))
            {
                foreach (var permission in permissions)
                {
                    result.Add(permission);
                }
            }
        }

        return result.ToList();
    }
}
