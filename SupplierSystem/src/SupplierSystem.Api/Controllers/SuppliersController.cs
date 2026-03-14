using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Common;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController : ApiControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSuppliers(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? region,
        [FromQuery] string? stage,
        [FromQuery] string? importance,
        [FromQuery] string? query,
        [FromQuery] string? tag,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        [FromQuery] bool? forRfq,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        var result = await _supplierService.ListSuppliersAsync(new SupplierListQuery
        {
            Status = status,
            Category = category,
            Region = region,
            Stage = stage,
            Importance = importance,
            Query = query,
            Tag = tag,
            Limit = limit,
            Offset = offset,
            ForRfq = forRfq ?? false,
        }, user, cancellationToken);

        return Success(result);
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatsOverview(CancellationToken cancellationToken)
    {
        var stats = await _supplierService.GetStatsOverviewAsync(cancellationToken);
        return Success(stats);
    }

    [HttpGet("benchmarks")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBenchmarks(CancellationToken cancellationToken)
    {
        var benchmarks = await _supplierService.GetBenchmarksAsync(cancellationToken);
        return Success(benchmarks);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.PurchaserSupplierCreate)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        try
        {
            var supplier = await _supplierService.CreateSupplierAsync(request, user.Name ?? user.Id, cancellationToken);
            return Created(supplier);
        }
        catch (ServiceException ex)
        {
            return StatusCode(ex.StatusCode, new ApiErrorResponse { Error = ex.Message, Code = ex.ErrorCode, Details = ex.Details });
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);
        if (supplier == null)
        {
            return NotFound("Supplier not found.");
        }

        return Success(supplier);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplier(
        int id,
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.UpdateSupplierAsync(id, request, cancellationToken);
        if (supplier == null)
        {
            return NotFound("Supplier not found.");
        }

        return Success(supplier);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplierStatus(
        int id,
        [FromBody] UpdateSupplierStatusRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.UpdateSupplierStatusAsync(id, request, cancellationToken);
        if (supplier == null)
        {
            return NotFound("Supplier not found.");
        }

        return Success(supplier);
    }

    [HttpPut("{id:int}/approve")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveSupplier(
        int id,
        [FromBody] ApproveSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        var supplier = await _supplierService.ApproveSupplierAsync(id, request, user.Name ?? user.Id, cancellationToken);
        if (supplier == null)
        {
            return NotFound("Supplier not found.");
        }

        return Success(supplier);
    }

    [HttpPut("{id:int}/tags")]
    [Authorize(Policy = Permissions.AdminSupplierTags)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSupplierTags(
        int id,
        [FromBody] UpdateSupplierTagsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Tags == null)
        {
            return BadRequest("Tags is required.");
        }

        var tags = await _supplierService.UpdateSupplierTagsAsync(id, request.Tags, cancellationToken);
        return Success(tags);
    }

    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        int id,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var history = await _supplierService.GetHistoryAsync(id, limit, offset, cancellationToken);
        return Success(history);
    }
}
