using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/permissions")]
public sealed partial class PermissionsController : NodeControllerBase
{
    private readonly PermissionsDataService _permissionsDataService;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        PermissionsDataService permissionsDataService,
        IAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<PermissionsController> logger) : base(environment)
    {
        _permissionsDataService = permissionsDataService;
        _auditService = auditService;
        _environment = environment;
        _logger = logger;
    }
}
