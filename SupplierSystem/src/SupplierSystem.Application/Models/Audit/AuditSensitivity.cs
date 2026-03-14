namespace SupplierSystem.Application.Models.Audit;

public static class AuditSensitivity
{
    private static readonly HashSet<string> SensitiveActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "delete_file",
        "delete_document",
        "delete_supplier_document",
        "delete_contract",
        "delete_template",
        "remove_document",
        "update_role",
        "change_role",
        "update_permissions",
        "assign_permission",
        "revoke_permission",
        "grant_access",
        "revoke_access",
        "export_excel",
        "export_data",
        "export_suppliers",
        "export_contracts",
        "export_invoices",
        "download_bulk_data",
        "supplier_status_change",
        "blacklist_add",
        "whitelist_remove",
        "supplier_suspend",
        "supplier_terminate",
        "supplier_reject",
        "supplier_disqualify",
        "approve_invoice",
        "reject_invoice",
        "approve_settlement",
        "modify_payment",
        "update_system_config",
        "modify_security_settings",
        "delete_audit_log",
        "purge_data",
    };

    private static readonly HashSet<string> SensitiveEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "user_role",
        "permission",
        "system_config",
        "security_settings",
    };

    private static readonly string[] SensitiveKeywords =
    {
        "delete",
        "remove",
        "export",
        "purge",
        "blacklist",
        "terminate",
        "suspend",
        "revoke",
    };

    public static bool IsSensitiveAction(string? action, string? entityType = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var normalized = action.Trim();
        if (SensitiveActions.Contains(normalized))
        {
            return true;
        }

        if (SensitiveKeywords.Any(keyword =>
                normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(entityType) &&
            SensitiveEntityTypes.Contains(entityType.Trim()))
        {
            return true;
        }

        return false;
    }
}
