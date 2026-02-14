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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupplierSystem"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=SupplierSystemTest;Trusted_Connection=True;",
                ["JwtSettings:Secret"] = "test-secret-for-integration-contracts-123456",
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
}
