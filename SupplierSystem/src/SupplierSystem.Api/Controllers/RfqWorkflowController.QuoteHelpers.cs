using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    private async Task<HashSet<string>> GetValidTariffCountryCodesAsync(CancellationToken cancellationToken)
    {
        var rows = await _tariffService.GetAvailableCountriesAsync(cancellationToken);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.TryGetValue("country_code", out var codeObj) ||
                row.TryGetValue("countryCode", out codeObj) ||
                row.TryGetValue("code", out codeObj))
            {
                var code = codeObj?.ToString()?.Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    set.Add(code);
                }
            }
        }

        set.Add("HK");
        return set;
    }

    private static string? DeriveShippingCountry(
        string? shippingCountry,
        string? shippingLocation,
        HashSet<string> validCodes)
    {
        var normalized = NormalizeCountryCode(shippingCountry, validCodes);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return MapLocationToCountry(shippingLocation, validCodes);
    }

    private static string? NormalizeCountryCode(string? code, HashSet<string> validCodes)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var upper = trimmed.ToUpperInvariant();

        if (CountryAliasMap.TryGetValue(upper, out var mapped))
        {
            return mapped;
        }

        if (CountryCodeRegex.IsMatch(upper) && validCodes.Contains(upper))
        {
            return upper;
        }

        return null;
    }

    private static string? MapLocationToCountry(string? location, HashSet<string> validCodes)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var trimmed = location.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var upper = trimmed.ToUpperInvariant();
        var direct = NormalizeCountryCode(upper, validCodes);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        var sanitized = Regex.Replace(upper, "[^A-Z]", string.Empty);
        if (CountryAliasMap.TryGetValue(sanitized, out var alias))
        {
            return alias;
        }

        if (sanitized.Contains("HONGKONG", StringComparison.Ordinal))
        {
            return "HK";
        }

        var tokens = Regex.Split(upper, "[^A-Z]").Where(token => !string.IsNullOrWhiteSpace(token));
        foreach (var token in tokens)
        {
            var normalized = NormalizeCountryCode(token, validCodes);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static decimal ToFixedNumber(decimal value, int digits = 2)
    {
        return Math.Round(value, digits, MidpointRounding.AwayFromZero);
    }

    private static decimal? GetDecimalValue(Dictionary<string, object?> item, string key)
    {
        if (!item.TryGetValue(key, out var raw) || raw == null)
        {
            return null;
        }

        if (raw is decimal dec)
        {
            return dec;
        }

        if (raw is double dbl)
        {
            return Convert.ToDecimal(dbl);
        }

        if (raw is float flt)
        {
            return Convert.ToDecimal(flt);
        }

        if (raw is int intValue)
        {
            return intValue;
        }

        if (raw is long longValue)
        {
            return longValue;
        }

        if (decimal.TryParse(raw.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? GetIntValue(Dictionary<string, object?> item, string key)
    {
        if (!item.TryGetValue(key, out var raw) || raw == null)
        {
            return null;
        }

        if (raw is int intValue)
        {
            return intValue;
        }

        if (raw is long longValue)
        {
            return (int)longValue;
        }

        if (int.TryParse(raw.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? GetStringValue(Dictionary<string, object?> item, string key)
    {
        return item.TryGetValue(key, out var raw) ? raw?.ToString() : null;
    }

    private async Task<Dictionary<string, object?>?> GetQuoteWithLineItemsAsync(
        int quoteId,
        CancellationToken cancellationToken)
    {
        var quote = await _dbContext.Quotes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);
        if (quote == null)
        {
            return null;
        }

        return await BuildQuoteResponseAsync(quote, quote.Currency ?? DefaultCurrency, cancellationToken);
    }

    private async Task<List<Dictionary<string, object?>>> GetQuoteLineItemsAsync(
        int quoteId,
        CancellationToken cancellationToken)
    {
        var rows = await (from qli in _dbContext.QuoteLineItems.AsNoTracking()
                          join rli in _dbContext.RfqLineItems.AsNoTracking()
                              on qli.RfqLineItemId equals rli.Id into rfqGroup
                          from rli in rfqGroup.DefaultIfEmpty()
                          where qli.QuoteId == quoteId
                          orderby qli.Id
                          select new { QuoteLineItem = qli, RfqQuantity = rli != null ? rli.Quantity : (decimal?)null })
            .ToListAsync(cancellationToken);

        var items = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var item = NodeCaseMapper.ToSnakeCaseDictionary(row.QuoteLineItem);
            item["rfq_quantity"] = row.RfqQuantity;
            items.Add((Dictionary<string, object?>)CaseTransform.ToCamelCase(item)!);
        }

        return items;
    }

    private async Task<List<Dictionary<string, object?>>> GetQuoteAttachmentsAsync(
        int quoteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var rfqId = await _dbContext.Quotes.AsNoTracking()
                .Where(q => q.Id == quoteId)
                .Select(q => (long?)q.RfqId)
                .FirstOrDefaultAsync(cancellationToken);
            var rows = await _dbContext.QuoteAttachments.AsNoTracking()
                .Where(att => att.QuoteId == quoteId)
                .OrderByDescending(att => att.UploadedAt)
                .ThenByDescending(att => att.Id)
                .ToListAsync(cancellationToken);

            var attachments = NodeCaseMapper.ToCamelCaseList(rows);
            foreach (var attachment in attachments)
            {
                var storedName =
                    GetStringValue(attachment, "storedName") ??
                    GetStringValue(attachment, "stored_name") ??
                    GetStringValue(attachment, "storedFileName") ??
                    GetStringValue(attachment, "stored_file_name");
                if (!string.IsNullOrWhiteSpace(storedName))
                {
                    var prefix = rfqId.HasValue
                        ? $"/uploads/rfq-attachments/{rfqId.Value.ToString(CultureInfo.InvariantCulture)}/"
                        : "/uploads/rfq-attachments/";
                    var downloadUrl = $"{prefix}{storedName}";
                    attachment["filePath"] = downloadUrl;
                    attachment["downloadUrl"] = downloadUrl;
                }
            }

            return attachments;
        }
        catch
        {
            return new List<Dictionary<string, object?>>();
        }
    }
}
