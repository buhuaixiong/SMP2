using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

/// <summary>
/// 供应商标签管理控制器
/// </summary>
[ApiController]
[Route("api/suppliers/tags")]
[Authorize]
public sealed class SupplierTagsController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SupplierTagsController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    /// <summary>
    /// 获取所有标签
    /// GET /api/suppliers/tags
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTags(CancellationToken cancellationToken)
    {
        var tags = await _supplierService.GetTagsAsync(cancellationToken);
        return Ok(new { data = tags });
    }

    /// <summary>
    /// 创建标签 (需要Admin权限)
    /// POST /api/suppliers/tags
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.AdminSupplierTags)]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tag = await _supplierService.CreateTagAsync(request, cancellationToken);
            return StatusCode(201, new { data = tag });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    /// <summary>
    /// 更新标签 (需要Admin权限)
    /// PUT /api/suppliers/tags/:id
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdminSupplierTags)]
    public async Task<IActionResult> UpdateTag(int id, [FromBody] UpdateTagRequest request, CancellationToken cancellationToken)
    {
        var tag = await _supplierService.UpdateTagAsync(id, request, cancellationToken);
        if (tag == null)
            return NotFound(new { message = "Tag not found." });

        return Ok(new { data = tag });
    }

    /// <summary>
    /// 删除标签 (需要Admin权限)
    /// DELETE /api/suppliers/tags/:id
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.AdminSupplierTags)]
    public async Task<IActionResult> DeleteTag(int id, CancellationToken cancellationToken)
    {
        var deleted = await _supplierService.DeleteTagAsync(id, cancellationToken);
        if (!deleted)
            return NotFound(new { message = "Tag not found." });

        return NoContent();
    }

    /// <summary>
    /// 获取指定标签下的供应商
    /// GET /api/suppliers/tags/:tagId/suppliers
    /// </summary>
    [HttpGet("{tagId:int}/suppliers")]
    public async Task<IActionResult> GetSuppliersByTag(int tagId, CancellationToken cancellationToken)
    {
        var suppliers = await _supplierService.GetSuppliersByTagAsync(tagId, cancellationToken);
        return Ok(new { data = suppliers });
    }

    /// <summary>
    /// 批量分配标签到供应商 (需要Admin权限)
    /// POST /api/suppliers/tags/:tagId/batch-assign
    /// </summary>
    [HttpPost("{tagId:int}/batch-assign")]
    [Authorize(Policy = Permissions.AdminSupplierTags)]
    public async Task<IActionResult> BatchAssignTag(
        int tagId,
        [FromBody] BatchAssignTagRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SupplierIds == null || request.SupplierIds.Count == 0)
            return BadRequest(new { message = "SupplierIds is required." });

        var result = await _supplierService.BatchAssignTagAsync(tagId, request.SupplierIds, cancellationToken);

        return Ok(new
        {
            message = $"Successfully assigned tag to {result.Added} supplier(s).",
            data = result
        });
    }

    /// <summary>
    /// 批量移除供应商标签 (需要Admin权限)
    /// POST /api/suppliers/tags/:tagId/batch-remove
    /// </summary>
    [HttpPost("{tagId:int}/batch-remove")]
    [Authorize(Policy = Permissions.AdminSupplierTags)]
    public async Task<IActionResult> BatchRemoveTag(
        int tagId,
        [FromBody] BatchAssignTagRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SupplierIds == null || request.SupplierIds.Count == 0)
            return BadRequest(new { message = "SupplierIds is required." });

        var result = await _supplierService.BatchRemoveTagAsync(tagId, request.SupplierIds, cancellationToken);

        return Ok(new
        {
            message = $"Successfully removed tag from {result.Removed} supplier(s).",
            data = result
        });
    }
}
