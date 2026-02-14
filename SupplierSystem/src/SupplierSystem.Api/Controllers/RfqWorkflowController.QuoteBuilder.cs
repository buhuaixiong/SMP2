using System.Globalization;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    private async Task<Dictionary<string, object?>> BuildQuoteResponseAsync(
        Quote rawQuote,
        string? rfqCurrency,
        CancellationToken cancellationToken)
    {
        var rawDict = NodeCaseMapper.ToSnakeCaseDictionary(rawQuote);
        return await BuildQuoteResponseAsync(rawDict, rfqCurrency, cancellationToken);
    }

    private async Task<Dictionary<string, object?>> BuildQuoteResponseAsync(
        Dictionary<string, object?> rawQuote,
        string? rfqCurrency,
        CancellationToken cancellationToken)
    {
        var quoteId = GetIntValue(rawQuote, "id") ?? 0;
        var lineItems = await GetQuoteLineItemsAsync(quoteId, cancellationToken);
        var attachments = await GetQuoteAttachmentsAsync(quoteId, cancellationToken);

        foreach (var item in lineItems)
        {
            if (item.TryGetValue("rfqLineItemId", out var lineItemId))
            {
                item["lineItemId"] = lineItemId;
            }

            if (item.TryGetValue("minimumOrderQuantity", out var moq))
            {
                item["moq"] = moq;
            }
        }

        var camelQuote = (Dictionary<string, object?>)CaseTransform.ToCamelCase(rawQuote)!;

        var shippingCountry = GetStringValue(camelQuote, "shippingCountry") ?? GetStringValue(rawQuote, "shipping_country");
        var shippingLocation = GetStringValue(camelQuote, "shippingLocation") ?? GetStringValue(rawQuote, "shipping_location");
        var validCodes = await GetValidTariffCountryCodesAsync(cancellationToken);
        var effectiveShippingCountry = DeriveShippingCountry(shippingCountry, shippingLocation, validCodes) ?? shippingCountry;

        if (!string.IsNullOrWhiteSpace(effectiveShippingCountry))
        {
            camelQuote["shippingCountry"] = effectiveShippingCountry;
        }

        var currency = GetStringValue(camelQuote, "currency") ?? rfqCurrency ?? DefaultCurrency;

        var hasPreStoredData = lineItems.Count > 0 &&
            lineItems[0].TryGetValue("standardCostLocal", out var standardCostLocal) &&
            standardCostLocal != null;

        List<Dictionary<string, object?>> itemsWithTariff;

        if (hasPreStoredData)
        {
            itemsWithTariff = lineItems.Select(item =>
            {
                var quantity = GetDecimalValue(item, "rfqQuantity") ?? GetDecimalValue(item, "quantity") ?? 1m;
                var standardUnitLocal = GetDecimalValue(item, "standardCostLocal") ?? 0m;
                var standardUnitUsd = GetDecimalValue(item, "standardCostUsd");
                var totalStandardLocal = ToFixedNumber(standardUnitLocal * quantity);
                var totalStandardUsd = standardUnitUsd.HasValue ? ToFixedNumber(standardUnitUsd.Value * quantity) : (decimal?)null;

                var totalOriginalPrice = GetDecimalValue(item, "totalPrice");
                if (!totalOriginalPrice.HasValue)
                {
                    var unitPrice = GetDecimalValue(item, "unitPrice") ?? 0m;
                    totalOriginalPrice = unitPrice * quantity;
                }

                var originalLineTotalUsd = GetDecimalValue(item, "originalPriceUsd");
                var totalOriginalUsd = originalLineTotalUsd.HasValue
                    ? ToFixedNumber(originalLineTotalUsd.Value * quantity)
                    : (decimal?)null;

                var tariffUnitLocal = GetDecimalValue(item, "tariffAmount") ?? 0m;
                var tariffUnitUsd = GetDecimalValue(item, "tariffAmountUsd");
                var tariffLineTotal = ToFixedNumber(tariffUnitLocal * quantity);
                var tariffLineTotalUsd = tariffUnitUsd.HasValue
                    ? ToFixedNumber(tariffUnitUsd.Value * quantity)
                    : (decimal?)null;

                var tariffCalculation = new Dictionary<string, object?>
                {
                    ["originalPrice"] = GetDecimalValue(item, "unitPrice") ?? 0m,
                    ["originalPriceUsd"] = GetDecimalValue(item, "originalPriceUsd"),
                    ["exchangeRate"] = GetDecimalValue(item, "exchangeRate"),
                    ["tariffRate"] = GetDecimalValue(item, "tariffRate"),
                    ["tariffRatePercent"] = GetStringValue(item, "tariffRatePercent"),
                    ["tariffAmount"] = GetDecimalValue(item, "tariffAmount"),
                    ["tariffAmountUsd"] = GetDecimalValue(item, "tariffAmountUsd"),
                    ["totalTariffAmount"] = tariffUnitLocal,
                    ["totalTariffAmountUsd"] = tariffUnitUsd,
                    ["specialTariffRate"] = GetDecimalValue(item, "specialTariffRate"),
                    ["specialTariffRatePercent"] = GetStringValue(item, "specialTariffRatePercent"),
                    ["specialTariffAmount"] = GetDecimalValue(item, "specialTariffAmount"),
                    ["specialTariffAmountUsd"] = GetDecimalValue(item, "specialTariffAmountUsd"),
                    ["hasSpecialTariff"] = ToBool(item.TryGetValue("hasSpecialTariff", out var hasSpecial) ? hasSpecial : null),
                    ["standardCostLocal"] = standardUnitLocal,
                    ["standardCostUsd"] = standardUnitUsd,
                    ["standardCostCurrency"] = GetStringValue(item, "standardCostCurrency") ?? "USD",
                    ["calculatedAt"] = GetStringValue(item, "calculatedAt"),
                };

                item["lineQuantity"] = quantity;
                item["standardLineCostLocal"] = totalStandardLocal;
                item["standardLineCostUsd"] = totalStandardUsd;
                item["originalLineTotal"] = ToFixedNumber(totalOriginalPrice.Value);
                item["originalLineTotalUsd"] = totalOriginalUsd;
                item["tariffLineTotal"] = tariffLineTotal;
                item["tariffLineTotalUsd"] = tariffLineTotalUsd;
                item["tariffCalculation"] = tariffCalculation;

                return item;
            }).ToList();
        }
        else
        {
            var computed = await _tariffService.CalculateQuoteItemsCostsAsync(
                lineItems,
                effectiveShippingCountry,
                currency,
                cancellationToken: cancellationToken);

            itemsWithTariff = computed.Select(item =>
            {
                var quantity = GetDecimalValue(item, "rfqQuantity") ?? GetDecimalValue(item, "quantity") ?? 1m;
                var calculation = item.TryGetValue("tariffCalculation", out var calcObj) ? calcObj as TariffCalculationResult : null;
                var standardUnitLocal = calculation?.StandardCostLocal ?? 0m;
                var standardUnitUsd = calculation?.StandardCostUsd;
                var totalStandardLocal = ToFixedNumber(standardUnitLocal * quantity);
                var totalStandardUsd = standardUnitUsd.HasValue ? ToFixedNumber(standardUnitUsd.Value * quantity) : (decimal?)null;

                var totalOriginalPrice = GetDecimalValue(item, "totalPrice");
                if (!totalOriginalPrice.HasValue)
                {
                    var unitPrice = GetDecimalValue(item, "unitPrice") ?? calculation?.OriginalPrice ?? 0m;
                    totalOriginalPrice = unitPrice * quantity;
                }

                var originalPriceUsd = calculation?.OriginalPriceUsd;
                var totalOriginalUsd = originalPriceUsd.HasValue
                    ? ToFixedNumber(originalPriceUsd.Value * quantity)
                    : (decimal?)null;

                var totalTariffLocal = ToFixedNumber((calculation?.TotalTariffAmount ?? 0m) * quantity);
                var totalTariffUsd = calculation?.TotalTariffAmountUsd.HasValue == true
                    ? ToFixedNumber(calculation.TotalTariffAmountUsd.Value * quantity)
                    : (decimal?)null;

                item["lineQuantity"] = quantity;
                item["standardLineCostLocal"] = totalStandardLocal;
                item["standardLineCostUsd"] = totalStandardUsd;
                item["originalLineTotal"] = ToFixedNumber(totalOriginalPrice.Value);
                item["originalLineTotalUsd"] = totalOriginalUsd;
                item["tariffLineTotal"] = totalTariffLocal;
                item["tariffLineTotalUsd"] = totalTariffUsd;

                return item;
            }).ToList();
        }

        var hasQuoteLevelTotals = GetDecimalValue(rawQuote, "total_standard_cost_local").HasValue;

        var summary = new Dictionary<string, object?>
        {
            ["currency"] = currency,
            ["totalOriginalAmount"] = 0m,
            ["totalOriginalAmountUsd"] = null,
            ["totalStandardCostLocal"] = 0m,
            ["totalStandardCostUsd"] = null,
            ["totalTariffAmountLocal"] = 0m,
            ["totalTariffAmountUsd"] = null,
            ["hasSpecialTariff"] = false,
            ["standardCostCurrency"] = "USD",
        };

        if (hasPreStoredData && hasQuoteLevelTotals)
        {
            summary["totalOriginalAmount"] = ToFixedNumber(GetDecimalValue(rawQuote, "total_price") ?? 0m);
            summary["totalStandardCostLocal"] = ToFixedNumber(GetDecimalValue(rawQuote, "total_standard_cost_local") ?? 0m);
            summary["totalStandardCostUsd"] = GetDecimalValue(rawQuote, "total_standard_cost_usd") is { } totalUsd
                ? ToFixedNumber(totalUsd)
                : null;
            summary["totalTariffAmountLocal"] = ToFixedNumber(GetDecimalValue(rawQuote, "total_tariff_amount_local") ?? 0m);
            summary["totalTariffAmountUsd"] = GetDecimalValue(rawQuote, "total_tariff_amount_usd") is { } tariffUsd
                ? ToFixedNumber(tariffUsd)
                : null;
            summary["hasSpecialTariff"] = ToBool(rawQuote.TryGetValue("has_special_tariff", out var hasSpecial) ? hasSpecial : null);
            summary["standardCostCurrency"] = summary["totalStandardCostUsd"] != null ? "USD" : currency;
        }
        else
        {
            decimal totalOriginalAmount = 0m;
            decimal totalStandardLocal = 0m;
            decimal totalTariffLocal = 0m;

            decimal usdOriginalAccumulator = 0m;
            decimal usdStandardAccumulator = 0m;
            decimal usdTariffAccumulator = 0m;
            var usdOriginalCount = 0;
            var usdStandardCount = 0;
            var usdTariffCount = 0;
            var hasSpecialTariff = false;

            foreach (var item in itemsWithTariff)
            {
                totalOriginalAmount += GetDecimalValue(item, "originalLineTotal") ?? 0m;
                totalStandardLocal += GetDecimalValue(item, "standardLineCostLocal") ?? 0m;
                totalTariffLocal += GetDecimalValue(item, "tariffLineTotal") ?? 0m;

                var originalUsd = GetDecimalValue(item, "originalLineTotalUsd");
                if (originalUsd.HasValue)
                {
                    usdOriginalAccumulator += originalUsd.Value;
                    usdOriginalCount += 1;
                }

                var standardUsd = GetDecimalValue(item, "standardLineCostUsd");
                if (standardUsd.HasValue)
                {
                    usdStandardAccumulator += standardUsd.Value;
                    usdStandardCount += 1;
                }

                var tariffUsd = GetDecimalValue(item, "tariffLineTotalUsd");
                if (tariffUsd.HasValue)
                {
                    usdTariffAccumulator += tariffUsd.Value;
                    usdTariffCount += 1;
                }

                if (item.TryGetValue("tariffCalculation", out var calcObj) &&
                    calcObj is TariffCalculationResult calc &&
                    calc.HasSpecialTariff)
                {
                    hasSpecialTariff = true;
                }
            }

            summary["totalOriginalAmount"] = ToFixedNumber(totalOriginalAmount);
            summary["totalStandardCostLocal"] = ToFixedNumber(totalStandardLocal);
            summary["totalTariffAmountLocal"] = ToFixedNumber(totalTariffLocal);
            summary["totalOriginalAmountUsd"] = usdOriginalCount > 0 ? ToFixedNumber(usdOriginalAccumulator) : null;
            summary["totalStandardCostUsd"] = usdStandardCount > 0 ? ToFixedNumber(usdStandardAccumulator) : null;
            summary["totalTariffAmountUsd"] = usdTariffCount > 0 ? ToFixedNumber(usdTariffAccumulator) : null;
            summary["hasSpecialTariff"] = hasSpecialTariff;
            summary["standardCostCurrency"] = summary["totalStandardCostUsd"] != null ? "USD" : currency;
        }

        camelQuote["quoteItems"] = itemsWithTariff;
        camelQuote["items"] = itemsWithTariff;
        camelQuote["tariffSummary"] = summary;
        camelQuote["standardCostLocalTotal"] = summary["totalStandardCostLocal"];
        camelQuote["standardCostUsdTotal"] = summary["totalStandardCostUsd"];
        camelQuote["attachments"] = attachments;

        return camelQuote;
    }

    private static bool ToBool(object? value)
    {
        if (value == null)
        {
            return false;
        }

        if (value is bool boolean)
        {
            return boolean;
        }

        if (value is int intValue)
        {
            return intValue != 0;
        }

        if (value is long longValue)
        {
            return longValue != 0;
        }

        if (bool.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }
}
