using System.Collections.Concurrent;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class DashboardController
{
    private const int ExpiryThresholdDays = 30;

    private static readonly ConcurrentDictionary<string, bool> TableExistenceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] SupplierDeclinedInvitationStatuses = { "declined", "revoked", "cancelled", "expired" };
    private static readonly string[] SupplierPendingQuoteStatuses = { "draft", "withdrawn" };
    private static readonly string[] SupplierActiveStatuses = { "approved", "qualified", "formal_supplier" };
    private static readonly string[] RequiredComplianceDocumentKeys = { "business_license", "tax_certificate", "bank_information" };

    private static readonly ProfileRequirement[] RequiredProfileFields =
    {
        new("companyName", "Company name", supplier => supplier.CompanyName),
        new("companyId", "Supplier code", supplier => supplier.CompanyId),
        new("contactPerson", "Primary contact name", supplier => supplier.ContactPerson),
        new("contactPhone", "Primary contact phone", supplier => supplier.ContactPhone),
        new("contactEmail", "Primary contact email", supplier => supplier.ContactEmail),
        new("category", "Supplier category", supplier => supplier.Category),
        new("address", "Registered address", supplier => supplier.Address),
        new("businessRegistrationNumber", "Business registration number", supplier => supplier.BusinessRegistrationNumber),
        new("paymentTerms", "Payment terms", supplier => supplier.PaymentTerms),
        new("paymentCurrency", "Payment currency", supplier => supplier.PaymentCurrency),
        new("bankAccount", "Bank account", supplier => supplier.BankAccount),
        new("region", "Region", supplier => supplier.Region),
    };

    private static readonly DocumentRequirement[] RequiredDocumentTypes =
    {
        new("business_license", "Business license", new[] { "business licence", "business_license" }),
        new("tax_certificate", "Tax / VAT certificate", new[] { "tax certificate", "vat certificate", "vat_license" }),
        new("bank_information", "Bank account information", new[] { "bank certificate", "bank statement", "banking_details" }),
    };
}
