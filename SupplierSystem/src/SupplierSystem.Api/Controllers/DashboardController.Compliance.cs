using System.Globalization;
using System.Text;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class DashboardController
{
    private static ComplianceSummary BuildComplianceSummary(
        Supplier supplier,
        IEnumerable<SupplierDocument> documents,
        IEnumerable<string> whitelist)
    {
        var missingProfileFields = RequiredProfileFields
            .Select(field => new
            {
                field.Key,
                field.Label,
                Value = field.ValueSelector(supplier)
            })
            .Where(item => IsValueMissing(item.Value))
            .Select(item => new MissingField { Key = item.Key, Label = item.Label })
            .ToList();

        var documentTypeSet = new HashSet<string>(
            documents
                .Select(doc => NormalizeDocKey(doc.DocType))
                .Where(key => key.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var whitelistSet = new HashSet<string>(
            whitelist
                .Select(NormalizeDocKey)
                .Where(key => key.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var missingDocumentTypes = new List<MissingDocumentType>();

        foreach (var requirement in RequiredDocumentTypes)
        {
            var keys = new List<string> { requirement.Type };
            if (requirement.Aliases.Count > 0)
            {
                keys.AddRange(requirement.Aliases);
            }

            var normalizedKeys = keys
                .Select(NormalizeDocKey)
                .Where(key => key.Length > 0)
                .ToList();

            var uploaded = normalizedKeys.Any(key => documentTypeSet.Contains(key));
            var exempted = normalizedKeys.Any(key => whitelistSet.Contains(key));

            if (!uploaded && !exempted)
            {
                missingDocumentTypes.Add(new MissingDocumentType
                {
                    Type = requirement.Type,
                    Label = requirement.Label
                });
            }
        }

        return new ComplianceSummary
        {
            MissingProfileFields = missingProfileFields,
            MissingDocumentTypes = missingDocumentTypes
        };
    }

    private static bool IsValueMissing(object? value)
    {
        if (value == null)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var _ in enumerable)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static string NormalizeDocKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var normalized = value.Trim().ToLowerInvariant();

        foreach (var ch in normalized)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                builder.Append(ch);
            }
            else if (builder.Length == 0 || builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString().Trim('_');
    }

    private static int CountExpiringDates(IEnumerable<string?> values, DateTimeOffset now)
    {
        var startDate = now.Date;
        var endDate = now.Date.AddDays(ExpiryThresholdDays);
        var count = 0;

        foreach (var value in values)
        {
            if (!TryParseDate(value, out var date))
            {
                continue;
            }

            var dateOnly = date.Date;
            if (dateOnly >= startDate && dateOnly <= endDate)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryParseDate(string? value, out DateTimeOffset date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = default;
            return false;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
        {
            return true;
        }

        return DateTimeOffset.TryParse(value, out date);
    }
}
