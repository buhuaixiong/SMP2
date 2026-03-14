using System.Reflection;

namespace SupplierSystem.Api.Helpers;

public static class NodeCaseMapper
{
    public static Dictionary<string, object?> ToCamelCaseDictionary(object source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead)
            {
                continue;
            }

            var value = property.GetValue(source);
            result[ToCamelCase(property.Name)] = value;
        }

        return result;
    }

    public static Dictionary<string, object?> ToSnakeCaseDictionary(object source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead)
            {
                continue;
            }

            var value = property.GetValue(source);
            result[ToSnakeCase(property.Name)] = value;
        }

        return result;
    }

    public static List<Dictionary<string, object?>> ToCamelCaseList<T>(IEnumerable<T> items) where T : class
    {
        return items.Select(item => ToCamelCaseDictionary(item)).ToList();
    }

    public static List<Dictionary<string, object?>> ToSnakeCaseList<T>(IEnumerable<T> items) where T : class
    {
        return items.Select(item => ToSnakeCaseDictionary(item)).ToList();
    }

    public static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length == 1)
        {
            return value.ToLowerInvariant();
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    public static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsUpper(ch))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
