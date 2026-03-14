using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Services.Reconciliation;
using SupplierSystem.Api.StateMachines;

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

    private readonly ReconciliationStore _reconciliationStore;
    private readonly ReconciliationStateMachine _stateMachine;
    private readonly IWebHostEnvironment _environment;

    public ReconciliationController(
        ReconciliationStore reconciliationStore,
        ReconciliationStateMachine stateMachine,
        IWebHostEnvironment environment)
    {
        _reconciliationStore = reconciliationStore;
        _stateMachine = stateMachine;
        _environment = environment;
    }
}
