using FluentAssertions;
using SupplierSystem.Api.Helpers;
using Xunit;

namespace SupplierSystem.Tests.Helpers;

public class NodeCaseMapperTests
{
    private sealed class Sample
    {
        public string SampleName { get; set; } = "value";
        public int Count { get; set; } = 2;
    }

    [Fact]
    public void ToCamelCaseDictionary_ConvertsPropertyNames()
    {
        var result = NodeCaseMapper.ToCamelCaseDictionary(new Sample());

        result["sampleName"].Should().Be("value");
        result["count"].Should().Be(2);
    }

    [Fact]
    public void ToSnakeCaseDictionary_ConvertsPropertyNames()
    {
        var result = NodeCaseMapper.ToSnakeCaseDictionary(new Sample());

        result["sample_name"].Should().Be("value");
        result["count"].Should().Be(2);
    }

    [Fact]
    public void ToCaseLists_ProjectItems()
    {
        var items = new[]
        {
            new Sample { SampleName = "one", Count = 1 },
            new Sample { SampleName = "two", Count = 2 }
        };

        var camel = NodeCaseMapper.ToCamelCaseList(items);
        camel.Should().HaveCount(2);
        camel[0]["sampleName"].Should().Be("one");

        var snake = NodeCaseMapper.ToSnakeCaseList(items);
        snake.Should().HaveCount(2);
        snake[1]["sample_name"].Should().Be("two");
    }

    [Fact]
    public void ToCaseHelpers_HandleEdgeCases()
    {
        NodeCaseMapper.ToCamelCase(string.Empty).Should().Be(string.Empty);
        NodeCaseMapper.ToCamelCase("A").Should().Be("a");
        NodeCaseMapper.ToSnakeCase("TestValue").Should().Be("test_value");
    }
}
