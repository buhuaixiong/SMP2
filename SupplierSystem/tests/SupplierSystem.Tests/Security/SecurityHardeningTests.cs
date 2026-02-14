using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using SupplierSystem.Api.Filters;
using SupplierSystem.Application.Models.Common;
using SupplierSystem.Tests.Helpers;
using Xunit;

namespace SupplierSystem.Tests.Security;

public sealed class SecurityHardeningTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SecurityHardeningTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthMe_WithQueryTokenOnly_ShouldReturnMissingTokenByDefault()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/me?token=fake-token");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().Contain("Authentication token is missing.");
    }

    [Fact]
    public async Task ApiResponseFilter_InProduction_ShouldMaskServerErrorDetails()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Production");

        var filter = new ApiResponseFilter(environment.Object);

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var filters = new List<IFilterMetadata>();
        var sourceResult = new ObjectResult(new
        {
            message = "SQL timeout at db node 01",
            details = "stack-and-internal-context"
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };

        var executingContext = new ResultExecutingContext(actionContext, filters, sourceResult, controller: null);

        ResultExecutionDelegate next = () =>
        {
            var executed = new ResultExecutedContext(actionContext, filters, executingContext.Result, controller: null);
            return Task.FromResult(executed);
        };

        await filter.OnResultExecutionAsync(executingContext, next);

        var wrapped = executingContext.Result.Should().BeOfType<ObjectResult>().Subject;
        wrapped.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var payload = wrapped.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        payload.Success.Should().BeFalse();
        payload.Error.Should().Be("An error occurred processing your request.");
        payload.Details.Should().BeNull();
        payload.Stack.Should().BeNull();
    }
}

