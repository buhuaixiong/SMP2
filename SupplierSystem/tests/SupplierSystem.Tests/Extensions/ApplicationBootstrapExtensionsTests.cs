using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using SupplierSystem.Api.Extensions;
using Xunit;

namespace SupplierSystem.Tests.Extensions;

[Collection("EnvironmentVariableDependent")]
public sealed class ApplicationBootstrapExtensionsTests : IDisposable
{
    private const string TestJwtSecret = "test-secret-for-bootstrap-extensions-123456";

    private readonly string? _originalUploadsPath;
    private readonly string? _originalUploadDir;
    private readonly string? _originalJwtSecret;
    private readonly string? _originalNodeJwtSecret;
    private readonly List<string> _directoriesToCleanup = new();

    public ApplicationBootstrapExtensionsTests()
    {
        _originalUploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        _originalUploadDir = Environment.GetEnvironmentVariable("UPLOAD_DIR");
        _originalJwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        _originalNodeJwtSecret = Environment.GetEnvironmentVariable("NODE_JWT_SECRET");

        // Ensure JWT auth registration doesn't fail during builder.AddSupplierSystemApi().
        Environment.SetEnvironmentVariable("JWT_SECRET", TestJwtSecret);
        Environment.SetEnvironmentVariable("NODE_JWT_SECRET", TestJwtSecret);
    }

    [Fact]
    public async Task UseSupplierSystemApi_ShouldSeedUploadsPathFromUploadDir_WhenUploadsPathMissing()
    {
        var contentRoot = CreateTempDirectoryPath("content-root");
        var uploadDir = CreateTempDirectoryPath("upload-dir");

        Environment.SetEnvironmentVariable("UPLOADS_PATH", null);
        Environment.SetEnvironmentVariable("UPLOAD_DIR", uploadDir);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            EnvironmentName = "Development",
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SupplierSystem"] =
                "Server=(localdb)\\MSSQLLocalDB;Database=SupplierSystemTest;Trusted_Connection=True;",
            ["JwtSettings:Secret"] = TestJwtSecret,
            ["NodeJwt:Secret"] = TestJwtSecret,
            ["Scheduler:Enabled"] = "false",
        });

        builder.ConfigureApiHost();
        builder.AddSupplierSystemApi();

        var app = builder.Build();
        try
        {
            app.UseSupplierSystemApi();

            var seeded = Environment.GetEnvironmentVariable("UPLOADS_PATH");
            seeded.Should().NotBeNullOrWhiteSpace();
            Normalize(seeded!).Should().Be(Normalize(uploadDir));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("UPLOADS_PATH", _originalUploadsPath);
        Environment.SetEnvironmentVariable("UPLOAD_DIR", _originalUploadDir);
        Environment.SetEnvironmentVariable("JWT_SECRET", _originalJwtSecret);
        Environment.SetEnvironmentVariable("NODE_JWT_SECRET", _originalNodeJwtSecret);

        foreach (var directory in _directoriesToCleanup)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private string CreateTempDirectoryPath(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"supplier-system-{prefix}-{Guid.NewGuid():N}");
        _directoriesToCleanup.Add(path);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
