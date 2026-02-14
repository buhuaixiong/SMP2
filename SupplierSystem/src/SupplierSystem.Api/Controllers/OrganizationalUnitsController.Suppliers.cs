using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Controllers;

public sealed partial class OrganizationalUnitsController
{
    [HttpGet("{id:int}/suppliers")]
    public async Task<IActionResult> GetSuppliers(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var tenantId = HttpContext.GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = "Tenant context is required." });
        }

        var unitExists = await _dbContext.OrganizationalUnits.AsNoTracking()
            .AnyAsync(unit => unit.Id == id && unit.TenantId == tenantId && unit.DeletedAt == null, cancellationToken);
        if (!unitExists)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        return Ok(new { data = Array.Empty<OrgUnitSupplierResponse>() });
    }

    [HttpPost("{id:int}/suppliers")]
    public async Task<IActionResult> AddSuppliers(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var tenantId = HttpContext.GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = "Tenant context is required." });
        }

        if (!TryReadIntList(body, out var supplierIds, "supplierIds", "supplier_ids") || supplierIds.Count == 0)
        {
            return BadRequest(new { message = "Supplier IDs array is required." });
        }

        var unitExists = await _dbContext.OrganizationalUnits.AsNoTracking()
            .AnyAsync(unit => unit.Id == id && unit.TenantId == tenantId && unit.DeletedAt == null, cancellationToken);
        if (!unitExists)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        return Ok(new
        {
            message = "Supplier assignments are not persisted in this environment.",
            data = new { addedCount = supplierIds.Count }
        });
    }

    [HttpDelete("{id:int}/suppliers/{supplierId:int}")]
    public async Task<IActionResult> RemoveSupplier(int id, int supplierId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var tenantId = HttpContext.GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = "Tenant context is required." });
        }

        var unitExists = await _dbContext.OrganizationalUnits.AsNoTracking()
            .AnyAsync(unit => unit.Id == id && unit.TenantId == tenantId && unit.DeletedAt == null, cancellationToken);
        if (!unitExists)
        {
            return NotFound(new { message = "Organizational unit not found." });
        }

        return NoContent();
    }
}
