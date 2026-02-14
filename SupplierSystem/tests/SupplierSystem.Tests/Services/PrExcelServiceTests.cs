using FluentAssertions;
using SupplierSystem.Api.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for PrExcelService
/// </summary>
public class PrExcelServiceTests
{
    #region ExcelDateSerial Tests

    [Fact]
    public void ExcelDateSerial_WithDateTimeNow_ShouldReturnValidSerial()
    {
        // Arrange
        var testDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Local);

        // Act
        var result = InvokeExcelDateSerial(testDate);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExcelDateSerial_WithUtcDate_ShouldConvertToLocal()
    {
        // Arrange
        var utcDate = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var result = InvokeExcelDateSerial(utcDate);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExcelDateSerial_WithEpochDate_ShouldReturnZero()
    {
        // Arrange - Excel epoch is 1899-12-30
        var epoch = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Local);

        // Act
        var result = InvokeExcelDateSerial(epoch);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ExcelDateSerial_WithYear2000_ShouldReturnCorrectValue()
    {
        // Arrange
        var year2000 = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);

        // Act
        var result = InvokeExcelDateSerial(year2000);

        // Assert
        result.Should().Be(36526); // 2000-01-01 is 36526 days after 1899-12-30
    }

    #endregion

    #region ConvertToUsd Tests

    [Theory]
    [InlineData("CNY", 7.2, 72)] // rate=7.2, amount=72 CNY -> 10 USD
    [InlineData("USD", 1, 10)] // rate=1, amount=10 USD -> 10 USD
    [InlineData("EUR", 0.91, 9.1)] // rate=0.91, amount=9.1 EUR -> 10 USD
    [InlineData("GBP", 0.79, 7.9)] // rate=0.79, amount=7.9 GBP -> 10 USD
    [InlineData("JPY", 149.5, 1495)] // rate=149.5, amount=1495 JPY -> 10 USD
    [InlineData("HKD", 7.8, 78)] // rate=7.8, amount=78 HKD -> 10 USD
    [InlineData("THB", 35.5, 355)] // rate=35.5, amount=355 THB -> 10 USD
    public void ConvertToUsd_WithVariousCurrencies_ShouldConvert(string currency, double rate, double amount)
    {
        // Act
        var result = InvokeConvertToUsd(amount, currency);

        // Assert
        rate.Should().BeGreaterThan(0);
        result.Should().BeApproximately(10, 0.01); // Should convert to ~10 USD
    }

    [Fact]
    public void ConvertToUsd_WithZeroAmount_ShouldReturnZero()
    {
        // Act
        var result = InvokeConvertToUsd(0, "USD");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ConvertToUsd_WithNaN_ShouldReturnZero()
    {
        // Act
        var result = InvokeConvertToUsd(double.NaN, "USD");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ConvertToUsd_WithInfinity_ShouldReturnZero()
    {
        // Act
        var result = InvokeConvertToUsd(double.PositiveInfinity, "USD");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ConvertToUsd_WithUnknownCurrency_ShouldUseRateOf1()
    {
        // Act
        var result = InvokeConvertToUsd(100, "XYZ");

        // Assert
        result.Should().Be(100);
    }

    [Theory]
    [InlineData("cny", 72)] // 72 CNY = 10 USD
    [InlineData("CNY", 72)]
    [InlineData("CnY", 72)]
    [InlineData("usd", 10)] // 10 USD = 10 USD
    [InlineData("USD", 10)]
    [InlineData("eur", 9.1)] // 9.1 EUR = 10 USD
    [InlineData("EUR", 9.1)]
    public void ConvertToUsd_WithCaseInsensitiveCurrency_ShouldConvert(string currency, double amount)
    {
        // Act
        var result = InvokeConvertToUsd(amount, currency);

        // Assert
        result.Should().BeApproximately(10, 0.01); // ~10 USD
    }

    #endregion

    #region SetCellValue Tests

    [Fact]
    public void SetCellValue_WithEmptyCellRef_ShouldReturnOriginalXml()
    {
        // Arrange
        var xml = "<root><cell>A1</cell></root>";

        // Act
        var result = InvokeSetCellValue(xml, "", "value");

        // Assert
        result.Should().Be(xml);
    }

    [Fact]
    public void SetCellValue_WithWhitespaceCellRef_ShouldReturnOriginalXml()
    {
        // Arrange
        var xml = "<root><cell>A1</cell></root>";

        // Act
        var result = InvokeSetCellValue(xml, "   ", "value");

        // Assert
        result.Should().Be(xml);
    }

    [Fact]
    public void SetCellValue_WithNumericValue_ShouldCreateNumericCell()
    {
        // Arrange
        var xml = "<sheetData><row r=\"1\"><c r=\"A1\"><v>old</v></c></row></sheetData>";

        // Act
        var result = InvokeSetCellValue(xml, "A1", 123, "n");

        // Assert
        result.Should().Contain("<c r=\"A1\" t=\"n\"><v>123</v></c>");
    }

    [Fact]
    public void SetCellValue_WithStringValue_ShouldCreateStringCell()
    {
        // Arrange
        var xml = "<sheetData><row r=\"1\"><c r=\"A1\"><v>old</v></c></row></sheetData>";

        // Act
        var result = InvokeSetCellValue(xml, "A1", "test");

        // Assert
        result.Should().Contain("<c r=\"A1\" t=\"inlineStr\"");
        result.Should().Contain("<t>test</t>");
    }

    #endregion

    #region EscapeXmlValue Tests

    [Theory]
    [InlineData("test", "test")]
    [InlineData("test<test", "test&lt;test")]
    [InlineData("test>test", "test&gt;test")]
    [InlineData("test&test", "test&amp;test")]
    [InlineData("test\"test", "test&quot;test")]
    [InlineData("test'test", "test&apos;test")]
    [InlineData("<>&\"'", "&lt;&gt;&amp;&quot;&apos;")]
    public void EscapeXmlValue_ShouldEscapeSpecialCharacters(string input, string expected)
    {
        // Act
        var result = InvokeEscapeXmlValue(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EscapeXmlValue_WithNull_ShouldReturnEmpty()
    {
        // Act
        var result = InvokeEscapeXmlValue(null!);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ResolveUserName Tests

    [Fact]
    public void ResolveUserName_WithName_ShouldReturnName()
    {
        // Arrange
        var user = new PrExcelUserContext { Id = "1", Name = "John Doe", Username = "john" };

        // Act
        var result = InvokeResolveUserName(user);

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public void ResolveUserName_WithOnlyUsername_ShouldReturnUsername()
    {
        // Arrange
        var user = new PrExcelUserContext { Id = "1", Username = "john" };

        // Act
        var result = InvokeResolveUserName(user);

        // Assert
        result.Should().Be("john");
    }

    [Fact]
    public void ResolveUserName_WithOnlyId_ShouldReturnId()
    {
        // Arrange
        var user = new PrExcelUserContext { Id = "user123" };

        // Act
        var result = InvokeResolveUserName(user);

        // Assert
        result.Should().Be("user123");
    }

    [Fact]
    public void ResolveUserName_WithWhitespaceName_ShouldReturnUsername()
    {
        // Arrange
        var user = new PrExcelUserContext { Id = "1", Name = "   ", Username = "john" };

        // Act
        var result = InvokeResolveUserName(user);

        // Assert
        result.Should().Be("john");
    }

    #endregion

    #region BuildCellXml Tests

    [Fact]
    public void BuildCellXml_WithNumericValue_ShouldCreateNumericCell()
    {
        // Act
        var result = InvokeBuildCellXml("A1", 123.45, "n");

        // Assert
        result.Should().Be("<c r=\"A1\" t=\"n\"><v>123.45</v></c>");
    }

    [Fact]
    public void BuildCellXml_WithStringValue_ShouldCreateStringCell()
    {
        // Act
        var result = InvokeBuildCellXml("A1", "test", "s");

        // Assert
        result.Should().Contain("<c r=\"A1\" t=\"inlineStr\"");
        result.Should().Contain("<t>test</t>");
    }

    [Fact]
    public void BuildCellXml_WithInvalidNumeric_ShouldReturnZero()
    {
        // Act
        var result = InvokeBuildCellXml("A1", "invalid", "n");

        // Assert
        result.Should().Contain("<v>0</v>");
    }

    #endregion

    #region Helper Methods

    private static int InvokeExcelDateSerial(DateTime date)
    {
        var methodInfo = typeof(PrExcelService).GetMethod("ExcelDateSerial",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (int)methodInfo!.Invoke(null, new object[] { date })!;
    }

    private static double InvokeConvertToUsd(double amount, string currency)
    {
        var methodInfo = typeof(PrExcelService).GetMethod("ConvertToUsd",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (double)methodInfo!.Invoke(null, new object[] { amount, currency })!;
    }

    private static string InvokeSetCellValue(string xml, string cellRef, object? value, string type = "s")
    {
        var methodInfo = typeof(PrExcelService).GetMethod("SetCellValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)methodInfo!.Invoke(null, new object?[] { xml, cellRef, value, type })!;
    }

    private static string InvokeEscapeXmlValue(object? value)
    {
        var methodInfo = typeof(PrExcelService).GetMethod("EscapeXmlValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)methodInfo!.Invoke(null, new object?[] { value })!;
    }

    private static string InvokeResolveUserName(PrExcelUserContext user)
    {
        var methodInfo = typeof(PrExcelService).GetMethod("ResolveUserName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)methodInfo!.Invoke(null, new object[] { user })!;
    }

    private static string InvokeBuildCellXml(string cellRef, object? value, string type)
    {
        var methodInfo = typeof(PrExcelService).GetMethod("BuildCellXml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)methodInfo!.Invoke(null, new object?[] { cellRef, value, type })!;
    }

    #endregion
}
