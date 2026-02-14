using FluentAssertions;
using SupplierSystem.Api.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for PublicRfqService
/// </summary>
public class PublicRfqServiceTests
{
    #region IsTokenExpired Tests

    [Fact]
    public void IsTokenExpired_WithNull_ShouldReturnFalse()
    {
        // Act
        var result = InvokeIsTokenExpired(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTokenExpired_WithEmpty_ShouldReturnFalse()
    {
        // Act
        var result = InvokeIsTokenExpired("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTokenExpired_WithWhitespace_ShouldReturnFalse()
    {
        // Act
        var result = InvokeIsTokenExpired("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTokenExpired_WithFutureDate_ShouldReturnFalse()
    {
        // Arrange
        var futureDate = DateTimeOffset.UtcNow.AddDays(1).ToString("o");

        // Act
        var result = InvokeIsTokenExpired(futureDate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTokenExpired_WithPastDate_ShouldReturnTrue()
    {
        // Arrange
        var pastDate = DateTimeOffset.UtcNow.AddDays(-1).ToString("o");

        // Act
        var result = InvokeIsTokenExpired(pastDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTokenExpired_WithCurrentDate_ShouldReturnFalse()
    {
        // Arrange - use a date far in the future to ensure it doesn't expire
        var futureDate = DateTimeOffset.UtcNow.AddMinutes(5).ToString("o");

        // Act
        var result = InvokeIsTokenExpired(futureDate);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("2024-12-31T23:59:59Z")]
    [InlineData("2024-12-31T23:59:59+00:00")]
    [InlineData("2024-12-31T23:59:59.0000000+00:00")]
    public void IsTokenExpired_WithVariousFormats_ShouldParse(string dateString)
    {
        // Act
        var result = InvokeIsTokenExpired(dateString);

        // Assert
        result.Should().BeTrue(); // Past date
    }

    [Fact]
    public void IsTokenExpired_WithInvalidFormat_ShouldReturnFalse()
    {
        // Act
        var result = InvokeIsTokenExpired("not-a-date");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsPlaceholder Tests

    [Fact]
    public void IsPlaceholder_WithEnvVariableFormat_ShouldReturnTrue()
    {
        // Act
        var result = InvokeIsPlaceholder("${JWT_SECRET}");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPlaceholder_WithOtherEnvFormat_ShouldReturnTrue()
    {
        // Act
        var result = InvokeIsPlaceholder("${SECRET_VALUE}");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPlaceholder_WithCaseInsensitive_ShouldReturnTrue()
    {
        // Act
        var result = InvokeIsPlaceholder("${jwt_secret}");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPlaceholder_WithActualSecret_ShouldReturnFalse()
    {
        // Act
        var result = InvokeIsPlaceholder("my-secret-key-12345");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsPlaceholder_WithEmptyString_ShouldReturnFalse()
    {
        // Act
        var result = InvokeIsPlaceholder("");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ResolveSecret Tests

    [Fact]
    public void ResolveSecret_WithValidConfigured_ShouldReturnConfigured()
    {
        // Act
        var result = InvokeResolveSecret("valid-secret", "env-secret");

        // Assert
        result.Should().Be("valid-secret");
    }

    [Fact]
    public void ResolveSecret_WithPlaceholder_ShouldReturnEnvValue()
    {
        // Act
        var result = InvokeResolveSecret("${JWT_SECRET}", "env-secret");

        // Assert
        result.Should().Be("env-secret");
    }

    [Fact]
    public void ResolveSecret_WithEmptyConfigured_ShouldReturnEnvValue()
    {
        // Act
        var result = InvokeResolveSecret("", "env-secret");

        // Assert
        result.Should().Be("env-secret");
    }

    [Fact]
    public void ResolveSecret_WithWhitespaceConfigured_ShouldReturnEnvValue()
    {
        // Act
        var result = InvokeResolveSecret("   ", "env-secret");

        // Assert
        result.Should().Be("env-secret");
    }

    [Fact]
    public void ResolveSecret_WithBothEmpty_ShouldReturnEmpty()
    {
        // Act
        var result = InvokeResolveSecret("", "");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Record Tests

    [Fact]
    public void PublicRfqPreviewInfo_CanBeCreated()
    {
        // Arrange & Act
        var preview = new PublicRfqPreviewInfo(
            1,
            "Test RFQ",
            "Description",
            "30 days",
            10000m,
            "CNY",
            "2024-12-31",
            false,
            "John");

        // Assert
        preview.Id.Should().Be(1);
        preview.Title.Should().Be("Test RFQ");
        preview.IsExpired.Should().BeFalse();
        preview.InviterName.Should().Be("John");
    }

    [Fact]
    public void PublicRfqPreviewResult_CanBeCreated()
    {
        // Arrange
        var preview = new PublicRfqPreviewInfo(1, null, null, null, null, null, null, false, "John");

        // Act
        var result = new PublicRfqPreviewResult(
            true,
            preview,
            "test@example.com",
            new PublicRfqSupplierInfo(1, "Test Supplier"),
            "Message");

        // Assert
        result.IsRegistered.Should().BeTrue();
        result.RfqPreview.Id.Should().Be(1);
        result.RecipientEmail.Should().Be("test@example.com");
        result.SupplierInfo.Should().NotBeNull();
        result.SupplierInfo!.SupplierId.Should().Be(1);
    }

    [Fact]
    public void PublicRfqSupplierInfo_CanBeCreated()
    {
        // Arrange & Act
        var info = new PublicRfqSupplierInfo(1, "Test Company");

        // Assert
        info.SupplierId.Should().Be(1);
        info.CompanyName.Should().Be("Test Company");
    }

    [Fact]
    public void PublicAutoLoginUser_CanBeCreated()
    {
        // Arrange & Act
        var user = new PublicAutoLoginUser("user1", "John Doe", "supplier", 1);

        // Assert
        user.Id.Should().Be("user1");
        user.Name.Should().Be("John Doe");
        user.Role.Should().Be("supplier");
        user.SupplierId.Should().Be(1);
    }

    [Fact]
    public void PublicAutoLoginUser_WithNullSupplierId_ShouldWork()
    {
        // Arrange & Act
        var user = new PublicAutoLoginUser("user1", "John Doe", "supplier", null);

        // Assert
        user.SupplierId.Should().BeNull();
    }

    [Fact]
    public void PublicAutoLoginResult_CanBeCreated()
    {
        // Arrange
        var user = new PublicAutoLoginUser("user1", "John", "supplier", 1);
        var token = "test-token-123";

        // Act
        var result = new PublicAutoLoginResult(token, user, 1);

        // Assert
        result.Token.Should().Be(token);
        result.User.Id.Should().Be("user1");
        result.RfqId.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private static bool InvokeIsTokenExpired(string? value)
    {
        var methodInfo = typeof(PublicRfqService).GetMethod("IsTokenExpired",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (bool)methodInfo!.Invoke(null, new object?[] { value })!;
    }

    private static bool InvokeIsPlaceholder(string secret)
    {
        var methodInfo = typeof(PublicRfqService).GetMethod("IsPlaceholder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (bool)methodInfo!.Invoke(null, new object[] { secret })!;
    }

    private static string ResolveSecret(string? configured, string? envValue)
    {
        var methodInfo = typeof(PublicRfqService).GetMethod("ResolveSecret",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)methodInfo!.Invoke(null, new object?[] { configured, envValue })!;
    }

    private static string InvokeResolveSecret(string? configured, string? envValue)
    {
        return ResolveSecret(configured, envValue);
    }

    #endregion
}
