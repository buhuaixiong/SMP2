using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers/{id:int}/draft")]
public sealed class SupplierDraftsController : ApiControllerBase
{
    private readonly ISupplierService _supplierService;

    public SupplierDraftsController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveDraft(int id, [FromBody] SaveDraftRequest request, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        var saved = await _supplierService.SaveDraftAsync(id, request.DraftData!, user.Name ?? user.Id, cancellationToken);
        if (!saved)
        {
            return NotFound("Supplier not found.");
        }

        return Success(null, "Draft saved successfully.");
    }

    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDraft(int id, CancellationToken cancellationToken)
    {
        var draft = await _supplierService.GetDraftAsync(id, cancellationToken);
        return Success(draft);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDraft(int id, CancellationToken cancellationToken)
    {
        var deleted = await _supplierService.DeleteDraftAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound("Draft not found.");
        }

        return NoContent();
    }
}
