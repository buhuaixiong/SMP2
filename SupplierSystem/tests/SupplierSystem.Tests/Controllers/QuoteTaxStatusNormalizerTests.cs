using FluentAssertions;
using SupplierSystem.Api.Controllers;
using Xunit;

namespace SupplierSystem.Tests.Controllers;

public class QuoteTaxStatusNormalizerTests
{
    [Theory]
    [InlineData("inclusive", "inclusive")]
    [InlineData("exclusive", "exclusive")]
    [InlineData("  INCLUSIVE  ", "inclusive")]
    [InlineData("ExClUsIvE", "exclusive")]
    [InlineData("含税", "inclusive")]
    [InlineData("不含税", "exclusive")]
    public void TryNormalize_WithKnownValue_ShouldReturnCanonicalValue(string input, string expected)
    {
        // Act
        var result = QuoteTaxStatusNormalizer.TryNormalize(input, out var normalized);

        // Assert
        result.Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_WithEmptyValue_ShouldReturnNullAndSuccess(string? input)
    {
        // Act
        var result = QuoteTaxStatusNormalizer.TryNormalize(input, out var normalized);

        // Assert
        result.Should().BeTrue();
        normalized.Should().BeNull();
    }

    [Fact]
    public void TryNormalize_WithUnknownValue_ShouldReturnFalse()
    {
        // Act
        var result = QuoteTaxStatusNormalizer.TryNormalize("unknown-tax-status", out var normalized);

        // Assert
        result.Should().BeFalse();
        normalized.Should().BeNull();
    }
}
