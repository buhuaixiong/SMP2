using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/permissions")]
public sealed partial class PermissionsController : NodeControllerBase
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<PermissionsController> logger) : base(environment)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _environment = environment;
        _logger = logger;
    }
}
