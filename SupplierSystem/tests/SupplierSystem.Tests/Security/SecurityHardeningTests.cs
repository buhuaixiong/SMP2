using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SupplierSystem.Api.Filters;
using SupplierSystem.Application.Models.Common;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
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
    public async Task AuthMe_WithQueryTokenOnly_ShouldStillReturnMissingToken_WhenEnvFlagEnabled()
    {
        var original = Environment.GetEnvironmentVariable("AUTH_ALLOW_QUERY_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("AUTH_ALLOW_QUERY_TOKEN", "1");
            using var client = _factory.CreateClient();

            using var response = await client.GetAsync("/api/auth/me?token=fake-token");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            body.Should().Contain("Authentication token is missing.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH_ALLOW_QUERY_TOKEN", original);
        }
    }

    [Fact]
    public async Task Uploads_Path_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/uploads/rfq-attachments/non-existent.bin");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SecureDownload_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent(
            "{\"token\":\"fake-token\",\"type\":\"supplier_file\",\"id\":\"1\"}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/files/secure-download", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void LegacyToken_ShouldValidateWithTokenParameters()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["Auth:LegacyIssuerAudienceCompatibilityDeadlineUtc"] = "2099-01-01T00:00:00Z",
        });

        var optionsMonitor = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var options = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
        var handler = new JwtSecurityTokenHandler();

        try
        {
            handler.ValidateToken(CreateLegacyToken("legacy-window-user"), options.TokenValidationParameters, out _);
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(ex.ToString());
        }
    }

    [Fact]
    public async Task LegacyToken_OnTokenValidated_ShouldSucceed()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["Auth:LegacyIssuerAudienceCompatibilityDeadlineUtc"] = "2099-01-01T00:00:00Z",
        });
        await SeedUserAsync(factory, "legacy-window-user");

        var optionsMonitor = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var options = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
        var handler = new JwtSecurityTokenHandler();
        var token = CreateLegacyToken("legacy-window-user");
        var principal = handler.ValidateToken(token, options.TokenValidationParameters, out var validatedToken);

        using var scope = factory.Services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Items["AuthToken"] = token;

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, JwtBearerDefaults.AuthenticationScheme, typeof(JwtBearerHandler));
        var context = new TokenValidatedContext(httpContext, scheme, options)
        {
            Principal = principal,
            SecurityToken = validatedToken,
        };

        await options.Events.OnTokenValidated(context);

        if (context.Result?.Failure != null)
        {
            throw new Xunit.Sdk.XunitException(context.Result.Failure.ToString());
        }
    }

    [Fact]
    public void AuthCompatibilityDeadlineOverride_ShouldApplyToFactoryConfiguration()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["Auth:LegacyIssuerAudienceCompatibilityDeadlineUtc"] = "2099-01-01T00:00:00Z",
        });

        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        configuration["Auth:LegacyIssuerAudienceCompatibilityDeadlineUtc"].Should().Be("2099-01-01T00:00:00Z");
    }

    [Fact]
    public async Task AuthMe_WithLegacyTokenInsideCompatibilityWindow_ShouldSucceed()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["Auth:LegacyIssuerAudienceCompatibilityDeadlineUtc"] = "2099-01-01T00:00:00Z",
        });
        await SeedUserAsync(factory, "legacy-window-user");

        var token = CreateLegacyToken("legacy-window-user");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("legacy-window-user");
    }

    [Fact]
    public async Task AuthMe_WithLegacyTokenAfterCompatibilityDeadline_ShouldReturnUnauthorized()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["Auth:LegacyIssuerAudienceCompatibilityDeadlineUtc"] = "2000-01-01T00:00:00Z",
        });
        await SeedUserAsync(factory, "legacy-expired-user");

        var token = CreateLegacyToken("legacy-expired-user");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().Contain("Invalid or expired authentication token.");
    }

    [Fact]
    public async Task FlaggedSupplier_ShouldBeBlockedFromProtectedApiUntilPasswordChanged()
    {
        await SeedUserAsync(_factory, "supplier-must-change", role: "supplier", mustChangePassword: true, supplierId: 123);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateStandardToken("supplier-must-change", role: "supplier", supplierId: 123));

        using var response = await client.GetAsync("/api/rfq-workflow/categories");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("PASSWORD_CHANGE_REQUIRED");
    }

    [Fact]
    public async Task FlaggedSupplier_ShouldStillAccessAuthMe()
    {
        await SeedUserAsync(_factory, "supplier-auth-me", role: "supplier", mustChangePassword: true, supplierId: 124);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateStandardToken("supplier-auth-me", role: "supplier", supplierId: 124));

        using var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("supplier-auth-me");
        body.Should().MatchRegex("MustChangePassword|mustChangePassword");
    }

    [Fact]
    public async Task FlaggedSupplier_ShouldStillReachChangePasswordEndpoint()
    {
        await SeedUserAsync(_factory, "supplier-change-password", role: "supplier", mustChangePassword: true, supplierId: 125);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateStandardToken("supplier-change-password", role: "supplier", supplierId: 125));

        using var content = new StringContent(
            "{\"currentPassword\":\"wrong\",\"newPassword\":\"new-password-123\"}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/auth/change-password", content);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        body.Should().NotContain("PASSWORD_CHANGE_REQUIRED");
    }

    [Fact]
    public async Task LegacyRfqContract_ShouldExposeDeprecationHeaders()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["ApiContracts:RfqLegacy:Enabled"] = "true",
            ["ApiContracts:RfqLegacy:SunsetUtc"] = "2026-04-30T00:00:00Z",
            ["ApiContracts:RfqLegacy:SuccessorBasePath"] = "/api/rfq-workflow",
        });
        await SeedUserAsync(factory, "rfq-legacy-header-user");

        var token = CreateStandardToken("rfq-legacy-header-user");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/rfq/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().Contain(header => header.Key == "Deprecation");
        response.Headers.Should().Contain(header => header.Key == "Sunset");
        response.Headers.Should().Contain(header => header.Key == "Link");
    }

    [Fact]
    public async Task RfqWorkflowContract_ShouldNotExposeLegacyDeprecationHeaders()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["ApiContracts:RfqLegacy:Enabled"] = "true",
            ["ApiContracts:RfqLegacy:SunsetUtc"] = "2026-04-30T00:00:00Z",
            ["ApiContracts:RfqLegacy:SuccessorBasePath"] = "/api/rfq-workflow",
        });
        await SeedUserAsync(factory, "rfq-workflow-header-user");

        var token = CreateStandardToken("rfq-workflow-header-user");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/rfq-workflow/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().NotContain(header => header.Key == "Deprecation");
        response.Headers.Should().NotContain(header => header.Key == "Sunset");
    }

    [Fact]
    public async Task LegacyRfqCategories_ShouldBeForwardedToWorkflowContract()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["ApiContracts:RfqLegacy:Enabled"] = "true",
            ["ApiContracts:RfqLegacy:ForwardToWorkflowEnabled"] = "true",
        });
        await SeedUserAsync(factory, "rfq-forward-user");

        var token = CreateStandardToken("rfq-forward-user");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/rfq/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().Contain(header => header.Key == "X-Legacy-Contract-Forwarded-To");
    }

    [Fact]
    public async Task LegacyRfqQuoteById_ShouldNotBeForwardedToWorkflowContract()
    {
        using var factory = WithAuthOverrides(new Dictionary<string, string?>
        {
            ["ApiContracts:RfqLegacy:Enabled"] = "true",
            ["ApiContracts:RfqLegacy:ForwardToWorkflowEnabled"] = "true",
        });
        await SeedUserAsync(factory, "rfq-no-forward-user");

        var token = CreateStandardToken("rfq-no-forward-user");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/rfq/quotes/1");

        response.Headers.Should().NotContain(header => header.Key == "X-Legacy-Contract-Forwarded-To");
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

    private static string CreateLegacyToken(string userId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("authVersion", "1"),
            new("role", "admin"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-for-integration-contracts-123456"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateStandardToken(string userId, string role = "admin", int? supplierId = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("authVersion", "1"),
            new("role", role),
        };

        if (supplierId.HasValue)
        {
            claims.Add(new Claim("supplierId", supplierId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-for-integration-contracts-123456"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "supplier-system",
            audience: "supplier-system",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task SeedUserAsync(
        WebApplicationFactory<Program> factory,
        string userId,
        string role = "admin",
        bool mustChangePassword = false,
        int? supplierId = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupplierSystemDbContext>();

        var existing = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (existing != null)
        {
            return;
        }

        await dbContext.Users.AddAsync(new User
        {
            Id = userId,
            Name = "Legacy User",
            Username = userId,
            Role = role,
            Password = "test",
            Status = "active",
            AuthVersion = 1,
            SupplierId = supplierId,
            MustChangePassword = mustChangePassword,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o"),
        });
        await dbContext.SaveChangesAsync();
    }

    private WebApplicationFactory<Program> WithAuthOverrides(IReadOnlyDictionary<string, string?> overrides)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(overrides);
            });
        });
    }
}
