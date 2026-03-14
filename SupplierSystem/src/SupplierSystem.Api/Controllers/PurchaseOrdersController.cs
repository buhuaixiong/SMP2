using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services.Rfq;

namespace SupplierSystem.Api.Controllers;

/// <summary>
/// 采购订单管理控制器
/// 处理采购订单的创建、查询、更新、提交和删除操作
/// </summary>
[ApiController]
[Authorize]
[Route("api/rfq")]
public sealed class PurchaseOrdersController(
    PurchaseOrderService purchaseOrderService,
    IWebHostEnvironment environment) : NodeControllerBase(environment)
{
    private readonly PurchaseOrderService _purchaseOrderService = purchaseOrderService;

    /// <summary>
    /// 获取RFQ可用的行项目（按供应商分组）
    /// </summary>
    /// <param name="rfqId">RFQ ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按供应商分组的可用行项目</returns>
    /// <response code="200">成功返回行项目列表</response>
    /// <response code="401">未授权访问</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("{rfqId:int}/purchase-orders/available-items")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAvailableLineItems(int rfqId, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var grouped = await _purchaseOrderService.GetAvailableLineItemsGroupedBySupplierAsync(
                rfqId,
                user!,
                cancellationToken);

            return Success(grouped);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 创建采购订单
    /// </summary>
    /// <param name="rfqId">RFQ ID</param>
    /// <param name="body">创建请求数据，包含supplierId、lineItemIds、description、notes</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的采购订单</returns>
    /// <response code="201">采购订单创建成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("{rfqId:int}/purchase-orders")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreatePo(
        int rfqId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        try
        {
            var supplierId = JsonHelper.GetInt(body, "supplierId") ?? 0;
            var lineItemIds = new List<int>();
            if (JsonHelper.TryGetProperty(body, "lineItemIds", out var lineItemsElement) &&
                lineItemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in lineItemsElement.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var value))
                    {
                        lineItemIds.Add(value);
                    }
                }
            }

            var description = JsonHelper.GetString(body, "description");
            var notes = JsonHelper.GetString(body, "notes");
            var user = HttpContext.GetAuthUser();

            var po = await _purchaseOrderService.CreatePoAsync(
                rfqId,
                supplierId,
                lineItemIds,
                description,
                notes,
                user!,
                cancellationToken);

            return Success(po, 201, "Purchase Order created successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 获取RFQ的采购订单列表
    /// </summary>
    /// <param name="rfqId">RFQ ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>采购订单列表</returns>
    /// <response code="200">成功返回采购订单列表</response>
    /// <response code="401">未授权访问</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("{rfqId:int}/purchase-orders")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListPosForRfq(int rfqId, CancellationToken cancellationToken)
    {
        try
        {
            var pos = await _purchaseOrderService.ListPosForRfqAsync(rfqId, cancellationToken);
            return Success(pos);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 获取采购订单详情
    /// </summary>
    /// <param name="poId">采购订单ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>采购订单详情</returns>
    /// <response code="200">成功返回采购订单详情</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">采购订单不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("purchase-orders/{poId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPo(int poId, CancellationToken cancellationToken)
    {
        try
        {
            var po = await _purchaseOrderService.GetPoAsync(poId, cancellationToken);
            return Success(po);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 更新采购订单
    /// </summary>
    /// <param name="poId">采购订单ID</param>
    /// <param name="body">更新请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的采购订单</returns>
    /// <response code="200">采购订单更新成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">采购订单不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("purchase-orders/{poId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdatePo(
        int poId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        try
        {
            var updateData = new Dictionary<string, object?>();
            if (JsonHelper.TryGetProperty(body, "description", out var description))
            {
                updateData["description"] = description.ValueKind == JsonValueKind.String ? description.GetString() : description.ToString();
            }

            if (JsonHelper.TryGetProperty(body, "notes", out var notes))
            {
                updateData["notes"] = notes.ValueKind == JsonValueKind.String ? notes.GetString() : notes.ToString();
            }

            if (JsonHelper.TryGetProperty(body, "po_file_path", out var filePath))
            {
                updateData["po_file_path"] = filePath.ValueKind == JsonValueKind.String ? filePath.GetString() : filePath.ToString();
            }

            if (JsonHelper.TryGetProperty(body, "po_file_name", out var fileName))
            {
                updateData["po_file_name"] = fileName.ValueKind == JsonValueKind.String ? fileName.GetString() : fileName.ToString();
            }

            if (JsonHelper.TryGetProperty(body, "po_file_size", out var fileSize))
            {
                updateData["po_file_size"] = fileSize.ValueKind == JsonValueKind.Number && fileSize.TryGetInt64(out var size)
                    ? size
                    : fileSize.ToString();
            }

            var user = HttpContext.GetAuthUser();
            var po = await _purchaseOrderService.UpdatePoAsync(
                poId,
                updateData,
                user!,
                cancellationToken);

            return Success(po, 200, "Purchase Order updated successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 提交采购订单
    /// </summary>
    /// <param name="poId">采购订单ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>提交后的采购订单</returns>
    /// <response code="200">采购订单提交成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">采购订单不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("purchase-orders/{poId:int}/submit")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitPo(int poId, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var po = await _purchaseOrderService.SubmitPoAsync(poId, user!, cancellationToken);
            return Success(po, 200, "Purchase Order submitted successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 删除采购订单
    /// </summary>
    /// <param name="poId">采购订单ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    /// <response code="200">采购订单删除成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">采购订单不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpDelete("purchase-orders/{poId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeletePo(int poId, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            await _purchaseOrderService.DeletePoAsync(poId, user!, cancellationToken);
            return Success(null, 200, "Purchase Order deleted successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
