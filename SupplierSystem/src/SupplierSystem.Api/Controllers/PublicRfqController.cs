using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Exceptions;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public")]
public sealed class PublicRfqController : ControllerBase
{
    private readonly PublicRfqService _rfqService;

    public PublicRfqController(PublicRfqService rfqService)
    {
        _rfqService = rfqService;
    }

    [HttpGet("rfq-preview/{token}")]
    public async Task<IActionResult> GetRfqPreview(string token, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _rfqService.GetPreviewAsync(token, cancellationToken);
            return Ok(new
            {
                success = true,
                isRegistered = result.IsRegistered,
                rfqPreview = new
                {
                    id = result.RfqPreview.Id,
                    title = result.RfqPreview.Title,
                    description = result.RfqPreview.Description,
                    deliveryPeriod = result.RfqPreview.DeliveryPeriod,
                    budgetAmount = result.RfqPreview.BudgetAmount,
                    currency = result.RfqPreview.Currency,
                    validUntil = result.RfqPreview.ValidUntil,
                    isExpired = result.RfqPreview.IsExpired,
                    inviterName = result.RfqPreview.InviterName,
                },
                recipientEmail = result.RecipientEmail,
                supplierInfo = result.SupplierInfo == null
                    ? null
                    : new
                    {
                        companyName = result.SupplierInfo.CompanyName,
                        supplierId = result.SupplierInfo.SupplierId,
                    },
                message = result.Message,
            });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpGet("auto-login/{token}")]
    public async Task<IActionResult> AutoLogin(string token, CancellationToken cancellationToken)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();
            var result = await _rfqService.AutoLoginAsync(token, ip, userAgent, cancellationToken);

            return Ok(new
            {
                success = true,
                token = result.Token,
                user = new
                {
                    id = result.User.Id,
                    name = result.User.Name,
                    role = result.User.Role,
                    supplierId = result.User.SupplierId,
                },
                redirectTo = $"/rfq/{result.RfqId}",
                message = "Auto login successful.",
            });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }
}
