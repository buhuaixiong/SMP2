using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly AuthSchemaMonitor _schemaMonitor;
    private readonly IWebHostEnvironment _environment;

    public HealthController(
        SupplierSystemDbContext dbContext,
        AuthSchemaMonitor schemaMonitor,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _schemaMonitor = schemaMonitor;
        _environment = environment;
    }

    /// <summary>
    /// Basic health check endpoint
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("healthz")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Healthz()
    {
        return Ok(new
        {
            status = "ok",
            timestamp = DateTimeOffset.UtcNow.ToString("o")
        });
    }

    /// <summary>
    /// Readiness check - verifies database and environment connectivity
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Readiness status with component health</returns>
    [HttpGet("readiness")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Readiness(CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, object>
        {
            ["server"] = new { status = "ok", message = "Server is running" },
            ["database"] = new { status = "unknown" },
            ["environment"] = new { status = "unknown" }
        };

        var overallStatus = 200;
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            checks["database"] = canConnect
                ? new { status = "ok", message = "Database connection ok" }
                : new { status = "error", message = "Database connection failed" };
            if (!canConnect)
            {
                overallStatus = 503;
            }
        }
        catch (Exception ex)
        {
            checks["database"] = new { status = "error", message = $"Database error: {ex.Message}" };
            overallStatus = 503;
        }

        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        checks["environment"] = string.IsNullOrWhiteSpace(jwtSecret)
            ? new { status = "warning", message = "Missing JWT_SECRET" }
            : new { status = "ok", message = "Environment variables ok" };

        return StatusCode(overallStatus, new
        {
            status = overallStatus == 200 ? "ready" : "not_ready",
            timestamp = DateTimeOffset.UtcNow.ToString("o"),
            checks
        });
    }

    /// <summary>
    /// Get detailed system status including memory, uptime, and configuration
    /// </summary>
    /// <returns>System status information</returns>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Status()
    {
        var process = Process.GetCurrentProcess();
        var memory = GC.GetTotalMemory(false);

        return Ok(new
        {
            status = "ok",
            timestamp = DateTimeOffset.UtcNow.ToString("o"),
            systemInfo = new
            {
                framework = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                uptimeSeconds = (DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds,
                memoryBytes = memory,
                workingSetBytes = process.WorkingSet64
            },
            config = new
            {
                environment = _environment.EnvironmentName,
                jwtConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JWT_SECRET"))
            }
        });
    }

    /// <summary>
    /// Get detailed health metrics (admin only)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed health metrics</returns>
    [HttpGet("/api/health")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DetailedHealth(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var databaseStats = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            databaseStats["suppliers"] = await _dbContext.Suppliers.CountAsync(cancellationToken);
            databaseStats["users"] = await _dbContext.Users.CountAsync(cancellationToken);
            databaseStats["rfqs"] = await _dbContext.Rfqs.CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            databaseStats["error"] = ex.Message;
        }

        var process = Process.GetCurrentProcess();
        var memory = GC.GetTotalMemory(false);

        return Ok(new
        {
            status = "ok",
            timestamp = now,
            version = "1.0.0",
            systemInfo = new
            {
                framework = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                uptimeSeconds = (DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds,
                memoryBytes = memory,
                workingSetBytes = process.WorkingSet64
            },
            config = new
            {
                environment = _environment.EnvironmentName,
                port = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "not_set",
                logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "info",
                jwtExpiresIn = Environment.GetEnvironmentVariable("JWT_EXPIRES_IN") ?? "8h",
                smtpConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMTP_USER")) &&
                    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMTP_PASSWORD")),
                clientOrigin = Environment.GetEnvironmentVariable("CLIENT_ORIGIN") ?? "not_set"
            },
            database = databaseStats
        });
    }

    /// <summary>
    /// Get authentication schema version and stats
    /// </summary>
    /// <returns>Auth schema information</returns>
    [HttpGet("/api/health/auth-schema")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult AuthSchema()
    {
        return Ok(new
        {
            currentSchemaVersion = AuthSchemaMonitor.CurrentSchemaVersion,
            stats = _schemaMonitor.GetStats(),
            timestamp = DateTimeOffset.UtcNow.ToString("o")
        });
    }

    private static IActionResult? RequireAdmin(AuthUser? user)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!granted.Contains(Permissions.AdminSystemConfig))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }
}

[ApiController]
[AllowAnonymous]
public sealed class DocsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public DocsController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// API documentation endpoint (development only)
    /// </summary>
    /// <returns>API documentation HTML page</returns>
    [HttpGet("/api-docs")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetDocs()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound(new { message = "API docs are disabled." });
        }

        var html = """
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><title>API Docs</title></head>
            <body>
              <h1>API Documentation</h1>
              <p>OpenAPI JSON: <a href="/openapi/v1.json">/openapi/v1.json</a></p>
            </body>
            </html>
            """;
        return Content(html, "text/html");
    }

    /// <summary>
    /// Redirect to OpenAPI JSON spec (development only)
    /// </summary>
    /// <returns>Redirect to OpenAPI spec</returns>
    [HttpGet("/api-docs.json")]
    [ProducesResponseType(typeof(StatusCodeResult), StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetDocsJson()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound(new { message = "API docs are disabled." });
        }

        return Redirect("/openapi/v1.json");
    }
}
