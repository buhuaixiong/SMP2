using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupplierSystem.Api.Services.Startup;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Security;

public sealed class AdminBootstrapStartupGuardTests
{
    [Fact]
    public async Task EnsureInitializedAsync_ProductionWithoutAdmin_ShouldThrow()
    {
        await using var db = CreateDbContext();
        var guard = CreateGuard(db, environmentName: "Production");

        var action = () => guard.EnsureInitializedAsync();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no active admin account*");
    }

    [Fact]
    public async Task EnsureInitializedAsync_ProductionWithActiveAdmin_ShouldPass()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Id = "admin-1",
            Username = "admin",
            Name = "System Admin",
            Role = "admin",
            Password = "hash",
            Status = "active",
            AuthVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o"),
        });
        await db.SaveChangesAsync();

        var guard = CreateGuard(db, environmentName: "Production");

        var action = () => guard.EnsureInitializedAsync();

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureInitializedAsync_DevelopmentWithoutAdmin_ShouldSkipByDefault()
    {
        await using var db = CreateDbContext();
        var guard = CreateGuard(db, environmentName: "Development");

        var action = () => guard.EnsureInitializedAsync();

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureInitializedAsync_DevelopmentWithoutAdmin_WhenEnabledByConfig_ShouldThrow()
    {
        await using var db = CreateDbContext();
        var guard = CreateGuard(
            db,
            environmentName: "Development",
            configValues: new Dictionary<string, string?>
            {
                ["StartupGuard:RequireInitializedAdmin"] = "true"
            });

        var action = () => guard.EnsureInitializedAsync();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no active admin account*");
    }

    [Fact]
    public void Program_ShouldInvokeAdminBootstrapStartupGuard()
    {
        var programPath = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Program.cs");
        var content = File.ReadAllText(programPath);

        content.Should().Contain("AddScoped<AdminBootstrapStartupGuard>()");
        content.Should().Contain("await EnsureAdminBootstrapInitializedAsync(app);");
    }

    [Fact]
    public void ActiveAdminPredicate_ShouldBeTranslatableBySqlServerProvider()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SupplierSystemGuardTranslation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var db = new SupplierSystemDbContext(options);

        var action = () => db.Users
            .AsNoTracking()
            .Where(
                user =>
                    user.Role != null &&
                    user.Role.ToLower() == "admin" &&
                    (user.Status == null ||
                     (user.Status.ToLower() != "deleted" && user.Status.ToLower() != "frozen")))
            .ToQueryString();

        action.Should().NotThrow();
    }

    private static SupplierSystemDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase($"admin-bootstrap-guard-{Guid.NewGuid():N}")
            .Options;

        return new SupplierSystemDbContext(options);
    }

    private static AdminBootstrapStartupGuard CreateGuard(
        SupplierSystemDbContext db,
        string environmentName,
        IReadOnlyDictionary<string, string?>? configValues = null)
    {
        var env = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        env.SetupGet(x => x.EnvironmentName).Returns(environmentName);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues ?? new Dictionary<string, string?>())
            .Build();

        return new AdminBootstrapStartupGuard(
            db,
            env.Object,
            configuration,
            NullLogger<AdminBootstrapStartupGuard>.Instance);
    }

    private static string GetSupplierSystemRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "SupplierSystem.sln");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate SupplierSystem root directory.");
    }
}


