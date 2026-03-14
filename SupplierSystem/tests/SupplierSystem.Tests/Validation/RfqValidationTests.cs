using System.Text.Json;
using FluentAssertions;
using SupplierSystem.Api.Validation;
using Xunit;

namespace SupplierSystem.Tests.Validation;

public class RfqValidationTests
{
    [Fact]
    public void ValidateCreateRfq_WhenMissingRequiredFields_ReturnsErrors()
    {
        using var document = JsonDocument.Parse("{}");

        var details = RfqValidation.ValidateCreateRfq(document.RootElement);

        details.Should().Contain(d => d.Field == "title" && d.Type == "any.required");
        details.Should().Contain(d => d.Field == "description" && d.Type == "any.required");
        details.Should().Contain(d => d.Field == "rfqType" && d.Type == "any.required");
        details.Should().Contain(d => d.Field == "deliveryPeriod" && d.Type == "any.required");
    }

    [Fact]
    public void ValidateCreateRfq_WhenInvalidTypeAndItemsNotArray_ReturnsErrors()
    {
        var json = @"{
  ""title"": ""RFQ Title"",
  ""description"": ""Valid description"",
  ""rfqType"": ""invalid"",
  ""deliveryPeriod"": ""Q1"",
  ""items"": {}
}";
        using var document = JsonDocument.Parse(json);

        var details = RfqValidation.ValidateCreateRfq(document.RootElement);

        details.Should().Contain(d => d.Field == "rfqType" && d.Type == "any.only");
        details.Should().Contain(d => d.Field == "items" && d.Type == "array.base");
    }

    [Fact]
    public void ValidateUpdateRfq_WhenInvalidFields_ReturnsErrors()
    {
        var json = @"{
  ""title"": 123,
  ""validUntil"": ""not-a-date"",
  ""budgetAmount"": -5
}";
        using var document = JsonDocument.Parse(json);

        var details = RfqValidation.ValidateUpdateRfq(document.RootElement);

        details.Should().Contain(d => d.Field == "title" && d.Type == "string.base");
        details.Should().Contain(d => d.Field == "validUntil" && d.Type == "date.format");
        details.Should().Contain(d => d.Field == "budgetAmount" && d.Type == "number.positive");
    }

    [Fact]
    public void ValidateSubmitQuote_WhenInvalidPayload_ReturnsErrors()
    {
        var json = @"{
  ""totalPrice"": -10,
  ""currency"": """",
  ""deliveryPeriod"": 123,
  ""items"": [
    { ""description"": """", ""quantity"": 0, ""unitPrice"": 0 }
  ]
}";
        using var document = JsonDocument.Parse(json);

        var details = RfqValidation.ValidateSubmitQuote(document.RootElement);

        details.Should().Contain(d => d.Field == "totalPrice" && d.Type == "number.positive");
        details.Should().Contain(d => d.Field == "currency" && d.Type == "any.required");
        details.Should().Contain(d => d.Field == "deliveryPeriod" && d.Type == "string.base");
        details.Should().Contain(d => d.Field == "items[0].description" && d.Type == "any.required");
        details.Should().Contain(d => d.Field == "items[0].quantity" && d.Type == "number.positive");
        details.Should().Contain(d => d.Field == "items[0].unitPrice" && d.Type == "number.positive");
    }

    [Fact]
    public void ValidateSendInvitations_WhenMissingSupplierIds_ReturnsError()
    {
        using var document = JsonDocument.Parse("{}");

        var details = RfqValidation.ValidateSendInvitations(document.RootElement);

        details.Should().Contain(d => d.Field == "supplierIds" && d.Type == "any.required");
    }

    [Fact]
    public void ValidateSendInvitations_WhenEmptyArray_ReturnsError()
    {
        using var document = JsonDocument.Parse("{\"supplierIds\":[]}");

        var details = RfqValidation.ValidateSendInvitations(document.RootElement);

        details.Should().Contain(d => d.Field == "supplierIds" && d.Type == "array.min");
    }

    [Fact]
    public void ValidateReview_WhenInvalidPayload_ReturnsErrors()
    {
        using var document = JsonDocument.Parse("{\"selectedQuoteId\":\"abc\"}");

        var details = RfqValidation.ValidateReview(document.RootElement);

        details.Should().Contain(d => d.Field == "selectedQuoteId" && d.Type == "number.integer");
        details.Should().Contain(d => d.Field == "reviewScores" && d.Type == "any.required");
    }
}
