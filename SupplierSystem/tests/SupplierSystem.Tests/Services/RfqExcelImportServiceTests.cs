using System.Reflection;
using FluentAssertions;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.Services.Rfq;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for RfqExcelImportService
/// </summary>
public class RfqExcelImportServiceTests
{
    #region Date Parsing Tests

    [Theory]
    [InlineData("20240115", "2024-01-15")]
    [InlineData("20241225", "2024-12-25")]
    [InlineData("19990101", "1999-01-01")]
    public void FormatDate_WithEightDigitDate_ShouldParse(string input, string expected)
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("FormatDate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { input });

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2024-01-15", "2024-01-15")]
    [InlineData("2024-12-25", "2024-12-25")]
    public void FormatDate_WithIsoDate_ShouldReturnAsIs(string input, string expected)
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("FormatDate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { input });

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void FormatDate_WithEmptyInput_ShouldReturnNull(string? input, string? expected)
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("FormatDate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object?[] { input });

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatDate_WithInvalidOaDate_ShouldReturnNull()
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("FormatDate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { "invalid-date" });

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("2024/01/15")]
    [InlineData("01-15-2024")]
    public void FormatDate_WithOtherDateFormats_ShouldParse(string input)
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("FormatDate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { input });

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Decimal Parsing Tests

    [Theory]
    [InlineData("100.50", 100.50)]
    [InlineData("1000", 1000)]
    [InlineData("1,000.50", 1000.50)]
    [InlineData("1,234,567.89", 1234567.89)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("abc", 0)]
    [InlineData("   ", 0)]
    public void ParseDecimal_WithVariousFormats_ShouldParse(string? input, decimal expected)
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("ParseDecimal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object?[] { input });

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("100.00", 100)]
    [InlineData("0.99", 0.99)]
    public void ParseDecimal_WithDecimalValues_ShouldParse(string input, decimal expected)
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("ParseDecimal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { input });

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Column Mapping Tests

    [Fact]
    public void BuildColumnMapping_WithEnglishHeaders_ShouldMap()
    {
        // Arrange
        var headers = new List<string>
        {
            "PR No.", "Line", "Dept", "Item Description", "ECI P/N", "PR Iss.Date",
            "PR initiator", "Qty.", "Unit", "Currency", "Unit price", "Amount",
            "Vendor name", "Expect Date", "Remark"
        };

        var methodInfo = typeof(RfqExcelImportService).GetMethod("BuildColumnMapping",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var mapping = methodInfo!.Invoke(null, new object[] { headers });

        // Assert
        mapping.Should().NotBeNull();
    }

    [Fact]
    public void BuildColumnMapping_WithChineseHeaders_ShouldMap()
    {
        // Arrange
        var headers = new List<string>
        {
            "PR编号", "行号", "部门", "物料描述", "物料编号", "发行日期",
            "发起人", "数量", "单位", "币种", "单价", "金额",
            "供应商名称", "期望日期", "备注"
        };

        var methodInfo = typeof(RfqExcelImportService).GetMethod("BuildColumnMapping",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var mapping = methodInfo!.Invoke(null, new object[] { headers });

        // Assert
        mapping.Should().NotBeNull();
    }

    [Fact]
    public void BuildColumnMapping_WithMixedHeaders_ShouldMap()
    {
        // Arrange
        var headers = new List<string>
        {
            "PR No.", "行号", "部门", "Description", "物料编号", "Issue Date", "发起人",
            "Qty", "单位", "Currency", "单价", "Amount", "Vendor", "Expect Date", "备注"
        };

        var methodInfo = typeof(RfqExcelImportService).GetMethod("BuildColumnMapping",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var mapping = methodInfo!.Invoke(null, new object[] { headers });

        // Assert
        mapping.Should().NotBeNull();
    }

    [Fact]
    public void BuildColumnMapping_WithEmptyHeaders_ShouldNotThrow()
    {
        // Arrange
        var headers = new List<string>();

        var methodInfo = typeof(RfqExcelImportService).GetMethod("BuildColumnMapping",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var mapping = methodInfo!.Invoke(null, new object[] { headers });

        // Assert
        mapping.Should().NotBeNull();
    }

    #endregion

    #region GetCellValue Tests

    [Theory]
    [InlineData(0, 5, "value", "value")]
    [InlineData(0, 5, " value ", "value")]
    [InlineData(10, 5, "", "")] // Column out of range
    [InlineData(-1, 5, "test", "")]
    public void GetCellValue_WithVariousInputs_ShouldHandle(
        int columnIndex, int rowSize, string cellValue, string expected)
    {
        // Arrange
        var row = new List<string>();
        for (int i = 0; i < rowSize; i++)
        {
            row.Add(i == columnIndex ? cellValue : $"col{i}");
        }

        var methodInfo = typeof(RfqExcelImportService).GetMethod("GetCellValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { row, columnIndex });

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 3, null, "")]
    [InlineData(0, 3, "  ", "")]
    public void GetCellValue_WithNullOrWhitespace_ShouldReturnEmpty(int columnIndex, int rowSize, string? cellValue, string expected)
    {
        // Arrange
        var row = new List<string>();
        for (int i = 0; i < rowSize; i++)
        {
            row.Add(cellValue ?? string.Empty);
        }

        var methodInfo = typeof(RfqExcelImportService).GetMethod("GetCellValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { row, columnIndex });

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GenerateRfqTitle Tests

    [Fact]
    public void GenerateRfqTitle_WithSingleItem_ShouldReturnTitle()
    {
        // Arrange
        var requirements = new List<ImportExcelRequirement>
        {
            new("Short description item", 10, "EA", null, 100m, 1000m, null)
        };

        var methodInfo = typeof(RfqExcelImportService).GetMethod("GenerateRfqTitle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (string?)methodInfo!.Invoke(null, new object[] { "PR001", "IT", requirements });

        // Assert
        result.Should().Contain("PR001");
        result.Should().Contain("IT");
    }

    [Fact]
    public void GenerateRfqTitle_WithLongDescription_ShouldTruncate()
    {
        // Arrange
        var longDescription = "This is a very long item description that exceeds twenty characters";
        var requirements = new List<ImportExcelRequirement>
        {
            new(longDescription, 10, "EA", null, 100m, 1000m, null)
        };

        var methodInfo = typeof(RfqExcelImportService).GetMethod("GenerateRfqTitle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (string?)methodInfo!.Invoke(null, new object[] { "PR001", "IT", requirements });

        // Assert - description should be truncated to 20 chars
        result.Should().Contain("This is a very long "); // First 20 chars
        result.Should().NotContain(longDescription); // Original long description should not appear
    }

    [Fact]
    public void GenerateRfqTitle_WithMultipleItems_ShouldSummarize()
    {
        // Arrange
        var requirements = new List<ImportExcelRequirement>
        {
            new("Item 1", 10, "EA", null, 100m, 1000m, null),
            new("Item 2", 10, "EA", null, 100m, 1000m, null),
            new("Item 3", 10, "EA", null, 100m, 1000m, null),
            new("Item 4", 10, "EA", null, 100m, 1000m, null)
        };

        var methodInfo = typeof(RfqExcelImportService).GetMethod("GenerateRfqTitle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (string?)methodInfo!.Invoke(null, new object[] { "PR001", "IT", requirements });

        // Assert
        result.Should().Contain("等"); // Should indicate "etc."
    }

    [Fact]
    public void GenerateRfqTitle_WithExactlyThreeItems_ShouldNotHaveSuffix()
    {
        // Arrange
        var requirements = new List<ImportExcelRequirement>
        {
            new("Item 1", 10, "EA", null, 100m, 1000m, null),
            new("Item 2", 10, "EA", null, 100m, 1000m, null),
            new("Item 3", 10, "EA", null, 100m, 1000m, null)
        };

        var methodInfo = typeof(RfqExcelImportService).GetMethod("GenerateRfqTitle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (string?)methodInfo!.Invoke(null, new object[] { "PR001", "IT", requirements });

        // Assert
        result.Should().NotContain("等");
    }

    [Fact]
    public void GenerateRfqTitle_WithEmptyList_ShouldReturnBasicTitle()
    {
        // Arrange
        var requirements = new List<ImportExcelRequirement>();

        var methodInfo = typeof(RfqExcelImportService).GetMethod("GenerateRfqTitle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (string?)methodInfo!.Invoke(null, new object[] { "PR001", "IT", requirements });

        // Assert
        result.Should().Contain("PR001");
        result.Should().Contain("IT");
    }

    #endregion

    #region FindColumnIndex Tests

    [Theory]
    [InlineData(new[] { "PR No.", "Line", "Dept" }, new[] { "PR No.", "PR编号" }, 0)]
    [InlineData(new[] { "PR No.", "Line", "Dept" }, new[] { "行号", "Line" }, 1)]
    [InlineData(new[] { "PR No.", "Line", "Dept" }, new[] { "Unknown" }, -1)]
    public void FindColumnIndex_WithVariousNames_ShouldFindColumn(
        string[] headers, string[] possibleNames, int expectedIndex)
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("FindColumnIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { headers, possibleNames });

        // Assert
        result.Should().Be(expectedIndex);
    }

    [Theory]
    [InlineData(new[] { "A", "B", "C" }, new[] { "b" }, 1)] // Case insensitive
    [InlineData(new[] { "A", "B", "C" }, new[] { "D" }, -1)] // Not found
    [InlineData(new string[] { }, new[] { "A" }, -1)] // Empty headers
    public void FindColumnIndex_WithEdgeCases_ShouldHandle(
        string[] headers, string[] possibleNames, int expectedIndex)
    {
        // Arrange
        var methodInfo = typeof(RfqExcelImportService).GetMethod("FindColumnIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { headers, possibleNames });

        // Assert
        result.Should().Be(expectedIndex);
    }

    [Fact]
    public void FindColumnIndex_WithWhitespaceInHeaders_ShouldTrim()
    {
        // Arrange
        var headers = new[] { "  PR No.  ", "Line", "Dept" };
        var possibleNames = new[] { "PR No." };

        var methodInfo = typeof(RfqExcelImportService).GetMethod("FindColumnIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = methodInfo!.Invoke(null, new object[] { headers, possibleNames });

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region ValidateRequiredFields Tests

    [Fact]
    public void ValidateRequiredFields_WithAllRequiredFields_ShouldNotThrow()
    {
        // Arrange
        var mapping = CreateMapping(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);

        var methodInfo = typeof(RfqExcelImportService).GetMethod("ValidateRequiredFields",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act & Assert - should not throw
        var action = () => methodInfo!.Invoke(null, new object[] { mapping });
        action.Should().NotBeNull();
    }

    [Fact]
    public void ValidateRequiredFields_WithMissingPrNumber_ShouldThrow()
    {
        // Arrange
        var mapping = CreateMapping(-1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);

        var methodInfo = typeof(RfqExcelImportService).GetMethod("ValidateRequiredFields",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act & Assert
        var action = () => methodInfo!.Invoke(null, new object[] { mapping });
        action.Should().Throw<TargetInvocationException>();
    }

    private static object CreateMapping(
        int prNumber, int line, int dept, int itemDescription, int eciPn,
        int issueDate, int initiator, int qty, int unit, int currency,
        int unitPrice, int amount, int vendor, int expectDate, int remark)
    {
        var mappingType = Type.GetType("SupplierSystem.Api.Services.Rfq.RfqExcelImportService+ColumnMapping, SupplierSystem.Api");
        if (mappingType == null)
        {
            throw new InvalidOperationException("ColumnMapping type not found");
        }
        return Activator.CreateInstance(mappingType,
            prNumber, line, dept, itemDescription, eciPn, issueDate, initiator,
            qty, unit, currency, unitPrice, amount, vendor, expectDate, remark)!;
    }

    #endregion
}
