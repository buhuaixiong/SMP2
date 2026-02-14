using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reconciliation")]
public sealed partial class ReconciliationController : ApiControllerBase
{
    private static readonly string[] StaffPermissions =
    [
        Application.Security.Permissions.FinanceAccountantReconciliation,
        Application.Security.Permissions.FinanceDirectorReconciliation
    ];

    private static readonly string[] SupplierPermissions =
    [
        Application.Security.Permissions.SupplierReconciliationView,
        Application.Security.Permissions.SupplierReconciliationUpload
    ];

    private readonly SupplierSystemDbContext _dbContext;
    private readonly ReconciliationStateMachine _stateMachine;
    private readonly IWebHostEnvironment _environment;

    public ReconciliationController(
        SupplierSystemDbContext dbContext,
        ReconciliationStateMachine stateMachine,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _stateMachine = stateMachine;
        _environment = environment;
    }
}
