using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Tests.Helpers;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestJwtSecret = "test-secret-for-integration-contracts-123456";
    private readonly string? _originalJwtSecret;
    private readonly string? _originalNodeJwtSecret;

    public TestWebApplicationFactory()
    {
        _originalJwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        _originalNodeJwtSecret = Environment.GetEnvironmentVariable("NODE_JWT_SECRET");

        // Keep tests hermetic even if machine/session env vars are set.
        Environment.SetEnvironmentVariable("JWT_SECRET", TestJwtSecret);
        Environment.SetEnvironmentVariable("NODE_JWT_SECRET", TestJwtSecret);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupplierSystem"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=SupplierSystemTest;Trusted_Connection=True;",
                ["JwtSettings:Secret"] = TestJwtSecret,
                ["Scheduler:Enabled"] = "false",
            };

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupplierSystemDbContext>>();
            services.RemoveAll<DbContextOptions>();

            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<SupplierSystemDbContext>(options =>
                options.UseInMemoryDatabase("SupplierSystemContractTests")
                    .UseInternalServiceProvider(inMemoryProvider));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Environment.SetEnvironmentVariable("JWT_SECRET", _originalJwtSecret);
            Environment.SetEnvironmentVariable("NODE_JWT_SECRET", _originalNodeJwtSecret);
        }

        base.Dispose(disposing);
    }
}
