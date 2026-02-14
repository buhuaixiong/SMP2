namespace SupplierSystem.Api.Controllers;

public static class QuoteTaxStatusNormalizer
{
    public static bool TryNormalize(string? raw, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            normalized = null;
            return true;
        }

        var value = raw.Trim();

        if (value.Equals("inclusive", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("含税", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "inclusive";
            return true;
        }

        if (value.Equals("exclusive", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("不含税", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "exclusive";
            return true;
        }

        normalized = null;
        return false;
    }
}
