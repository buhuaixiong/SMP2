using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;
/// <summary>
/// RFQ（询价单）管理控制器
/// 处理RFQ的创建、发布、报价、评审等完整生命周期
/// </summary>
[ApiController]
[Authorize]
[Route("api/rfq")]
[ServiceFilter(typeof(LegacyContractDeprecationFilter))]
public sealed class RfqController : NodeControllerBase
{
    private readonly RfqService _rfqService;
    private readonly RfqExcelImportService _rfqExcelImportService;
    private readonly IWebHostEnvironment _environment;

    public RfqController(
        RfqService rfqService,
        RfqExcelImportService rfqExcelImportService,
        IWebHostEnvironment environment) : base(environment)
    {
        _rfqService = rfqService;
        _rfqExcelImportService = rfqExcelImportService;
        _environment = environment;
    }

    /// <summary>
    /// 获取RFQ分类列表
    /// </summary>
    /// <returns>分类字典</returns>
    /// <response code="200">成功返回分类列表</response>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetRfqCategories()
    {
        var categories = new Dictionary<string, object?>
        {
            ["equipment"] = new Dictionary<string, object?>
            {
                ["label"] = "Equipment",
                ["subcategories"] = new Dictionary<string, string>
                {
                    ["standard"] = "Standard Equipment",
                    ["non_standard"] = "Non-Standard Equipment",
                },
            },
            ["auxiliary_materials"] = new Dictionary<string, object?>
            {
                ["label"] = "Auxiliary Materials",
                ["subcategories"] = new Dictionary<string, string>
                {
                    ["labor_protection"] = "Labor Protection Supplies",
                    ["office_supplies"] = "Office Supplies",
                    ["production_supplies"] = "Production Supplies",
                    ["accessories"] = "Accessories",
                    ["others"] = "Others",
                },
            },
        };

        return Success(categories);
    }

    /// <summary>
    /// 创建新的RFQ
    /// </summary>
    /// <param name="body">RFQ创建请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的RFQ</returns>
    /// <response code="201">RFQ创建成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost]
    [RfqValidationFilter(RfqValidationScenario.Create)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateRfq([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = body.Deserialize<CreateRfqRequest>(CreateJsonOptions()) ?? new CreateRfqRequest();

        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqService.CreateAsync(request, user!, cancellationToken);
            return SendCreated(rfq, "RFQ created successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 从Excel文件导入PR数据创建RFQ
    /// </summary>
    /// <param name="request">Excel导入请求</param>
    /// <returns>导入结果</returns>
    /// <response code="200">导入成功</response>
    /// <response code="400">文件格式错误或验证失败</response>
    /// <response code="403">无权限执行此操作</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("import-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public IActionResult ImportFromExcel([FromForm] ImportExcelRequest request)
    {
        var permissionResult = RequireAnyPermission(HttpContext.GetAuthUser(), Permissions.PurchaserRfqTarget);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { error = "Please upload an Excel file." });
        }

        var sheetName = string.IsNullOrWhiteSpace(request.SheetName) ? "PRBuyer" : request.SheetName.Trim();
        var headerRow = request.HeaderRow ?? 15;

        try
        {
            using var stream = request.File.OpenReadStream();
            var result = _rfqExcelImportService.Parse(stream, sheetName, headerRow);
            return Success(result, 200, $"成功导入 {result.Requirements.Count} 个物料行");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            var details = _environment.IsDevelopment() ? ex.Message : null;
            return StatusCode(500, new { message = "Failed to import Excel file.", details });
        }
    }

    /// <summary>
    /// 获取RFQ列表（支持分页和筛选）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>RFQ列表和分页信息</returns>
    /// <response code="200">成功返回RFQ列表</response>
    /// <response code="401">未授权访问</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListRfqs(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var page = ParseInt(Request.Query["page"], 1);
        var pageSize = ParseInt(Request.Query["pageSize"], 20);
        page = Math.Max(1, page);
        pageSize = Math.Min(100, Math.Max(1, pageSize));

        var keyword = GetSearchKeyword(Request.Query);

        try
        {
            var (data, total) = await _rfqService.ListAsync(
                Request.Query["status"],
                Request.Query["rfqType"],
                keyword,
                page,
                pageSize,
                user!,
                cancellationToken);

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            var response = new
            {
                data,
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    totalPages,
                    hasNext = page < totalPages,
                    hasPrev = page > 1,
                },
            };

            return Success(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 获取RFQ详情
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>RFQ详细信息</returns>
    /// <response code="200">成功返回RFQ详情</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRfqDetails(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqService.GetDetailsAsync(id, user!, cancellationToken);
            return Success(rfq);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 获取供应商RFQ（供应商视角）
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>供应商RFQ信息</returns>
    /// <response code="200">成功返回RFQ信息</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("supplier/{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSupplierRfq(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqService.GetSupplierRfqAsync(id, user!, cancellationToken);
            return Success(rfq);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 更新RFQ
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="body">更新请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的RFQ</returns>
    /// <response code="200">RFQ更新成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("{id:int}")]
    [RfqValidationFilter(RfqValidationScenario.Update)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRfq(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = body.Deserialize<CreateRfqRequest>(CreateJsonOptions()) ?? new CreateRfqRequest();

        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqService.UpdateAsync(id, request, user!, cancellationToken);
            return Success(rfq, 200, "RFQ updated successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 删除RFQ
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    /// <response code="200">RFQ删除成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteRfq(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            await _rfqService.DeleteAsync(id, user!, cancellationToken);
            return Success(null, 200, "RFQ deleted successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 发布RFQ
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发布后的RFQ</returns>
    /// <response code="200">RFQ发布成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("{id:int}/publish")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PublishRfq(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqService.PublishAsync(id, user!, cancellationToken);
            return Success(rfq, 200, "RFQ published successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 关闭RFQ
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>关闭后的RFQ</returns>
    /// <response code="200">RFQ关闭成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("{id:int}/close")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CloseRfq(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqService.CloseAsync(id, user!, cancellationToken);
            return Success(rfq, 200, "RFQ closed successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 向供应商发送RFQ邀请
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="body">邀请请求数据，包含supplierIds数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>邀请结果</returns>
    /// <response code="200">邀请发送成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("{id:int}/invitations")]
    [RfqValidationFilter(RfqValidationScenario.SendInvitations)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendInvitations(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var supplierIds = new List<int>();
        if (JsonHelper.TryGetProperty(body, "supplierIds", out var supplierElement) &&
            supplierElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in supplierElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var value))
                {
                    supplierIds.Add(value);
                }
            }
        }

        try
        {
            var user = HttpContext.GetAuthUser();
            var result = await _rfqService.SendInvitationsAsync(id, supplierIds, user!, cancellationToken);
            var count = result.TryGetValue("count", out var countObj) ? Convert.ToInt32(countObj, CultureInfo.InvariantCulture) : 0;
            return Success(result, 200, $"{count} invitations sent");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 获取当前供应商的RFQ邀请列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>邀请列表</returns>
    /// <response code="200">成功返回邀请列表</response>
    /// <response code="401">未授权访问或非供应商用户</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("supplier/invitations")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSupplierInvitations(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user?.SupplierId == null)
        {
            return HandleError(new Exception("User is not a supplier"));
        }

        var status = string.IsNullOrWhiteSpace(Request.Query["status"]) ? null : Request.Query["status"].ToString();
        var needsResponse = string.Equals(Request.Query["needsResponse"], "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            var invitations = await _rfqService.GetSupplierInvitationsAsync(
                user.SupplierId.Value,
                status,
                needsResponse,
                cancellationToken);
            return Success(invitations);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private IActionResult? RequireAnyPermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return StatusCode(401, new { message = "Authentication required." });
        }

        if (permissions.Length == 0)
        {
            return null;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.Any(granted.Contains))
        {
            return StatusCode(403, new { message = "Permission denied." });
        }

        return null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string? GetSearchKeyword(IQueryCollection query)
    {
        var candidates = new[] { query["search"].ToString(), query["keyword"].ToString(), query["q"].ToString() };
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }
}
