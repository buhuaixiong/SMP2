using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers/import")]
public sealed class SupplierImportController : ApiControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly ILogger<SupplierImportController> _logger;

    public SupplierImportController(ISupplierService supplierService, ILogger<SupplierImportController> logger)
    {
        _supplierService = supplierService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AdminRoleManage)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ImportSuppliers(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Excel file is required.");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (extension != ".xls" && extension != ".xlsx")
        {
            return BadRequest("Only Excel files (.xls, .xlsx) are supported.");
        }

        if (file.Length > 8 * 1024 * 1024)
        {
            return BadRequest("File size exceeds the 8MB limit.");
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            var fileContent = memoryStream.ToArray();

            var result = await _supplierService.ImportSuppliersFromExcelAsync(
                fileContent,
                file.FileName,
                user.Name ?? user.Id,
                CancellationToken.None);

            return Success(result);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Supplier import timed out while processing Excel file");
            return StatusCode(StatusCodes.Status408RequestTimeout, new { message = "Import request timed out. Please retry with a smaller file or try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import suppliers from Excel");
            return BadRequest(ex.Message);
        }
    }
}
