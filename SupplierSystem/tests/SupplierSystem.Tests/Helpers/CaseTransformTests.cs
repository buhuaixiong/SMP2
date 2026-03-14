using System.Text.Json;
using FluentAssertions;
using SupplierSystem.Api.Helpers;
using Xunit;

namespace SupplierSystem.Tests.Helpers;

public class CaseTransformTests
{
    [Fact]
    public void ToCamelCase_WithNull_ReturnsNull()
    {
        CaseTransform.ToCamelCase(null).Should().BeNull();
    }

    [Fact]
    public void ToCamelCase_WithString_ReturnsSameString()
    {
        CaseTransform.ToCamelCase("alreadyCamel").Should().Be("alreadyCamel");
    }

    [Fact]
    public void ToCamelCase_WithDictionary_ConvertsKeysRecursively()
    {
        using var document = JsonDocument.Parse("{\"snake_key\":1}");
        var input = new Dictionary<string, object?>
        {
            ["first_key"] = 123,
            ["nested_value"] = new Dictionary<string, object?>
            {
                ["second_key"] = "value"
            },
            ["items"] = new[]
            {
                new Dictionary<string, object?> { ["third_key"] = true }
            },
            ["raw_json"] = document.RootElement
        };

        var result = CaseTransform.ToCamelCase(input) as Dictionary<string, object?>;

        result.Should().NotBeNull();
        var mapped = result!;
        mapped.Should().ContainKey("firstKey");
        mapped.Should().ContainKey("nestedValue");
        mapped.Should().ContainKey("items");
        mapped.Should().ContainKey("rawJson");

        var nested = mapped["nestedValue"] as Dictionary<string, object?>;
        nested.Should().NotBeNull();
        var nestedMap = nested!;
        nestedMap.Should().ContainKey("secondKey");

        var list = mapped["items"] as List<object?>;
        list.Should().NotBeNull();
        var itemList = list!;
        itemList.Should().NotBeEmpty();
        var firstItem = itemList[0] as Dictionary<string, object?>;
        firstItem.Should().NotBeNull();
        var firstItemMap = firstItem!;
        firstItemMap.Should().ContainKey("thirdKey");

        mapped["rawJson"].Should().BeOfType<JsonElement>();
    }
}
