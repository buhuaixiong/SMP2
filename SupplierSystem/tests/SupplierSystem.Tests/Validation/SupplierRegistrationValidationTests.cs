using System.Text.Json;
using FluentAssertions;
using SupplierSystem.Application.DTOs.Registrations;
using Xunit;

namespace SupplierSystem.Tests.Validation;

public class SupplierRegistrationValidationTests
{
    [Fact]
    public void ValidateRegistration_WhenFinalPayloadMissingPaymentTermsDays_ReturnsRequiredError()
    {
        using var document = JsonDocument.Parse(CreateValidFinalPayload(includePaymentTermsDays: false));

        var result = SupplierRegistrationValidation.ValidateRegistration(
            document.RootElement,
            SupplierRegistrationValidationMode.Final);

        result.Valid.Should().BeFalse();
        result.Errors.Should().ContainKey("paymentTermsDays");
        result.Errors["paymentTermsDays"].Should().Be("REQUIRED");
    }

    [Fact]
    public void ValidateRegistration_WhenFinalPayloadHasPaymentTermsDays_ReturnsValidResult()
    {
        using var document = JsonDocument.Parse(CreateValidFinalPayload(includePaymentTermsDays: true));

        var result = SupplierRegistrationValidation.ValidateRegistration(
            document.RootElement,
            SupplierRegistrationValidationMode.Final);

        result.Valid.Should().BeTrue();
        result.Errors.Should().NotContainKey("paymentTermsDays");
        result.Normalized.PaymentTermsDays.Should().Be("30");
    }

    private static string CreateValidFinalPayload(bool includePaymentTermsDays)
    {
        var paymentTermsField = includePaymentTermsDays
            ? "\n  \"paymentTermsDays\": \"30\","
            : string.Empty;

        return $$"""
{
  "companyName": "Test Company",
  "companyType": "limited",
  "supplierClassification": "DM",
  "registeredOffice": "Shanghai",
  "businessRegistrationNumber": "REG-001",
  "businessAddress": "123 Test Street",
  "contactName": "John Doe",
  "contactEmail": "john@example.com",
  "procurementEmail": "buyer@example.com",
  "contactPhone": "13800138000",
  "financeContactName": "Jane Doe",
  "financeContactPhone": "13900139000",
  "operatingCurrency": "RMB",
  "deliveryLocation": "China",
  "shipCode": "DDP",
  "productOrigin": "CN",
  "invoiceType": "general_vat",{{paymentTermsField}}
  "paymentMethods": ["wire"],
  "bankName": "Test Bank",
  "bankAddress": "Bank Road 1",
  "bankAccountNumber": "6222000000001",
  "businessLicenseFile": {
    "name": "license.pdf",
    "type": "application/pdf",
    "size": 12,
    "content": "dGVzdA=="
  },
  "bankAccountFile": {
    "name": "bank.pdf",
    "type": "application/pdf",
    "size": 12,
    "content": "dGVzdA=="
  }
}
""";
    }
}
