using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/requisitions")]
public sealed partial class RequisitionsController : NodeControllerBase
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly RfqExcelImportService _excelImportService;
    private readonly IWebHostEnvironment _environment;
    private readonly IMemoryCache _cache;
    private readonly AuthSchemaMonitor _schemaMonitor;
    private readonly ILogger<RequisitionsController> _logger;

    public RequisitionsController(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        RfqExcelImportService excelImportService,
        IWebHostEnvironment environment,
        IMemoryCache cache,
        AuthSchemaMonitor schemaMonitor,
        ILogger<RequisitionsController> logger) : base(environment)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _excelImportService = excelImportService;
        _environment = environment;
        _cache = cache;
        _schemaMonitor = schemaMonitor;
        _logger = logger;
    }
}
