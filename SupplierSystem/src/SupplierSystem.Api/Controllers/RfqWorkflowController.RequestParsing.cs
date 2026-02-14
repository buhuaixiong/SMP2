using System.Globalization;
using System.Text.Json;
using SupplierSystem.Api.Helpers;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    private static decimal? ParsePercentValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().TrimEnd('%');
        if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (decimal.TryParse(trimmed, out parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? ReadIntValue(JsonElement element, string name)
    {
        if (!JsonHelper.TryGetProperty(element, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out intValue))
        {
            return intValue;
        }

        return null;
    }

    private static int? ReadIntValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : int.TryParse(value, out parsed)
                ? parsed
                : null;
    }

    private static decimal? ReadDecimalValue(JsonElement element, string name)
    {
        if (!JsonHelper.TryGetProperty(element, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return ReadDecimal(value.GetString());
        }

        return null;
    }

    private static string? ReadStringValue(JsonElement element, string name)
    {
        if (!JsonHelper.TryGetProperty(element, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static QuoteLineItemParseResult TryParseSubmitLineItems(JsonElement payload)
    {
        if (!JsonHelper.TryGetProperty(payload, "lineItems", out var lineItemsElement) ||
            lineItemsElement.ValueKind != JsonValueKind.Array ||
            lineItemsElement.GetArrayLength() == 0)
        {
            return QuoteLineItemParseResult.Fail("At least one line item quote is required.");
        }

        var lineItems = new List<QuoteLineItemInput>();
        var index = 1;
        foreach (var item in lineItemsElement.EnumerateArray())
        {
            var rfqLineItemId = ReadIntValue(item, "rfqLineItemId");
            if (!rfqLineItemId.HasValue)
            {
                return QuoteLineItemParseResult.Fail($"Line item {index}: rfqLineItemId is required.");
            }

            var unitPrice = ReadDecimalValue(item, "unitPrice");
            var notes = ReadStringValue(item, "notes");
            var hasPrice = unitPrice.HasValue;
            var hasNotes = !string.IsNullOrWhiteSpace(notes);

            if (!hasPrice && !hasNotes)
            {
                return QuoteLineItemParseResult.Fail($"Line item {index}: Either unitPrice or notes must be provided.");
            }

            if (unitPrice is { } unitPriceValue && unitPriceValue < 0m)
            {
                return QuoteLineItemParseResult.Fail($"Line item {index}: unitPrice must be a non-negative number.");
            }

            var minimumOrderQuantity = ReadDecimalValue(item, "minimumOrderQuantity") ??
                                      ReadDecimalValue(item, "moq");
            var productGroup = ReadStringValue(item, "productGroup");
            var productOrigin = ReadStringValue(item, "productOrigin");

            if (hasPrice)
            {
                var moq = minimumOrderQuantity ?? 0m;
                if (moq <= 0m)
                {
                    return QuoteLineItemParseResult.Fail($"Line item {index}: MOQ must be at least 1 when price is provided.");
                }

                if (string.IsNullOrWhiteSpace(productGroup))
                {
                    return QuoteLineItemParseResult.Fail($"Line item {index}: productGroup is required when price is provided.");
                }

                productGroup = productGroup.Trim().ToUpperInvariant();
                productOrigin = string.IsNullOrWhiteSpace(productOrigin) ? null : productOrigin.Trim();
                minimumOrderQuantity = moq;
            }
            else
            {
                minimumOrderQuantity = null;
                productGroup = null;
                productOrigin = null;
            }

            var rawTaxStatus = ReadStringValue(item, "taxStatus");
            if (!QuoteTaxStatusNormalizer.TryNormalize(rawTaxStatus, out var normalizedTaxStatus))
            {
                return QuoteLineItemParseResult.Fail($"Line item {index}: invalid taxStatus.");
            }

            lineItems.Add(new QuoteLineItemInput
            {
                Index = index,
                RfqLineItemId = rfqLineItemId.Value,
                UnitPrice = unitPrice,
                MinimumOrderQuantity = minimumOrderQuantity,
                StandardPackageQuantity = ReadDecimalValue(item, "standardPackageQuantity"),
                Brand = ReadStringValue(item, "brand"),
                DeliveryPeriod = ReadIntValue(item, "deliveryPeriod"),
                Parameters = ReadStringValue(item, "parameters"),
                Notes = notes,
                ProductOrigin = productOrigin,
                ProductGroup = productGroup,
                TaxStatus = normalizedTaxStatus,
            });

            index += 1;
        }

        return QuoteLineItemParseResult.SuccessResult(lineItems);
    }

    private static QuoteLineItemParseResult TryParseUpdateLineItems(JsonElement payload)
    {
        if (!JsonHelper.TryGetProperty(payload, "lineItems", out var lineItemsElement) ||
            lineItemsElement.ValueKind != JsonValueKind.Array ||
            lineItemsElement.GetArrayLength() == 0)
        {
            return QuoteLineItemParseResult.Fail("At least one line item quote is required.");
        }

        var lineItems = new List<QuoteLineItemInput>();
        var index = 1;
        foreach (var item in lineItemsElement.EnumerateArray())
        {
            var rfqLineItemId = ReadIntValue(item, "rfqLineItemId");
            if (!rfqLineItemId.HasValue)
            {
                return QuoteLineItemParseResult.Fail($"Line item {index}: rfqLineItemId is required.");
            }

            var minimumOrderQuantity = ReadDecimalValue(item, "minimumOrderQuantity") ??
                                      ReadDecimalValue(item, "moq");
            var moq = minimumOrderQuantity ?? 0m;
            if (moq <= 0m)
            {
                return QuoteLineItemParseResult.Fail($"Line item {index}: MOQ must be at least 1.");
            }

            var productGroup = ReadStringValue(item, "productGroup");
            if (string.IsNullOrWhiteSpace(productGroup))
            {
                return QuoteLineItemParseResult.Fail($"Line item {index}: productGroup is required.");
            }

            productGroup = productGroup.Trim().ToUpperInvariant();
            var productOrigin = ReadStringValue(item, "productOrigin");
            productOrigin = string.IsNullOrWhiteSpace(productOrigin) ? null : productOrigin.Trim();

            var rawTaxStatus = ReadStringValue(item, "taxStatus");
            if (!QuoteTaxStatusNormalizer.TryNormalize(rawTaxStatus, out var normalizedTaxStatus))
            {
                return QuoteLineItemParseResult.Fail($"Line item {index}: invalid taxStatus.");
            }

            lineItems.Add(new QuoteLineItemInput
            {
                Index = index,
                RfqLineItemId = rfqLineItemId.Value,
                UnitPrice = ReadDecimalValue(item, "unitPrice"),
                MinimumOrderQuantity = moq,
                StandardPackageQuantity = ReadDecimalValue(item, "standardPackageQuantity"),
                Brand = ReadStringValue(item, "brand"),
                DeliveryPeriod = ReadIntValue(item, "deliveryPeriod"),
                Parameters = ReadStringValue(item, "parameters"),
                Notes = ReadStringValue(item, "notes"),
                ProductOrigin = productOrigin,
                ProductGroup = productGroup,
                TaxStatus = normalizedTaxStatus,
            });

            index += 1;
        }

        return QuoteLineItemParseResult.SuccessResult(lineItems);
    }

    private sealed class QuoteLineItemInput
    {
        public int Index { get; init; }
        public int RfqLineItemId { get; init; }
        public decimal? UnitPrice { get; init; }
        public decimal? MinimumOrderQuantity { get; init; }
        public decimal? StandardPackageQuantity { get; init; }
        public string? Brand { get; init; }
        public int? DeliveryPeriod { get; init; }
        public string? Parameters { get; init; }
        public string? Notes { get; init; }
        public string? ProductOrigin { get; init; }
        public string? ProductGroup { get; init; }
        public string? TaxStatus { get; init; }
    }

    private sealed record QuoteLineItemParseResult(bool Success, List<QuoteLineItemInput> LineItems, string? ErrorMessage)
    {
        public static QuoteLineItemParseResult Fail(string message)
        {
            return new QuoteLineItemParseResult(false, new List<QuoteLineItemInput>(), message);
        }

        public static QuoteLineItemParseResult SuccessResult(List<QuoteLineItemInput> lineItems)
        {
            return new QuoteLineItemParseResult(true, lineItems, null);
        }
    }
}
