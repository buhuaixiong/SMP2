using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    [HttpGet("{id:int}/comparison-print-data")]
    public async Task<IActionResult> GetComparisonPrintData(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAnyPermission(
            user,
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: true, cancellationToken);
        if (rfq == null)
        {
            return NotFound(new { message = "RFQ not found." });
        }

        if (!CanPrintComparison(rfq))
        {
            return BadRequest(new { message = "Only RFQs that are opened and fully reviewed can be printed." });
        }

        var requestedScope = string.Equals(Request.Query["scope"], "all", StringComparison.OrdinalIgnoreCase)
            ? "all"
            : "latest";

        var data = await _rfqComparisonPrintService.BuildAsync(id, user?.Name ?? user?.Id, requestedScope, cancellationToken);
        if (data == null)
        {
            return NotFound(new { message = "RFQ comparison print data not found." });
        }

        return Ok(new { data });
    }

    private static bool CanPrintComparison(SupplierSystem.Domain.Entities.Rfq rfq)
    {
        return string.Equals(rfq.Status, "closed", StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(rfq.ReviewCompletedAt)
               && rfq.SelectedQuoteId.HasValue;
    }
}
