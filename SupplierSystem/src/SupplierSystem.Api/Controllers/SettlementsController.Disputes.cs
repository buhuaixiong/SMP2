using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Controllers;

public sealed partial class SettlementsController
{
    [HttpPost("{id:int}/dispute")]
    public async Task<IActionResult> HandleDispute(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_accountant", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var disputeReason = ReadString(body, "dispute_reason", "disputeReason");
        var disputedItems = ReadRawJson(body, "disputed_items", "disputedItems");
        var supportingDocuments = ReadRawJson(body, "supporting_documents", "supportingDocuments");

        var settlement = await _settlementStore.FindDisputableSettlementAsync(id, cancellationToken);

        if (settlement == null)
        {
            return NotFound(new { message = "Settlement not found or dispute is not supported for this type." });
        }

        settlement.DisputeReceived = true;
        settlement.DisputeReason = disputeReason;
        settlement.DisputedItems = disputedItems;
        settlement.SupportingDocuments = supportingDocuments;
        settlement.DisputeProcessorId = TryParseUserId(user);
        settlement.DisputeReceivedAt = DateTimeOffset.UtcNow.ToString("o");
        settlement.Status = "pending_approval";

        await _settlementStore.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(user, "settlement_dispute_handled", settlement.Id.ToString(), new
        {
            dispute_reason = disputeReason,
            disputed_items = disputedItems
        });

        return Ok(new { message = "Settlement dispute recorded." });
    }
}
