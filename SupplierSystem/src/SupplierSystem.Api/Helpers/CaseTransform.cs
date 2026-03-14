using System.Collections;
using System.Text.RegularExpressions;

namespace SupplierSystem.Api.Helpers;

public static class CaseTransform
{
    private static readonly Regex SnakeCaseRegex = new("_([a-z])", RegexOptions.Compiled);

    public static object? ToCamelCase(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string)
        {
            return value;
        }

        if (value is System.Text.Json.JsonElement)
        {
            return value;
        }

        if (value is IDictionary<string, object?> dict)
        {
            var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (key, val) in dict)
            {
                mapped[ToCamelKey(key)] = ToCamelCase(val);
            }
            return mapped;
        }

        if (value is IDictionary<string, object> objDict)
        {
            var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (key, val) in objDict)
            {
                mapped[ToCamelKey(key)] = ToCamelCase(val);
            }
            return mapped;
        }

        if (value is IEnumerable enumerable)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(ToCamelCase(item));
            }
            return list;
        }

        return value;
    }

    private static string ToCamelKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return key;
        }

        return SnakeCaseRegex.Replace(key, match => match.Groups[1].Value.ToUpperInvariant());
    }
}
