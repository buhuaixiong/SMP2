using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Filters;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[NodeResponse]
[Route("api/contracts")]
public sealed partial class ContractsController : ControllerBase
{
    private static readonly string[] StaffContractPermissions =
    [
        Permissions.PurchaserSegmentManage,
        Permissions.ProcurementManagerRfqReview,
        Permissions.ProcurementDirectorRfqApprove,
        Permissions.ProcurementDirectorReportsView,
        Permissions.FinanceAccountantReconciliation,
        Permissions.FinanceDirectorRiskMonitor,
        Permissions.AdminRoleManage,
        Permissions.AdminSupplierTags
    ];

    private static readonly string[] SupplierContractPermissions =
    [
        Permissions.SupplierContractChecklist,
        Permissions.SupplierContractUpload
    ];

    private readonly SupplierSystemDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public ContractsController(SupplierSystemDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }
}
