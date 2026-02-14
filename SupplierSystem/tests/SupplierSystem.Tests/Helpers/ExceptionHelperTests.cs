using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Helpers;
using Xunit;

namespace SupplierSystem.Tests.Helpers;

public class ExceptionHelperTests
{
    [Fact]
    public void TryGetPropertyMap_HandlesDictionaryAndJsonElement()
    {
        var dictionary = new Dictionary<string, object?> { ["message"] = "boom" };

        ExceptionHelper.TryGetPropertyMap(dictionary, out var map).Should().BeTrue();
        map["message"].Should().Be("boom");

        using var document = JsonDocument.Parse("{\"code\":\"E1\"}");
        ExceptionHelper.TryGetPropertyMap(document.RootElement, out var jsonMap).Should().BeTrue();
        jsonMap.TryGetValue("code", out var codeValue).Should().BeTrue();
        codeValue.Should().NotBeNull();
        codeValue!.ToString().Should().Be("E1");
    }

    [Fact]
    public void ExtractMessage_UsesProblemDetails()
    {
        var problem = new ProblemDetails { Title = "Problem title" };

        ExceptionHelper.ExtractMessage(problem).Should().Be("Problem title");
    }

    [Fact]
    public void ExtractMessage_UsesMapValues()
    {
        var payload = new { Message = "payload message" };

        ExceptionHelper.ExtractMessage(payload).Should().Be("payload message");
    }

    [Fact]
    public void ExtractCode_UsesMapValues()
    {
        var payload = new Dictionary<string, object?> { ["errorCode"] = "E_BAD" };

        ExceptionHelper.ExtractCode(payload).Should().Be("E_BAD");
    }

    [Fact]
    public void ExtractDetails_UsesValidationProblemDetails()
    {
        var errors = new Dictionary<string, string[]> { ["field"] = new[] { "error" } };
        var validation = new ValidationProblemDetails(errors);

        var details = ExceptionHelper.ExtractDetails(validation) as IDictionary<string, string[]>;

        details.Should().NotBeNull();
        details!["field"].Should().Contain("error");
    }

    [Fact]
    public void GetDefaultMessageAndCode_ReturnsFallbacks()
    {
        ExceptionHelper.GetDefaultMessage(404).Should().Be("Resource not found");
        ExceptionHelper.GetDefaultCode(404).Should().Be("NOT_FOUND");
        ExceptionHelper.GetDefaultCode(999).Should().Be("REQUEST_FAILED");
    }
}

