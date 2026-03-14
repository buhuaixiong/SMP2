using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupplierSystem.Api.Extensions;
using Xunit;

namespace SupplierSystem.Tests.Extensions;

public class AuthenticationExtensionsTests
{
    [Fact]
    public void AddJwtAuthentication_WithEnvAndConfiguredSecret_ShouldPrioritizeEnvSecret()
    {
        var originalSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        const string configuredSecret = "configured-secret-value-12345678901234567890";
        const string envSecret = "env-secret-value-123456789012345678901234";

        try
        {
            Environment.SetEnvironmentVariable("JWT_SECRET", envSecret);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:Secret"] = configuredSecret,
                    ["JwtSettings:Issuer"] = "supplier-system",
                    ["JwtSettings:Audience"] = "supplier-system"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddJwtAuthentication(configuration);

            using var provider = services.BuildServiceProvider();
            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
            var options = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

            var signingKey = options.TokenValidationParameters.IssuerSigningKey.Should().BeOfType<SymmetricSecurityKey>().Subject;
            var resolvedSecret = Encoding.UTF8.GetString(signingKey.Key);

            resolvedSecret.Should().Be(envSecret);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JWT_SECRET", originalSecret);
        }
    }
}
