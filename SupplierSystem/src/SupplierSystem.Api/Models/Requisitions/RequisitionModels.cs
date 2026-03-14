using System.Text.Json;
using SupplierSystem.Application.Exceptions;

namespace SupplierSystem.Api.Models.Requisitions;

public sealed class RequisitionCreateRequest
{
    public string RequestingDepartment { get; set; } = string.Empty;
    public string RequiredDate { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string? Notes { get; set; }
    public string? PrNumber { get; set; }
    public List<RequisitionItemInput> Items { get; set; } = new();
}

public sealed class RequisitionUpdateRequest
{
    public string RequestingDepartment { get; set; } = string.Empty;
    public string RequiredDate { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string? Notes { get; set; }
    public List<RequisitionItemInput> Items { get; set; } = new();
}

public sealed class RequisitionListQuery
{
    public string? Status { get; set; }
    public string? RequestingDepartment { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}

public sealed class RequisitionItemInput
{
    public string ItemType { get; set; } = string.Empty;
    public string? ItemSubtype { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? ItemDescription { get; set; }
    public string? Specifications { get; set; }
    public decimal? EstimatedBudget { get; set; }
    public string? Currency { get; set; }
}

public static class RequisitionPayloadParser
{
    public static List<RequisitionItemInput> ParseItems(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ValidationErrorException("Invalid items format.");
        }

        using var doc = JsonDocument.Parse(raw);
        return ParseItems(doc.RootElement);
    }

    public static List<RequisitionItemInput> ParseItems(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ValidationErrorException("Invalid items format.");
        }

        var results = new List<RequisitionItemInput>();
        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var itemType = GetString(entry, "item_type", "itemType") ?? string.Empty;
            var itemSubtype = GetString(entry, "item_subtype", "itemSubtype");
            var itemName = GetString(entry, "item_name", "itemName") ?? string.Empty;
            var quantity = GetDecimal(entry, "quantity") ?? 0m;
            var unit = GetString(entry, "unit");
            var itemDescription = GetString(entry, "item_description", "itemDescription");
            var specifications = GetString(entry, "specifications");
            var estimatedBudget = GetDecimal(entry, "estimated_budget", "estimatedBudget");
            var currency = GetString(entry, "currency");

            results.Add(new RequisitionItemInput
            {
                ItemType = itemType,
                ItemSubtype = itemSubtype,
                ItemName = itemName,
                Quantity = quantity,
                Unit = unit,
                ItemDescription = itemDescription,
                Specifications = specifications,
                EstimatedBudget = estimatedBudget,
                Currency = currency,
            });
        }

        return results;
    }

    public static string? GetString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    public static decimal? GetDecimal(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }
        }

        return null;
    }

    public static int GetInt(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }
        }

        return 0;
    }
}
