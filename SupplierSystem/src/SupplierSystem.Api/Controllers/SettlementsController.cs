using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Services.Settlements;
using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settlements")]
public sealed partial class SettlementsController : ControllerBase
{
    private static readonly string[] AccountantStatuses =
    {
        "draft",
        "pending_approval",
        "approved",
    };

    private readonly SettlementStore _settlementStore;
    private readonly IAuditService _auditService;
    private readonly ILogger<SettlementsController> _logger;

    public SettlementsController(
        SettlementStore settlementStore,
        IAuditService auditService,
        ILogger<SettlementsController> logger)
    {
        _settlementStore = settlementStore;
        _auditService = auditService;
        _logger = logger;
    }
}
