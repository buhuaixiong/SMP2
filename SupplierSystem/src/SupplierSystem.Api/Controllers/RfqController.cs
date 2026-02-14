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
public sealed class RfqController : NodeControllerBase
{
    private readonly RfqService _rfqService;
    private readonly RfqQuoteService _rfqQuoteService;
    private readonly PrExcelService _prExcelService;
    private readonly RfqExcelImportService _rfqExcelImportService;
    private readonly SupplierSystem.Infrastructure.Data.SupplierSystemDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RfqController> _logger;

    public RfqController(
        RfqService rfqService,
        RfqQuoteService rfqQuoteService,
        PrExcelService prExcelService,
        RfqExcelImportService rfqExcelImportService,
        SupplierSystem.Infrastructure.Data.SupplierSystemDbContext dbContext,
        IWebHostEnvironment environment,
        ILogger<RfqController> logger) : base(environment)
    {
        _rfqService = rfqService;
        _rfqQuoteService = rfqQuoteService;
        _prExcelService = prExcelService;
        _rfqExcelImportService = rfqExcelImportService;
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
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

    /// <summary>
    /// 供应商提交报价
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="body">报价请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的报价</returns>
    /// <response code="201">报价提交成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("{id:int}/quotes")]
    [RfqValidationFilter(RfqValidationScenario.SubmitQuote)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitQuote(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = body.Deserialize<SubmitQuoteRequest>(CreateJsonOptions()) ?? new SubmitQuoteRequest();

        try
        {
            var user = HttpContext.GetAuthUser();
            var quote = await _rfqQuoteService.SubmitQuoteAsync(
                id,
                request,
                user!,
                HttpContext.GetClientIp(),
                cancellationToken);
            return SendCreated(quote, "Quote submitted successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 更新报价
    /// </summary>
    /// <param name="rfqId">RFQ ID</param>
    /// <param name="quoteId">报价ID</param>
    /// <param name="body">更新请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的报价</returns>
    /// <response code="200">报价更新成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">报价不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("{rfqId:int}/quotes/{quoteId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateQuote(int rfqId, int quoteId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = body.Deserialize<SubmitQuoteRequest>(CreateJsonOptions()) ?? new SubmitQuoteRequest();

        try
        {
            var user = HttpContext.GetAuthUser();
            var quote = await _rfqQuoteService.UpdateQuoteAsync(rfqId, quoteId, request, user!, cancellationToken);
            return Success(quote, 200, "Quote updated successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 撤回报价
    /// </summary>
    /// <param name="rfqId">RFQ ID</param>
    /// <param name="quoteId">报价ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>撤回后的报价</returns>
    /// <response code="200">报价撤回成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">报价不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("{rfqId:int}/quotes/{quoteId:int}/withdraw")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> WithdrawQuote(int rfqId, int quoteId, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var quote = await _rfqQuoteService.WithdrawQuoteAsync(rfqId, quoteId, user!, cancellationToken);
            return Success(quote, 200, "Quote withdrawn successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 获取报价详情
    /// </summary>
    /// <param name="quoteId">报价ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>报价详细信息</returns>
    /// <response code="200">成功返回报价详情</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">报价不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("quotes/{quoteId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetQuoteDetails(int quoteId, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var quote = await _rfqQuoteService.GetQuoteDetailsAsync(quoteId, user!, cancellationToken);
            return Success(quote);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 获取RFQ价格对比分析
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>价格对比数据</returns>
    /// <response code="200">成功返回价格对比</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("{id:int}/price-comparison")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ComparePrices(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var comparison = await _rfqQuoteService.ComparePricesAsync(id, user!, cancellationToken);
            return Success(comparison);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 评审RFQ并选择中标供应商
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="body">评审请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评审后的RFQ</returns>
    /// <response code="200">RFQ评审成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("{id:int}/review")]
    [RfqValidationFilter(RfqValidationScenario.Review)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReviewRfq(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = new ReviewRfqRequest
        {
            SelectedQuoteId = JsonHelper.GetInt(body, "selectedQuoteId"),
            Comments = JsonHelper.GetString(body, "comments"),
            ReviewScoresJson = JsonHelper.TryGetProperty(body, "reviewScores", out var scoreElement)
                ? scoreElement.GetRawText()
                : null,
        };

        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqService.ReviewAsync(id, request, user!, cancellationToken);
            return Success(rfq, 200, "RFQ review completed successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// 生成采购申请（PR）Excel文件
    /// </summary>
    /// <param name="id">RFQ ID</param>
    /// <param name="body">生成请求数据，包含lineItemIds数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PR Excel文件</returns>
    /// <response code="200">成功生成PR Excel</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">RFQ或行项目不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("{id:long}/generate-pr-excel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GeneratePrExcel(long id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        if (id <= 0)
        {
            return BadRequest(new
            {
                error = "INVALID_RFQ_ID",
                message = "Invalid RFQ ID provided",
            });
        }

        if (!JsonHelper.TryGetProperty(body, "lineItemIds", out var lineItemIdsElement) ||
            lineItemIdsElement.ValueKind != JsonValueKind.Array ||
            lineItemIdsElement.GetArrayLength() == 0)
        {
            return BadRequest(new
            {
                error = "INVALID_LINE_ITEMS",
                message = "lineItemIds is required and must be a non-empty array",
            });
        }

        var lineItemIds = new List<long>();
        var invalidIds = new List<object?>();
        foreach (var entry in lineItemIdsElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Number || !entry.TryGetInt64(out var value) || value <= 0)
            {
                invalidIds.Add(ReadInvalidValue(entry));
                continue;
            }

            lineItemIds.Add(value);
        }

        if (invalidIds.Count > 0)
        {
            return BadRequest(new
            {
                error = "INVALID_LINE_ITEM_IDS",
                message = "All lineItemIds must be positive integers",
                invalidIds,
            });
        }

        const int maxLineItems = 1000;
        if (lineItemIds.Count > maxLineItems)
        {
            return BadRequest(new
            {
                error = "TOO_MANY_LINE_ITEMS",
                message = $"Cannot generate PR Excel with more than {maxLineItems} line items",
                received = lineItemIds.Count,
            });
        }

        var validationResult = await ValidateLineItemsForExportAsync(id, lineItemIds, cancellationToken);

        var missingQuoteItems = validationResult.InvalidItems
            .Where(item => string.Equals(item.Reason, "NO_SELECTED_QUOTE", StringComparison.Ordinal))
            .ToList();

        if (missingQuoteItems.Count > 0)
        {
            return BadRequest(new
            {
                error = "MISSING_SELECTED_QUOTE",
                message = "Some line items do not have a selected quote",
                details = new
                {
                    missingQuoteItems = missingQuoteItems.Select(item => new
                    {
                        id = item.Id,
                        lineNumber = item.LineNumber,
                    }),
                },
            });
        }

        var statusInvalidItems = validationResult.InvalidItems
            .Where(item => !string.Equals(item.Reason, "NO_SELECTED_QUOTE", StringComparison.Ordinal))
            .ToList();

        if (statusInvalidItems.Count > 0)
        {
            return BadRequest(new
            {
                error = "INVALID_LINE_ITEM_STATUS",
                message = "Some line items are not eligible for export",
                details = new
                {
                    invalidItems = statusInvalidItems.Select(item => new
                    {
                        id = item.Id,
                        lineNumber = item.LineNumber,
                        status = item.Status,
                        quoteStatus = item.QuoteStatus,
                        reason = item.Reason,
                    }),
                },
            });
        }

        if (validationResult.ValidItems.Count == 0)
        {
            return BadRequest(new
            {
                error = "NO_VALID_LINE_ITEMS",
                message = "No valid line items found with approved quotes for the selected items",
            });
        }

        var prNumber = JsonHelper.GetString(body, "prNumber");
        var department = JsonHelper.GetString(body, "department");
        var accountNo = JsonHelper.GetString(body, "accountNo");

        try
        {
            var user = HttpContext.GetAuthUser();
            var result = await _prExcelService.GenerateAsync(
                id,
                validationResult.ValidItems.Select(item => item.Id).ToList(),
                new PrExcelOptions
                {
                    PrNumber = prNumber,
                    Department = department,
                    AccountNo = accountNo,
                },
                new PrExcelUserContext
                {
                    Id = user?.Id ?? string.Empty,
                    Name = user?.Name,
                    Username = user?.Name,
                },
                cancellationToken);

            var buffer = result.Buffer;
            if (buffer == null || buffer.Length == 0)
            {
                return StatusCode(500, new
                {
                    error = "INVALID_BUFFER",
                    message = "Failed to generate valid Excel file",
                });
            }

            const int minFileSize = 100 * 1024;
            const int maxFileSize = 50 * 1024 * 1024;

            if (buffer.Length < minFileSize)
            {
                return StatusCode(500, new
                {
                    error = "FILE_TOO_SMALL",
                    message = "Generated file is smaller than expected. Generation may have failed.",
                    fileSizeKB = (buffer.Length / 1024.0).ToString("F2", CultureInfo.InvariantCulture),
                });
            }

            if (buffer.Length > maxFileSize)
            {
                return StatusCode(500, new
                {
                    error = "FILE_TOO_LARGE",
                    message = "Generated file exceeds maximum size limit",
                    fileSizeKB = (buffer.Length / 1024.0).ToString("F2", CultureInfo.InvariantCulture),
                    maxSizeKB = (maxFileSize / 1024.0).ToString("F2", CultureInfo.InvariantCulture),
                });
            }

            var generationTimeMs = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var finalPrNumber = string.IsNullOrWhiteSpace(result.PrNumber)
                ? $"HZ_PR_{timestamp}"
                : result.PrNumber;
            var filename = $"PR_({finalPrNumber})_{timestamp}.xlsm";

            Response.Headers["Content-Type"] = "application/vnd.ms-excel.sheet.macroEnabled.12";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{filename}\"";
            Response.Headers["Content-Length"] = buffer.Length.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-File-Size-KB"] = (buffer.Length / 1024.0).ToString("F2", CultureInfo.InvariantCulture);
            Response.Headers["X-Generation-Time-Ms"] = generationTimeMs.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-Line-Item-Count"] = result.LineItemCount.ToString(CultureInfo.InvariantCulture);

            // 更新 PR 状态（如果提供了 PR 编号）
            if (!string.IsNullOrWhiteSpace(finalPrNumber))
            {
                var validLineItemIds = validationResult.ValidItems.Select(item => item.Id).ToList();
                await UpdatePrStatusAfterExcelGenerationAsync(id, finalPrNumber, department, user, validLineItemIds, cancellationToken);
            }

            return File(buffer, "application/vnd.ms-excel.sheet.macroEnabled.12");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new
                {
                    error = "RESOURCE_NOT_FOUND",
                    message = ex.Message,
                });
            }

            if (ex.Message.Contains("No line items", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    error = "NO_VALID_LINE_ITEMS",
                    message = "No valid line items found with approved quotes for the selected items",
                });
            }

            if (ex.Message.Contains("template", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(500, new
                {
                    error = "TEMPLATE_ERROR",
                    message = "Excel template is missing or corrupted. Please contact system administrator.",
                });
            }

            var details = _environment.IsDevelopment() ? ex.Message : null;
            return StatusCode(500, new
            {
                error = "GENERATION_FAILED",
                message = "Failed to generate PR Excel file. Please try again or contact support.",
                details,
            });
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

    private static object? ReadInvalidValue(JsonElement entry)
    {
        return entry.ValueKind switch
        {
            JsonValueKind.Number => entry.TryGetInt64(out var longValue)
                ? longValue
                : entry.TryGetDecimal(out var decimalValue)
                    ? decimalValue
                    : entry.GetRawText(),
            JsonValueKind.String => entry.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => entry.GetRawText(),
        };
    }

    private async Task<LineItemExportValidationResult> ValidateLineItemsForExportAsync(
        long rfqId,
        IReadOnlyList<long> lineItemIds,
        CancellationToken cancellationToken)
    {
        var records = await (from li in _dbContext.RfqLineItems.AsNoTracking()
                             join q in _dbContext.Quotes.AsNoTracking()
                                 on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                             from q in quoteGroup.DefaultIfEmpty()
                             where li.RfqId == rfqId && lineItemIds.Contains(li.Id)
                             select new LineItemExportRecord
                             {
                                 Id = li.Id,
                                 LineNumber = li.LineNumber,
                                 Status = li.Status,
                                 SelectedQuoteId = li.SelectedQuoteId,
                                 QuoteStatus = q != null ? q.Status : null,
                             })
            .ToListAsync(cancellationToken);

        var recordMap = records.ToDictionary(record => record.Id);
        var validItems = new List<LineItemExportRecord>();
        var invalidItems = new List<LineItemExportRecord>();
        var itemsNeedingFix = new List<LineItemExportRecord>();

        foreach (var id in lineItemIds)
        {
            recordMap.TryGetValue(id, out var record);
            var reason = GetInvalidReason(record);

            if (reason != null && reason.StartsWith("QUOTE_STATUS_", StringComparison.Ordinal) && record?.SelectedQuoteId != null)
            {
                record.Reason = reason;
                itemsNeedingFix.Add(record);
            }
            else if (reason != null)
            {
                var placeholder = record ?? new LineItemExportRecord { Id = id };
                placeholder.Reason = reason;
                invalidItems.Add(placeholder);
            }
            else if (record != null)
            {
                validItems.Add(record);
            }
        }

        if (itemsNeedingFix.Count > 0)
        {
            var now = DateTime.UtcNow.ToString("o");
            foreach (var item in itemsNeedingFix)
            {
                if (!item.SelectedQuoteId.HasValue)
                {
                    continue;
                }

                var quote = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == item.SelectedQuoteId.Value, cancellationToken);
                if (quote == null)
                {
                    item.Reason = "QUOTE_STATUS_UNKNOWN";
                    invalidItems.Add(item);
                    continue;
                }

                quote.Status = "selected";
                quote.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var item in itemsNeedingFix)
            {
                item.QuoteStatus = "selected";
                validItems.Add(item);
            }
        }

        return new LineItemExportValidationResult(validItems, invalidItems);
    }

    /// <summary>
    /// 在生成 PR Excel 后更新 PR 状态
    /// </summary>
    private async Task UpdatePrStatusAfterExcelGenerationAsync(
        long rfqId,
        string prNumber,
        string? department,
        AuthUser? user,
        List<long> exportedLineItemIds,
        CancellationToken cancellationToken)
    {
        try
        {
            // 检查是否已存在 PR 记录
            var existingPr = await _dbContext.RfqPrRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(pr => pr.RfqId == rfqId, cancellationToken);

            if (existingPr != null)
            {
                // 已存在 PR 记录，跳过更新
                _logger.LogInformation("PR record already exists for RFQ {RfqId}, skipping status update", rfqId);
                return;
            }

            // 获取 RFQ 记录
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
            if (rfq == null)
            {
                _logger.LogWarning("RFQ {RfqId} not found when updating PR status", rfqId);
                return;
            }

            // 创建 PR 记录
            var now = DateTime.UtcNow.ToString("o");
            var record = new SupplierSystem.Domain.Entities.RfqPrRecord
            {
                RfqId = (int)rfqId,
                PrNumber = prNumber,
                PrDate = now.Substring(0, 10), // 提取 yyyy-MM-dd 部分
                FilledBy = user?.Id ?? "system",
                FilledAt = now,
                DepartmentConfirmerId = null,
                DepartmentConfirmerName = null,
                ConfirmationStatus = "confirmed",
                ConfirmedAt = now,
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            _dbContext.RfqPrRecords.Add(record);

            // 更新导出的行项目状态为 completed
            if (exportedLineItemIds.Count > 0)
            {
                var lineItemsToUpdate = await _dbContext.RfqLineItems
                    .Where(li => li.RfqId == rfqId && exportedLineItemIds.Contains(li.Id))
                    .ToListAsync(cancellationToken);

                foreach (var lineItem in lineItemsToUpdate)
                {
                    lineItem.Status = "completed";
                    lineItem.UpdatedAt = now;
                }
            }

            rfq.PrStatus = "confirmed";

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("PR status updated for RFQ {RfqId} with PR number {PrNumber}. {LineItemCount} line items marked as completed.",
                rfqId, prNumber, exportedLineItemIds.Count);
        }
        catch (Exception ex)
        {
            // 记录错误但不中断 Excel 下载流程
            _logger.LogError(ex, "Failed to update PR status for RFQ {RfqId}. Excel download will continue.", rfqId);
        }
    }

    private static string? GetInvalidReason(LineItemExportRecord? item)
    {
        if (item == null)
        {
            return "NOT_FOUND";
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pending_po", "completed" };
        if (string.IsNullOrWhiteSpace(item.Status) || !allowed.Contains(item.Status))
        {
            return $"STATUS_{item.Status ?? "UNKNOWN"}";
        }

        if (!item.SelectedQuoteId.HasValue)
        {
            return "NO_SELECTED_QUOTE";
        }

        if (!string.Equals(item.QuoteStatus, "selected", StringComparison.OrdinalIgnoreCase))
        {
            return $"QUOTE_STATUS_{item.QuoteStatus ?? "UNKNOWN"}";
        }

        return null;
    }

    private sealed class LineItemExportRecord
    {
        public long Id { get; set; }
        public int? LineNumber { get; set; }
        public string? Status { get; set; }
        public long? SelectedQuoteId { get; set; }
        public string? QuoteStatus { get; set; }
        public string? Reason { get; set; }
    }

    private sealed record LineItemExportValidationResult(
        List<LineItemExportRecord> ValidItems,
        List<LineItemExportRecord> InvalidItems);
}
