using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RequisitionsController
{
    [HttpPost("import-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public IActionResult ImportExcel([FromForm] ImportExcelRequest request)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.DepartmentRequisitionManage,
            Permissions.PurchaserRfqTarget);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { error = "Please upload an Excel file." });
        }

        var sheetName = string.IsNullOrWhiteSpace(request.SheetName) ? "PRBuyer" : request.SheetName.Trim();
        var headerRow = request.HeaderRow ?? 15;

        try
        {
            using var stream = request.File.OpenReadStream();
            var result = _excelImportService.Parse(stream, sheetName, headerRow);
            return Success(result, 200, $"Imported {result.Requirements.Count} items.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            var details = _environment.IsDevelopment() ? ex.Message : null;
            return StatusCode(500, new { message = "Failed to import Excel file.", details });
        }
    }
}
