using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SupplierSystem.Api.Services;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("uploads")]
public sealed class UploadsController : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly IWebHostEnvironment _environment;

    public UploadsController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("{**relativePath}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult Download(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return NotFound(new { message = "File not found." });
        }

        if (Path.IsPathRooted(relativePath))
        {
            return BadRequest(new { message = "Invalid file path." });
        }

        var normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return NotFound(new { message = "File not found." });
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                return BadRequest(new { message = "Invalid file path." });
            }
        }

        var uploadsRoot = Path.GetFullPath(UploadPathHelper.GetGenericUploadsRoot(_environment));
        var candidatePath = Path.GetFullPath(Path.Combine(
            uploadsRoot,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar)));

        var rootPrefix = uploadsRoot.EndsWith(Path.DirectorySeparatorChar)
            ? uploadsRoot
            : uploadsRoot + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Invalid file path." });
        }

        if (!System.IO.File.Exists(candidatePath))
        {
            return NotFound(new { message = "File not found." });
        }

        var downloadName = Path.GetFileName(candidatePath);
        if (!ContentTypeProvider.TryGetContentType(downloadName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(candidatePath, contentType, downloadName, enableRangeProcessing: true);
    }
}
