using System.Reflection;
using FluentAssertions;
using SupplierSystem.Api.Services.ItemMaster;
using Xunit;

namespace SupplierSystem.Tests.Services;

public class ItemMasterImportServiceTests
{
    [Fact]
    public void BuildColumnMap_WithRequiredHeaders_ShouldResolveRequiredColumns()
    {
        var headers = new List<string>
        {
            "Fac",
            "Item Number",
            "Vendor",
            "Sourcing name",
            "Item Description",
        };

        var method = typeof(ItemMasterImportService).GetMethod(
            "BuildColumnMap",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = method!.Invoke(null, new object[] { headers });
        var map = result.Should().BeOfType<Dictionary<string, int>>().Subject;

        map["fac"].Should().Be(0);
        map["item_number"].Should().Be(1);
        map["vendor"].Should().Be(2);
        map["sourcing_name"].Should().Be(3);
    }

    [Fact]
    public void ValidateRequiredColumns_WhenMissingRequiredField_ShouldThrow()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["fac"] = 0,
            ["item_number"] = 1,
            ["vendor"] = 2,
        };

        var method = typeof(ItemMasterImportService).GetMethod(
            "ValidateRequiredColumns",
            BindingFlags.NonPublic | BindingFlags.Static);

        Action act = () => method!.Invoke(null, new object[] { map });

        act.Should()
            .Throw<TargetInvocationException>()
            .Where(ex =>
                ex.InnerException != null &&
                ex.InnerException.GetType() == typeof(InvalidOperationException) &&
                ex.InnerException.Message.Contains("Sourcing name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CollapseDuplicateRows_WithSameCompositeKey_ShouldKeepLastRow()
    {
        var rows = new List<Dictionary<string, string?>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["fac"] = "hz",
                ["item_number"] = "1001",
                ["vendor"] = "v001",
                ["item_description"] = "old row",
            },
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["fac"] = " HZ ",
                ["item_number"] = "1001",
                ["vendor"] = "V001",
                ["item_description"] = "new row",
            },
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["fac"] = "TH",
                ["item_number"] = "2002",
                ["vendor"] = "V900",
                ["item_description"] = "another row",
            },
        };

        var warnings = new List<string>();
        var method = typeof(ItemMasterImportService).GetMethod(
            "CollapseDuplicateRows",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = method!.Invoke(null, new object[] { rows, warnings });
        var deduplicated = result.Should().BeOfType<List<Dictionary<string, string?>>>().Subject;

        deduplicated.Should().HaveCount(2);
        deduplicated.Should().Contain(row =>
            string.Equals(row["item_description"], "new row", StringComparison.Ordinal));
        warnings.Should().ContainSingle(message =>
            message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveOwnerUserId_ShouldMatchUsernameCaseInsensitive()
    {
        var usersByUsername = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alice.Wu"] = "u-001",
        };

        var method = typeof(ItemMasterImportService).GetMethod(
            "ResolveOwnerUserId",
            BindingFlags.NonPublic | BindingFlags.Static);

        var matched = method!.Invoke(null, new object?[] { "alice.wu", usersByUsername }) as string;
        var unmatched = method!.Invoke(null, new object?[] { "bob.lee", usersByUsername }) as string;

        matched.Should().Be("u-001");
        unmatched.Should().BeNull();
    }
}



