using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services.Registrations;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed partial class DashboardController : ControllerBase
{
    private readonly SupplierSystemDbContext _context;
    private readonly SupplierRegistrationService _registrationService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        SupplierSystemDbContext context,
        SupplierRegistrationService registrationService,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _registrationService = registrationService;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard todo items for the current user
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Todo items</returns>
    [HttpGet("todos")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTodos(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var todos = await ResolveTodosAsync(user, cancellationToken);
        return Ok(new { data = todos });
    }

    /// <summary>
    /// Get dashboard statistics based on user role and permissions
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dashboard statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var stats = await ResolveStatsAsync(user, cancellationToken);
        return Ok(new { data = stats });
    }
}
