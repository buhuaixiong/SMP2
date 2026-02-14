using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services.Rfq;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rfq")]
public sealed class LineItemWorkflowController(
    LineItemWorkflowService lineItemWorkflowService,
    IWebHostEnvironment environment) : NodeControllerBase(environment)
{
    private readonly LineItemWorkflowService _lineItemWorkflowService = lineItemWorkflowService;

    [HttpGet("line-items/pending-approvals")]
    public async Task<IActionResult> GetPendingApprovals(CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var role = user?.Role ?? string.Empty;
            var statusParam = string.IsNullOrWhiteSpace(Request.Query["status"])
                ? "pending"
                : Request.Query["status"].ToString().ToLowerInvariant();

            var items = await _lineItemWorkflowService.GetPendingApprovalsAsync(
                role,
                user!,
                statusParam,
                cancellationToken);

            return Success(items);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("{rfqId:int}/line-items/{lineItemId:int}/submit")]
    public async Task<IActionResult> SubmitLineItem(
        int rfqId,
        int lineItemId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        try
        {
            var selectedQuoteId = JsonHelper.GetInt(body, "selectedQuoteId");
            var user = HttpContext.GetAuthUser();
            var lineItem = await _lineItemWorkflowService.SubmitLineItemAsync(
                rfqId,
                lineItemId,
                selectedQuoteId,
                user!,
                cancellationToken);

            return Success(lineItem, 200, "Line item submitted for approval");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("{rfqId:int}/line-items/{lineItemId:int}/director-approve")]
    public async Task<IActionResult> DirectorApprove(
        int rfqId,
        int lineItemId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = JsonHelper.GetString(body, "decision") ?? string.Empty;
            var comments = JsonHelper.GetString(body, "comments");
            var newQuoteId = JsonHelper.GetInt(body, "newQuoteId");
            var user = HttpContext.GetAuthUser();

            var lineItem = await _lineItemWorkflowService.DirectorApproveAsync(
                rfqId,
                lineItemId,
                decision,
                comments,
                newQuoteId,
                user!,
                cancellationToken);

            return Success(lineItem, 200, $"Line item {decision} by director");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{rfqId:int}/line-items/{lineItemId:int}/history")]
    public async Task<IActionResult> GetApprovalHistory(
        int rfqId,
        int lineItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            var history = await _lineItemWorkflowService.GetApprovalHistoryAsync(lineItemId, cancellationToken);
            return Success(history);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("{rfqId:int}/line-items/{lineItemId:int}/invite-purchasers")]
    public async Task<IActionResult> InvitePurchasers(
        int rfqId,
        int lineItemId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        try
        {
            var purchaserIds = new List<int>();
            if (JsonHelper.TryGetProperty(body, "purchaserIds", out var idsElement) &&
                idsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in idsElement.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var value))
                    {
                        purchaserIds.Add(value);
                    }
                }
            }

            var message = JsonHelper.GetString(body, "message");
            var user = HttpContext.GetAuthUser();

            await _lineItemWorkflowService.InvitePurchasersAsync(
                rfqId,
                lineItemId,
                purchaserIds,
                message,
                user!,
                cancellationToken);

            return Success(new { purchaserIds }, 201, "Purchasers invited successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
