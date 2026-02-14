using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SupplierSystem.Application.DTOs.Registrations;

public enum SupplierRegistrationValidationMode
{
    Draft,
    Final
}

public sealed class SupplierRegistrationDocumentUpload
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long? Size { get; set; }
    public string Content { get; set; } = string.Empty;
}

public sealed class SupplierRegistrationNormalizedPayload
{
    public string? CompanyName { get; set; }
    public string? EnglishName { get; set; }
    public string? ChineseName { get; set; }
    public string? CompanyType { get; set; }
    public string? CompanyTypeOther { get; set; }
    public string? AuthorizedCapital { get; set; }
    public string? IssuedCapital { get; set; }
    public string? Directors { get; set; }
    public string? Owners { get; set; }
    public string? RegisteredOffice { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    public string? BusinessAddress { get; set; }
    public string? BusinessPhone { get; set; }
    public string? BusinessFax { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ProcurementEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? FinanceContactName { get; set; }
    public string? FinanceContactEmail { get; set; }
    public string? FinanceContactPhone { get; set; }
    public string? BusinessNature { get; set; }
    public string? OperatingCurrency { get; set; }
    public string? DeliveryLocation { get; set; }
    public string? ShipCode { get; set; }
    public string? ProductOrigin { get; set; }
    public string? ProductTypes { get; set; }
    public string? ProductTypesOther { get; set; }
    public string? InvoiceType { get; set; }
    public string? PaymentTermsDays { get; set; }
    public List<string> PaymentMethods { get; set; } = new();
    public string? PaymentMethodsOther { get; set; }
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? SwiftCode { get; set; }
    public string? Notes { get; set; }
    public string? SupplierClassification { get; set; }
    public string? SupplierStage { get; set; }
    public SupplierRegistrationDocumentUpload? BusinessLicenseFile { get; set; }
    public SupplierRegistrationDocumentUpload? BankAccountFile { get; set; }
}

public sealed class SupplierRegistrationValidationResult
{
    public bool Valid { get; init; }
    public Dictionary<string, string> Errors { get; init; } = new();
    public SupplierRegistrationNormalizedPayload Normalized { get; init; } = new();
}

public static class SupplierRegistrationValidation
{
    private static readonly Regex EmailRegex = new("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$", RegexOptions.IgnoreCase);
    private static readonly Regex PhoneRegex = new("^[+0-9()\\-\\s]{6,20}$");

    public static readonly IReadOnlyList<string> Classifications = new[] { "DM", "IDM" };
    public static readonly IReadOnlyList<string> Currencies = new[] { "RMB", "USD", "EUR", "GBP", "KRW", "THB", "JPY" };
    public static readonly IReadOnlyList<string> ShipCodes = new[]
    {
        "EXW", "FOB", "CIF", "CFR", "DDP", "DDU", "DAP", "DAT", "FCA", "CPT", "CIP"
    };
    public static readonly IReadOnlySet<string> PaymentTermCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CA",
            "PO",
            "00",
            "04",
            "05",
            "07",
            "1A",
            "1B",
            "1C",
            "1D",
            "1E",
            "10",
            "12",
            "14",
            "15",
            "16",
            "18",
            "21",
            "3A",
            "3B",
            "30",
            "32",
            "33",
            "35",
            "4A",
            "40",
            "45",
            "5A",
            "52",
            "55",
            "6A",
            "60",
            "65",
            "68",
            "7A",
            "70",
            "75",
            "76",
            "9A",
            "90",
        };

    public static readonly IReadOnlyList<string> RequiredFields = new[]
    {
        "companyName",
        "registeredOffice",
        "businessRegistrationNumber",
        "businessAddress",
        "contactName",
        "contactEmail",
        "procurementEmail",
        "contactPhone",
        "operatingCurrency",
        "deliveryLocation",
        "shipCode",
        "productOrigin",
        "bankName",
        "bankAddress",
        "bankAccountNumber",
        "companyType",
        "supplierClassification",
        "financeContactName",
        "financeContactPhone",
    };

    private static readonly IReadOnlyDictionary<string, string> CurrencyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RMB"] = "RMB",
            ["CNY"] = "RMB",
            ["USD"] = "USD",
            ["EUR"] = "EUR",
            ["GBP"] = "GBP",
            ["KRW"] = "KRW",
            ["THB"] = "THB",
            ["JPY"] = "JPY",
        };

    public static SupplierRegistrationValidationResult ValidateRegistration(
        JsonElement payload,
        SupplierRegistrationValidationMode mode)
    {
        var normalized = BuildNormalizedPayload(payload);
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (mode == SupplierRegistrationValidationMode.Final)
        {
            foreach (var field in RequiredFields)
            {
                if (!HasValue(normalized, field))
                {
                    errors[field] = "REQUIRED";
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(normalized.ContactEmail) && !EmailRegex.IsMatch(normalized.ContactEmail))
        {
            errors["contactEmail"] = "INVALID_EMAIL";
        }

        if (!string.IsNullOrWhiteSpace(normalized.ProcurementEmail) && !EmailRegex.IsMatch(normalized.ProcurementEmail))
        {
            errors["procurementEmail"] = "INVALID_EMAIL";
        }

        if (!string.IsNullOrWhiteSpace(normalized.FinanceContactEmail) && !EmailRegex.IsMatch(normalized.FinanceContactEmail))
        {
            errors["financeContactEmail"] = "INVALID_EMAIL";
        }

        if (!string.IsNullOrWhiteSpace(normalized.ContactPhone) && !PhoneRegex.IsMatch(normalized.ContactPhone))
        {
            errors["contactPhone"] = "INVALID_PHONE";
        }

        if (!string.IsNullOrWhiteSpace(normalized.FinanceContactPhone) && !PhoneRegex.IsMatch(normalized.FinanceContactPhone))
        {
            errors["financeContactPhone"] = "INVALID_PHONE";
        }

        if (!string.IsNullOrWhiteSpace(normalized.OperatingCurrency) && !Currencies.Contains(normalized.OperatingCurrency))
        {
            errors["operatingCurrency"] = "UNSUPPORTED_CURRENCY";
        }
        else if (mode == SupplierRegistrationValidationMode.Final && string.IsNullOrWhiteSpace(normalized.OperatingCurrency))
        {
            errors["operatingCurrency"] = "REQUIRED";
        }

        if (!string.IsNullOrWhiteSpace(normalized.SupplierClassification) && !Classifications.Contains(normalized.SupplierClassification))
        {
            errors["supplierClassification"] = "UNSUPPORTED_CLASSIFICATION";
        }
        else if (mode == SupplierRegistrationValidationMode.Final && string.IsNullOrWhiteSpace(normalized.SupplierClassification))
        {
            errors["supplierClassification"] = "REQUIRED";
        }

        if (!string.IsNullOrWhiteSpace(normalized.ShipCode) && !ShipCodes.Contains(normalized.ShipCode))
        {
            errors["shipCode"] = "UNSUPPORTED_SHIP_CODE";
        }
        else if (mode == SupplierRegistrationValidationMode.Final && string.IsNullOrWhiteSpace(normalized.ShipCode))
        {
            errors["shipCode"] = "REQUIRED";
        }

        if (!string.IsNullOrWhiteSpace(normalized.PaymentTermsDays))
        {
            if (!IsPaymentTermsValid(normalized.PaymentTermsDays))
            {
                errors["paymentTermsDays"] = "INVALID_PAYMENT_TERMS";
            }
        }

        if (mode == SupplierRegistrationValidationMode.Final)
        {
            if (string.IsNullOrWhiteSpace(normalized.BankAccountNumber))
            {
                errors["bankAccountNumber"] = "REQUIRED";
            }

            if (string.IsNullOrWhiteSpace(normalized.BankName))
            {
                errors["bankName"] = "REQUIRED";
            }

            if (string.IsNullOrWhiteSpace(normalized.BankAddress))
            {
                errors["bankAddress"] = "REQUIRED";
            }

            if (normalized.BusinessLicenseFile == null)
            {
                errors["businessLicenseFile"] = "REQUIRED";
            }

            if (normalized.BankAccountFile == null)
            {
                errors["bankAccountFile"] = "REQUIRED";
            }
        }

        return new SupplierRegistrationValidationResult
        {
            Valid = errors.Count == 0,
            Errors = errors,
            Normalized = normalized,
        };
    }

    public static SupplierRegistrationNormalizedPayload BuildNormalizedPayload(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return new SupplierRegistrationNormalizedPayload();
        }

        var normalizedCurrency = NormalizeCurrency(GetString(payload, "operatingCurrency"));

        return new SupplierRegistrationNormalizedPayload
        {
            CompanyName = NormalizeString(GetString(payload, "companyName")),
            EnglishName = NormalizeString(GetString(payload, "englishName")),
            ChineseName = NormalizeString(GetString(payload, "chineseName")),
            CompanyType = NormalizeString(GetString(payload, "companyType")),
            CompanyTypeOther = NormalizeString(GetString(payload, "companyTypeOther")),
            AuthorizedCapital = NormalizeString(GetString(payload, "authorizedCapital")),
            IssuedCapital = NormalizeString(GetString(payload, "issuedCapital")),
            Directors = NormalizeString(GetString(payload, "directors")),
            Owners = NormalizeString(GetString(payload, "owners")),
            RegisteredOffice = NormalizeString(GetString(payload, "registeredOffice")),
            BusinessRegistrationNumber = NormalizeString(GetString(payload, "businessRegistrationNumber")),
            BusinessAddress = NormalizeString(GetString(payload, "businessAddress")),
            BusinessPhone = NormalizeString(GetString(payload, "businessPhone")),
            BusinessFax = NormalizeString(GetString(payload, "businessFax")),
            ContactName = NormalizeString(GetString(payload, "contactName")),
            ContactEmail = NormalizeEmail(GetString(payload, "contactEmail")),
            ProcurementEmail = NormalizeEmail(GetString(payload, "procurementEmail")),
            ContactPhone = NormalizeString(GetString(payload, "contactPhone")),
            FinanceContactName = NormalizeString(GetString(payload, "financeContactName")),
            FinanceContactEmail = NormalizeEmail(GetString(payload, "financeContactEmail")),
            FinanceContactPhone = NormalizeString(GetString(payload, "financeContactPhone")),
            BusinessNature = NormalizeString(GetString(payload, "businessNature")),
            OperatingCurrency = normalizedCurrency,
            DeliveryLocation = NormalizeString(GetString(payload, "deliveryLocation")),
            ShipCode = NormalizeShipCode(GetString(payload, "shipCode")),
            ProductOrigin = NormalizeString(GetString(payload, "productOrigin")),
            ProductTypes = NormalizeProductTypes(payload),
            ProductTypesOther = NormalizeString(GetString(payload, "productTypesOther")),
            InvoiceType = NormalizeString(GetString(payload, "invoiceType")),
            PaymentTermsDays = NormalizePaymentTerms(GetString(payload, "paymentTermsDays")),
            PaymentMethods = NormalizePaymentMethods(payload),
            PaymentMethodsOther = NormalizeString(GetString(payload, "paymentMethodsOther")),
            BankName = NormalizeString(GetString(payload, "bankName")),
            BankAddress = NormalizeString(GetString(payload, "bankAddress")),
            BankAccountNumber = NormalizeString(GetString(payload, "bankAccountNumber")),
            SwiftCode = NormalizeString(GetString(payload, "swiftCode")),
            Notes = NormalizeString(GetString(payload, "notes")),
            SupplierClassification = NormalizeClassification(GetString(payload, "supplierClassification")),
            SupplierStage = NormalizeString(GetString(payload, "supplierStage")),
            BusinessLicenseFile = NormalizeDocumentUpload(payload, "businessLicenseFile"),
            BankAccountFile = NormalizeDocumentUpload(payload, "bankAccountFile"),
        };
    }

    public static string? NormalizeCurrency(string? value)
    {
        var currency = NormalizeString(value);
        if (currency == null)
        {
            return null;
        }

        return CurrencyAliases.TryGetValue(currency, out var normalized) ? normalized : null;
    }

    private static string? NormalizeClassification(string? value)
    {
        var normalized = NormalizeString(value);
        return normalized == null ? null : normalized.ToUpperInvariant();
    }

    private static string? NormalizeShipCode(string? value)
    {
        var normalized = NormalizeString(value);
        return normalized == null ? null : normalized.ToUpperInvariant();
    }

    private static string? NormalizePaymentTerms(string? value)
    {
        return NormalizeString(value);
    }

    private static bool IsPaymentTermsValid(string value)
    {
        if (PaymentTermCodes.Contains(value))
        {
            return true;
        }

        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var terms) &&
               terms >= 0 &&
               terms <= 365;
    }

    private static string? NormalizeProductTypes(JsonElement payload)
    {
        if (payload.TryGetProperty("productTypes", out var value))
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                var parts = value.EnumerateArray()
                    .Select(item => NormalizeString(item.ToString()))
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!);
                var joined = string.Join(", ", parts);
                return NormalizeString(joined);
            }

            return NormalizeString(value.ToString());
        }

        return null;
    }

    private static List<string> NormalizePaymentMethods(JsonElement payload)
    {
        if (!payload.TryGetProperty("paymentMethods", out var value))
        {
            return new List<string>();
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(item => NormalizeString(item.ToString()))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToList();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeString(value.GetString());
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new List<string>();
            }

            return normalized.Split(',')
                .Select(item => NormalizeString(item))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToList();
        }

        return new List<string>();
    }

    private static SupplierRegistrationDocumentUpload? NormalizeDocumentUpload(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fileName = NormalizeString(GetString(value, "name"));
        var type = NormalizeString(GetString(value, "type"));
        var content = NormalizeString(GetString(value, "content"));

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        long? size = null;
        if (value.TryGetProperty("size", out var sizeElement))
        {
            if (sizeElement.ValueKind == JsonValueKind.Number && sizeElement.TryGetInt64(out var parsed))
            {
                size = parsed > 0 ? parsed : null;
            }
            else if (sizeElement.ValueKind == JsonValueKind.String &&
                     long.TryParse(sizeElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                size = parsed > 0 ? parsed : null;
            }
        }

        return new SupplierRegistrationDocumentUpload
        {
            Name = fileName,
            Type = type,
            Content = content,
            Size = size,
        };
    }

    private static bool HasValue(SupplierRegistrationNormalizedPayload payload, string field)
    {
        return field switch
        {
            "companyName" => !string.IsNullOrWhiteSpace(payload.CompanyName),
            "registeredOffice" => !string.IsNullOrWhiteSpace(payload.RegisteredOffice),
            "businessRegistrationNumber" => !string.IsNullOrWhiteSpace(payload.BusinessRegistrationNumber),
            "businessAddress" => !string.IsNullOrWhiteSpace(payload.BusinessAddress),
            "contactName" => !string.IsNullOrWhiteSpace(payload.ContactName),
            "contactEmail" => !string.IsNullOrWhiteSpace(payload.ContactEmail),
            "procurementEmail" => !string.IsNullOrWhiteSpace(payload.ProcurementEmail),
            "contactPhone" => !string.IsNullOrWhiteSpace(payload.ContactPhone),
            "operatingCurrency" => !string.IsNullOrWhiteSpace(payload.OperatingCurrency),
            "deliveryLocation" => !string.IsNullOrWhiteSpace(payload.DeliveryLocation),
            "shipCode" => !string.IsNullOrWhiteSpace(payload.ShipCode),
            "productOrigin" => !string.IsNullOrWhiteSpace(payload.ProductOrigin),
            "bankName" => !string.IsNullOrWhiteSpace(payload.BankName),
            "bankAddress" => !string.IsNullOrWhiteSpace(payload.BankAddress),
            "bankAccountNumber" => !string.IsNullOrWhiteSpace(payload.BankAccountNumber),
            "companyType" => !string.IsNullOrWhiteSpace(payload.CompanyType),
            "supplierClassification" => !string.IsNullOrWhiteSpace(payload.SupplierClassification),
            "financeContactName" => !string.IsNullOrWhiteSpace(payload.FinanceContactName),
            "financeContactPhone" => !string.IsNullOrWhiteSpace(payload.FinanceContactPhone),
            _ => false,
        };
    }

    private static string? NormalizeString(string? value)
    {
        if (value == null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }

    private static string? NormalizeEmail(string? value)
    {
        var normalized = NormalizeString(value);
        return normalized?.ToLowerInvariant();
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
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
}
