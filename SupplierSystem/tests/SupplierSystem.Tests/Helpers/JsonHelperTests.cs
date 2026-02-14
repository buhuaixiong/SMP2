using System.Text.Json;
using FluentAssertions;
using SupplierSystem.Api.Helpers;
using Xunit;

namespace SupplierSystem.Tests.Helpers;

public class JsonHelperTests
{
    [Fact]
    public void TryGetProperty_WhenNotObject_ReturnsFalse()
    {
        using var document = JsonDocument.Parse("[1,2]");

        JsonHelper.TryGetProperty(document.RootElement, "name", out var value).Should().BeFalse();
        value.ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Fact]
    public void GetString_ReturnsStringValue()
    {
        using var document = JsonDocument.Parse("{\"name\":\"test\",\"count\":3}");

        JsonHelper.GetString(document.RootElement, "name").Should().Be("test");
        JsonHelper.GetString(document.RootElement, "count").Should().BeNull();
    }

    [Fact]
    public void GetNumberHelpers_HandleTypes()
    {
        using var document = JsonDocument.Parse("{\"amount\":12.5,\"count\":3,\"flag\":true,\"text\":\"true\"}");

        JsonHelper.GetDecimal(document.RootElement, "amount").Should().Be(12.5m);
        JsonHelper.GetDecimal(document.RootElement, "text").Should().BeNull();

        JsonHelper.GetInt(document.RootElement, "count").Should().Be(3);
        JsonHelper.GetInt(document.RootElement, "amount").Should().BeNull();

        JsonHelper.GetBool(document.RootElement, "flag").Should().BeTrue();
        JsonHelper.GetBool(document.RootElement, "text").Should().BeNull();
    }
}
