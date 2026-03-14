using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using SupplierSystem.Api.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

[CollectionDefinition("EnvironmentVariableDependent", DisableParallelization = true)]
public sealed class EnvironmentVariableDependentCollection
{
}

[Collection("EnvironmentVariableDependent")]
public sealed class UploadPathHelperTests : IDisposable
{
    private readonly string? _originalUploadsPath;
    private readonly string? _originalUploadDir;
    private readonly List<string> _directoriesToCleanup = new();

    public UploadPathHelperTests()
    {
        _originalUploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        _originalUploadDir = Environment.GetEnvironmentVariable("UPLOAD_DIR");
    }

    [Fact]
    public void GetGenericUploadsRoot_ShouldPreferUploadsPath_OverUploadDir()
    {
        var uploadsPath = CreateTempDirectoryPath("uploads-path");
        var uploadDir = CreateTempDirectoryPath("upload-dir");
        var contentRoot = CreateTempDirectoryPath("content-root");

        Environment.SetEnvironmentVariable("UPLOADS_PATH", uploadsPath);
        Environment.SetEnvironmentVariable("UPLOAD_DIR", uploadDir);

        var environment = BuildEnvironment(contentRoot);

        var root = UploadPathHelper.GetGenericUploadsRoot(environment);

        Normalize(root).Should().Be(Normalize(uploadsPath));
        Directory.Exists(root).Should().BeTrue();
    }

    [Fact]
    public void GetGenericUploadsRoot_ShouldFallbackToUploadDir_WhenUploadsPathMissing()
    {
        var uploadDir = CreateTempDirectoryPath("upload-dir");
        var contentRoot = CreateTempDirectoryPath("content-root");

        Environment.SetEnvironmentVariable("UPLOADS_PATH", null);
        Environment.SetEnvironmentVariable("UPLOAD_DIR", uploadDir);

        var environment = BuildEnvironment(contentRoot);

        var root = UploadPathHelper.GetGenericUploadsRoot(environment);

        Normalize(root).Should().Be(Normalize(uploadDir));
        Directory.Exists(root).Should().BeTrue();
    }

    [Fact]
    public void GetGenericUploadsRoot_ShouldFallbackToContentRootUploads_WhenNoEnvConfigured()
    {
        var contentRoot = CreateTempDirectoryPath("content-root");

        Environment.SetEnvironmentVariable("UPLOADS_PATH", null);
        Environment.SetEnvironmentVariable("UPLOAD_DIR", null);

        var environment = BuildEnvironment(contentRoot);

        var root = UploadPathHelper.GetGenericUploadsRoot(environment);

        var expected = Path.Combine(contentRoot, "uploads");
        Normalize(root).Should().Be(Normalize(expected));
        Directory.Exists(root).Should().BeTrue();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("UPLOADS_PATH", _originalUploadsPath);
        Environment.SetEnvironmentVariable("UPLOAD_DIR", _originalUploadDir);

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

    private static IWebHostEnvironment BuildEnvironment(string contentRoot)
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.SetupGet(x => x.ContentRootPath).Returns(contentRoot);
        return mock.Object;
    }

    private string CreateTempDirectoryPath(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"supplier-system-{prefix}-{Guid.NewGuid():N}");
        _directoriesToCleanup.Add(path);
        return path;
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
