using FluentAssertions;
using Xunit;

namespace SupplierSystem.Tests.Security;

public sealed class ConfigurationHardeningTests
{
    [Fact]
    public void InitSql_ShouldNotContainKnownDefaultAdminPasswordSeed()
    {
        var initSqlPath = Path.Combine(GetSupplierSystemRoot(), "sql", "init.sql");
        var sql = File.ReadAllText(initSqlPath);

        sql.Should().NotContain("password: admin123");
        sql.Should().NotContain("IF NOT EXISTS (SELECT * FROM users WHERE username = 'admin')");
    }

    [Fact]
    public void LaunchSettings_ShouldNotContainHardcodedDatabasePassword()
    {
        var launchSettingsPath = Path.Combine(
            GetSupplierSystemRoot(),
            "src",
            "SupplierSystem.Api",
            "Properties",
            "launchSettings.json");

        var content = File.ReadAllText(launchSettingsPath);

        content.Should().NotContain("Password=123456");
        content.Should().NotContain("ConnectionStrings__SupplierSystem");
    }

    [Fact]
    public void DeploymentGuide_ShouldNotDocumentDefaultWeakAdminPassword()
    {
        var deployRoot = Directory.GetParent(GetSupplierSystemRoot())?.FullName
            ?? throw new InvalidOperationException("Could not locate deployment root directory.");
        var deploymentDocPath = Path.Combine(deployRoot, "docs", "DEPLOYMENT.md");

        var content = File.ReadAllText(deploymentDocPath);

        content.Should().NotContain("admin123");
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
