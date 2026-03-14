using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers")]
public sealed class SupplierWorkflowController : ApiControllerBase
{
    private readonly ISupplierService _supplierService;

    public SupplierWorkflowController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpPost("preview-approval")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewApproval(
        [FromBody] ApprovalPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var preview = await _supplierService.PreviewApprovalAsync(request, cancellationToken);
        return Success(preview);
    }

    [HttpPost("{id:int}/temp-accounts")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueTempAccount(
        int id,
        [FromBody] IssueTempAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.IssueTempAccountAsync(id, request, cancellationToken);
        return Success(result);
    }

    [HttpPost("{id:int}/finalize-code")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> FinalizeSupplierCode(
        int id,
        [FromBody] FinalizeSupplierCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.FinalizeSupplierCodeAsync(id, request, cancellationToken);
        return Success(result);
    }
}
