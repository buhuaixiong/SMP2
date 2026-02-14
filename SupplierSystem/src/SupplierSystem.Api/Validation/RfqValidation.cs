using System.Globalization;
using System.Text.Json;
using SupplierSystem.Api.Helpers;

namespace SupplierSystem.Api.Validation;

public static class RfqValidation
{
    private static readonly HashSet<string> RfqTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "simple",
        "multi_line",
        "by_category",
        "short_term",
        "long_term",
    };

    public static List<ValidationDetail> ValidateCreateRfq(JsonElement body)
    {
        var details = new List<ValidationDetail>();

        ValidateRequiredString(body, "title", details, minLength: 2, maxLength: 200);
        ValidateRequiredString(body, "description", details, minLength: 10, maxLength: 2000);
        ValidateRequiredString(body, "rfqType", details, validValues: RfqTypes);
        ValidateRequiredString(body, "deliveryPeriod", details);
        ValidateOptionalPositiveNumber(body, "budgetAmount", details);
        ValidateOptionalString(body, "currency", details);
        ValidateOptionalIsoDate(body, "validUntil", details);

        if (JsonHelper.TryGetProperty(body, "items", out var itemsElement))
        {
            if (itemsElement.ValueKind != JsonValueKind.Array)
            {
                details.Add(CreateDetail("items", "\"items\" must be an array", "array.base"));
            }
            else
            {
                var index = 0;
                foreach (var item in itemsElement.EnumerateArray())
                {
                    var prefix = $"items[{index}]";
                    ValidateOptionalInteger(item, $"{prefix}.lineNumber", details, min: 1);
                    ValidateRequiredString(item, $"{prefix}.materialType", details);
                    ValidateRequiredString(item, $"{prefix}.description", details);
                    ValidateRequiredPositiveNumber(item, $"{prefix}.quantity", details);
                    ValidateRequiredString(item, $"{prefix}.unit", details);
                    ValidateOptionalPositiveNumber(item, $"{prefix}.targetPrice", details);
                    ValidateOptionalString(item, $"{prefix}.remarks", details);
                    index++;
                }
            }
        }

        return details;
    }

    public static List<ValidationDetail> ValidateUpdateRfq(JsonElement body)
    {
        var details = new List<ValidationDetail>();

        ValidateOptionalString(body, "title", details, minLength: 2, maxLength: 200);
        ValidateOptionalString(body, "description", details, minLength: 10, maxLength: 2000);
        ValidateOptionalString(body, "deliveryPeriod", details);
        ValidateOptionalPositiveNumber(body, "budgetAmount", details);
        ValidateOptionalIsoDate(body, "validUntil", details);

        return details;
    }

    public static List<ValidationDetail> ValidateSubmitQuote(JsonElement body)
    {
        var details = new List<ValidationDetail>();

        ValidateRequiredPositiveNumber(body, "totalPrice", details);
        ValidateRequiredString(body, "currency", details);
        ValidateRequiredString(body, "deliveryPeriod", details);
        ValidateOptionalString(body, "remarks", details, maxLength: 1000);

        if (JsonHelper.TryGetProperty(body, "items", out var itemsElement))
        {
            if (itemsElement.ValueKind != JsonValueKind.Array)
            {
                details.Add(CreateDetail("items", "\"items\" must be an array", "array.base"));
            }
            else
            {
                var index = 0;
                foreach (var item in itemsElement.EnumerateArray())
                {
                    var prefix = $"items[{index}]";
                    ValidateOptionalInteger(item, $"{prefix}.lineNumber", details, min: 1);
                    ValidateRequiredString(item, $"{prefix}.description", details);
                    ValidateRequiredPositiveNumber(item, $"{prefix}.quantity", details);
                    ValidateRequiredPositiveNumber(item, $"{prefix}.unitPrice", details);
                    ValidateOptionalPositiveNumber(item, $"{prefix}.totalPrice", details);
                    ValidateOptionalString(item, $"{prefix}.remarks", details);
                    index++;
                }
            }
        }

        return details;
    }

    public static List<ValidationDetail> ValidateSendInvitations(JsonElement body)
    {
        var details = new List<ValidationDetail>();

        if (!JsonHelper.TryGetProperty(body, "supplierIds", out var supplierIdsElement))
        {
            details.Add(CreateDetail("supplierIds", "\"supplierIds\" is required", "any.required"));
            return details;
        }

        if (supplierIdsElement.ValueKind != JsonValueKind.Array)
        {
            details.Add(CreateDetail("supplierIds", "\"supplierIds\" must be an array", "array.base"));
            return details;
        }

        if (supplierIdsElement.GetArrayLength() < 1)
        {
            details.Add(CreateDetail("supplierIds", "\"supplierIds\" must contain at least 1 items", "array.min"));
        }

        return details;
    }

    public static List<ValidationDetail> ValidateReview(JsonElement body)
    {
        var details = new List<ValidationDetail>();

        ValidateRequiredPositiveInteger(body, "selectedQuoteId", details);

        if (!JsonHelper.TryGetProperty(body, "reviewScores", out var reviewScoresElement) ||
            (reviewScoresElement.ValueKind != JsonValueKind.Object && reviewScoresElement.ValueKind != JsonValueKind.Array))
        {
            details.Add(CreateDetail("reviewScores", "\"reviewScores\" is required", "any.required"));
        }

        ValidateOptionalString(body, "comments", details, maxLength: 2000);

        return details;
    }

    private static void ValidateRequiredString(JsonElement body, string field, List<ValidationDetail> details, int? minLength = null, int? maxLength = null, IEnumerable<string>? validValues = null)
    {
        if (!JsonHelper.TryGetProperty(body, field.Split('.').Last(), out var element) || element.ValueKind == JsonValueKind.Null)
        {
            details.Add(CreateDetail(field, $"\"{field}\" is required", "any.required"));
            return;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a string", "string.base"));
            return;
        }

        var value = element.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            details.Add(CreateDetail(field, $"\"{field}\" is required", "any.required"));
            return;
        }

        if (minLength.HasValue && value.Length < minLength.Value)
        {
            details.Add(CreateDetail(field, $"\"{field}\" length must be at least {minLength} characters long", "string.min"));
        }

        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            details.Add(CreateDetail(field, $"\"{field}\" length must be less than or equal to {maxLength} characters long", "string.max"));
        }

        if (validValues != null && !validValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            var allowed = string.Join(", ", validValues);
            details.Add(CreateDetail(field, $"\"{field}\" must be one of [{allowed}]", "any.only"));
        }
    }

    private static void ValidateOptionalString(JsonElement body, string field, List<ValidationDetail> details, int? minLength = null, int? maxLength = null)
    {
        if (!JsonHelper.TryGetProperty(body, field.Split('.').Last(), out var element))
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a string", "string.base"));
            return;
        }

        var value = element.GetString() ?? string.Empty;

        if (minLength.HasValue && value.Length < minLength.Value)
        {
            details.Add(CreateDetail(field, $"\"{field}\" length must be at least {minLength} characters long", "string.min"));
        }

        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            details.Add(CreateDetail(field, $"\"{field}\" length must be less than or equal to {maxLength} characters long", "string.max"));
        }
    }

    private static void ValidateOptionalIsoDate(JsonElement body, string field, List<ValidationDetail> details)
    {
        if (!JsonHelper.TryGetProperty(body, field, out var element))
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be in ISO 8601 date format", "date.format"));
            return;
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be in ISO 8601 date format", "date.format"));
        }
    }

    private static void ValidateRequiredPositiveNumber(JsonElement body, string field, List<ValidationDetail> details)
    {
        if (!JsonHelper.TryGetProperty(body, field.Split('.').Last(), out var element))
        {
            details.Add(CreateDetail(field, $"\"{field}\" is required", "any.required"));
            return;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDecimal(out var value))
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a number", "number.base"));
            return;
        }

        if (value <= 0)
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a positive number", "number.positive"));
        }
    }

    private static void ValidateOptionalPositiveNumber(JsonElement body, string field, List<ValidationDetail> details)
    {
        if (!JsonHelper.TryGetProperty(body, field.Split('.').Last(), out var element))
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDecimal(out var value))
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a number", "number.base"));
            return;
        }

        if (value <= 0)
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a positive number", "number.positive"));
        }
    }

    private static void ValidateRequiredPositiveInteger(JsonElement body, string field, List<ValidationDetail> details)
    {
        if (!JsonHelper.TryGetProperty(body, field.Split('.').Last(), out var element))
        {
            details.Add(CreateDetail(field, $"\"{field}\" is required", "any.required"));
            return;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be an integer", "number.integer"));
            return;
        }

        if (value <= 0)
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a positive number", "number.positive"));
        }
    }

    private static void ValidateOptionalPositiveInteger(JsonElement body, string field, List<ValidationDetail> details)
    {
        if (!JsonHelper.TryGetProperty(body, field.Split('.').Last(), out var element))
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be an integer", "number.integer"));
            return;
        }

        if (value <= 0)
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a positive number", "number.positive"));
        }
    }

    private static void ValidateOptionalInteger(JsonElement body, string field, List<ValidationDetail> details, int? min = null)
    {
        if (!JsonHelper.TryGetProperty(body, field.Split('.').Last(), out var element))
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be an integer", "number.integer"));
            return;
        }

        if (min.HasValue && value < min.Value)
        {
            details.Add(CreateDetail(field, $"\"{field}\" must be a positive number", "number.positive"));
        }
    }

    private static ValidationDetail CreateDetail(string field, string message, string type)
    {
        return new ValidationDetail
        {
            Field = field,
            Message = message,
            Type = type,
        };
    }
}
