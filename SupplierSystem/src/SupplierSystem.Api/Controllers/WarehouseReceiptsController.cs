using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/warehouse-receipts")]
public sealed partial class WarehouseReceiptsController : ApiControllerBase
{
    private static readonly string[] StaffPermissions =
    [
        Permissions.FinanceAccountantReconciliation,
        Permissions.FinanceDirectorReconciliation
    ];

    private static readonly string[] SupplierPermissions =
    [
        Permissions.SupplierReconciliationView,
        Permissions.SupplierReconciliationUpload
    ];

    private readonly SupplierSystemDbContext _dbContext;

    public WarehouseReceiptsController(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
