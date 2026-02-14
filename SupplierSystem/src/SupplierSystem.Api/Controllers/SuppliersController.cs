using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Common;
using SupplierSystem.Application.Security;
using System.Threading;

namespace SupplierSystem.Api.Controllers;

/// <summary>
/// 供应商管理控制器 - 对齐Node.js API完整功能
/// 继承统一基类提供标准化响应
/// </summary>
[ApiController]
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController : ApiControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SuppliersController> _logger;

    public SuppliersController(
        ISupplierService supplierService,
        IWebHostEnvironment environment,
        ILogger<SuppliersController> logger)
    {
        _supplierService = supplierService;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// 获取供应商列表
    /// GET /api/suppliers
    /// </summary>
    /// <param name="status">状态筛选</param>
    /// <param name="category">类别筛选</param>
    /// <param name="region">地区筛选</param>
    /// <param name="stage">阶段筛选</param>
    /// <param name="importance">重要性筛选</param>
    /// <param name="query">搜索关键词</param>
    /// <param name="tag">标签筛选</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="offset">偏移量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>供应商列表</returns>
    /// <response code="200">成功返回供应商列表</response>
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

    /// <summary>
    /// 获取供应商统计
    /// GET /api/suppliers/stats
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>供应商统计数据</returns>
    /// <response code="200">成功返回统计数据</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatsOverview(CancellationToken cancellationToken)
    {
        var stats = await _supplierService.GetStatsOverviewAsync(cancellationToken);
        return Success(stats);
    }

    /// <summary>
    /// 获取供应商基准数据
    /// GET /api/suppliers/benchmarks
    /// </summary>
    [HttpGet("benchmarks")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBenchmarks(CancellationToken cancellationToken)
    {
        var benchmarks = await _supplierService.GetBenchmarksAsync(cancellationToken);
        return Success(benchmarks);
    }

    /// <summary>
    /// 预览审批流程
    /// POST /api/suppliers/preview-approval
    /// </summary>
    [HttpPost("preview-approval")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewApproval([FromBody] object payload, CancellationToken cancellationToken)
    {
        var preview = await _supplierService.PreviewApprovalAsync(payload, cancellationToken);
        return Success(preview);
    }

    /// <summary>
    /// 创建供应商
    /// POST /api/suppliers
    /// </summary>
    /// <param name="request">供应商创建请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的供应商</returns>
    /// <response code="201">供应商创建成功</response>
    /// <response code="400">请求数据验证失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="500">服务器内部错误</response>
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
            return Unauthorized();

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

    /// <summary>
    /// 获取供应商详情
    /// GET /api/suppliers/:id
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>供应商详情</returns>
    /// <response code="200">成功返回供应商详情</response>
    /// <response code="404">供应商不存在</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);
        if (supplier == null)
            return NotFound("Supplier not found.");

        return Success(supplier);
    }

    /// <summary>
    /// 更新供应商
    /// PUT /api/suppliers/:id
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="request">更新请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的供应商</returns>
    /// <response code="200">供应商更新成功</response>
    /// <response code="404">供应商不存在</response>
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
            return NotFound("Supplier not found.");

        return Success(supplier);
    }

    /// <summary>
    /// 更新供应商状态
    /// PATCH /api/suppliers/:id/status
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="request">状态更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的供应商</returns>
    /// <response code="200">供应商状态更新成功</response>
    /// <response code="404">供应商不存在</response>
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
            return NotFound("Supplier not found.");

        return Success(supplier);
    }

    /// <summary>
    /// 审批供应商
    /// PUT /api/suppliers/:id/approve
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="request">审批请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审批后的供应商</returns>
    /// <response code="200">供应商审批成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">供应商不存在</response>
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
            return Unauthorized();

        var supplier = await _supplierService.ApproveSupplierAsync(id, request, user.Name ?? user.Id, cancellationToken);
        if (supplier == null)
            return NotFound("Supplier not found.");

        return Success(supplier);
    }

    /// <summary>
    /// 更新供应商标签
    /// PUT /api/suppliers/:id/tags
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="request">标签更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的标签</returns>
    /// <response code="200">标签更新成功</response>
    /// <response code="400">请求参数错误</response>
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
            return BadRequest("Tags is required.");

        var tags = await _supplierService.UpdateSupplierTagsAsync(id, request.Tags, cancellationToken);
        return Success(tags);
    }

    /// <summary>
    /// 发行临时账户
    /// POST /api/suppliers/:id/temp-accounts
    /// </summary>
    [HttpPost("{id:int}/temp-accounts")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueTempAccount(int id, [FromBody] object payload, CancellationToken cancellationToken)
    {
        var result = await _supplierService.IssueTempAccountAsync(id, payload, cancellationToken);
        return Success(result);
    }

    /// <summary>
    /// 确定供应商代码
    /// POST /api/suppliers/:id/finalize-code
    /// </summary>
    [HttpPost("{id:int}/finalize-code")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> FinalizeSupplierCode(int id, [FromBody] object payload, CancellationToken cancellationToken)
    {
        var result = await _supplierService.FinalizeSupplierCodeAsync(id, payload, cancellationToken);
        return Success(result);
    }

    /// <summary>
    /// 获取供应商变更历史
    /// GET /api/suppliers/:id/history
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="offset">偏移量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>变更历史记录</returns>
    /// <response code="200">成功返回变更历史</response>
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

    #region 文档管理

    /// <summary>
    /// 获取供应商文档列表
    /// GET /api/suppliers/:id/documents
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文档列表</returns>
    /// <response code="200">成功返回文档列表</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限访问</response>
    /// <response code="404">供应商不存在</response>
    [HttpGet("{id:int}/documents")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListDocuments(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        if (!DocumentAccess.CanAccessDocuments(user, id))
            return Forbidden();

        var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);
        if (supplier == null)
            return NotFound("Supplier not found.");

        var documents = await _supplierService.GetSupplierDocumentsAsync(id, cancellationToken);
        return Success(documents);
    }

    /// <summary>
    /// 上传供应商文档
    /// POST /api/suppliers/:id/documents
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="file">上传的文件</param>
    /// <param name="docType">文档类型</param>
    /// <param name="category">文档分类</param>
    /// <param name="validFrom">有效期开始日期</param>
    /// <param name="expiresAt">有效期结束日期</param>
    /// <param name="notes">备注</param>
    /// <param name="isRequired">是否必需</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传的文档</returns>
    /// <response code="201">文档上传成功</response>
    /// <response code="400">请求参数错误</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限上传</response>
    /// <response code="404">供应商不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("{id:int}/documents")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadDocument(
        int id,
        IFormFile file,
        [FromForm] string? docType,
        [FromForm] string? category,
        [FromForm] string? validFrom,
        [FromForm] string? expiresAt,
        [FromForm] string? notes,
        [FromForm] bool isRequired = false,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        if (!DocumentAccess.CanUploadDocuments(user, id))
            return Forbidden();

        var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        var filePath = Path.Combine(documentsRoot, storedName);

        try
        {
            await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var document = await _supplierService.UploadDocumentAsync(
                id,
                file.FileName,
                storedName,
                file.Length,
                user.Name ?? user.Id,
                new UploadDocumentRequest
                {
                    DocType = docType,
                    Category = category,
                    ValidFrom = validFrom,
                    ExpiresAt = expiresAt,
                    Notes = notes,
                    IsRequired = isRequired
                },
                cancellationToken);

            return Created(document);
        }
        catch (ServiceException ex)
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            return StatusCode(ex.StatusCode, new ApiErrorResponse { Error = ex.Message, Code = ex.ErrorCode });
        }
        catch (Exception)
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            return InternalError("Failed to upload document.");
        }
    }

    /// <summary>
    /// 下载供应商文档
    /// GET /api/suppliers/:id/documents/:docId/download
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="docId">文档ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文档文件</returns>
    /// <response code="200">成功返回文档文件</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限访问</response>
    /// <response code="404">文档不存在</response>
    [HttpGet("{id:int}/documents/{docId:int}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(int id, int docId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        if (!DocumentAccess.CanAccessDocuments(user, id))
            return Forbidden();

        var document = await _supplierService.GetDocumentDownloadInfoAsync(id, docId, cancellationToken);
        if (document == null)
            return NotFound("Document not found.");

        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        var storedName = document.StoredName;
        if (string.IsNullOrWhiteSpace(storedName))
            return NotFound("Document file not found.");

        var filePath = Path.Combine(documentsRoot, storedName);
        if (!System.IO.File.Exists(filePath))
            return NotFound("Document file not found.");

        var downloadName = string.IsNullOrWhiteSpace(document.OriginalName) ? storedName : document.OriginalName;
        return PhysicalFile(filePath, "application/octet-stream", downloadName);
    }

    /// <summary>
    /// 删除供应商文档
    /// DELETE /api/suppliers/:id/documents/:docId
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="docId">文档ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    /// <response code="204">文档删除成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限删除</response>
    /// <response code="404">文档不存在</response>
    [HttpDelete("{id:int}/documents/{docId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(int id, int docId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        if (!DocumentAccess.CanUploadDocuments(user, id))
            return Forbidden();

        var document = await _supplierService.GetDocumentDownloadInfoAsync(id, docId, cancellationToken);
        if (document == null)
            return NotFound("Document not found.");

        var deleted = await _supplierService.DeleteDocumentAsync(id, docId, cancellationToken);
        if (!deleted)
            return NotFound("Document not found.");

        if (!string.IsNullOrWhiteSpace(document.StoredName))
        {
            var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
            var filePath = Path.Combine(documentsRoot, document.StoredName);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        return NoContent();
    }

    /// <summary>
    /// 续期供应商文档
    /// POST /api/suppliers/:id/documents/:docId/renew
    /// </summary>
    [HttpPost("{id:int}/documents/{docId:int}/renew")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> RenewDocument(
        int id,
        int docId,
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanUploadDocuments(user, id))
        {
            return Forbidden();
        }

        var existing = await _supplierService.GetDocumentDownloadInfoAsync(id, docId, cancellationToken);
        if (existing == null)
        {
            return NotFound("Document not found.");
        }

        var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        var filePath = Path.Combine(documentsRoot, storedName);

        try
        {
            await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var updated = await _supplierService.RenewDocumentAsync(
                id,
                docId,
                storedName,
                file.FileName,
                file.Length,
                user.Name ?? user.Id,
                cancellationToken);

            if (updated == null)
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                return NotFound("Document not found.");
            }

            if (!string.IsNullOrWhiteSpace(existing.StoredName))
            {
                var oldPath = Path.Combine(documentsRoot, existing.StoredName);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            return Success(updated);
        }
        catch (ServiceException ex)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return StatusCode(ex.StatusCode, new ApiErrorResponse { Error = ex.Message, Code = ex.ErrorCode });
        }
        catch (Exception)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return InternalError("Failed to renew document.");
        }
    }

    /// <summary>
    /// 批量上传文档
    /// POST /api/suppliers/:id/documents/bulk
    /// </summary>
    [HttpPost("{id:int}/documents/bulk")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkUploadDocuments(
        int id,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest("No files uploaded.");
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanUploadDocuments(user, id))
        {
            return Forbidden();
        }

        var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);
        if (supplier == null)
        {
            return NotFound("Supplier not found.");
        }

        var docTypes = Request.Form["docTypes"];
        var categories = Request.Form["categories"];
        var validFromValues = Request.Form["validFrom"];
        var expiresAtValues = Request.Form["expiresAt"];

        var success = new List<SupplierDocumentResponse>();
        var failed = new List<object>();
        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var docType = GetFormValue(docTypes, index);
            var category = GetFormValue(categories, index);
            var validFrom = GetFormValue(validFromValues, index);
            var expiresAt = GetFormValue(expiresAtValues, index);

            if (string.IsNullOrWhiteSpace(docType))
            {
                failed.Add(new { filename = file.FileName, error = "Missing document type" });
                continue;
            }

            if (IsInvalidDateRange(validFrom, expiresAt))
            {
                failed.Add(new { filename = file.FileName, error = "Valid from date must be earlier than expires at date" });
                continue;
            }

            var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(documentsRoot, storedName);

            try
            {
                await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }
            }
            catch (Exception)
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                failed.Add(new { filename = file.FileName, error = "Failed to store document." });
                continue;
            }

            try
            {
                var document = await _supplierService.UploadDocumentAsync(
                    id,
                    file.FileName,
                    storedName,
                    file.Length,
                    user.Name ?? user.Id,
                    new UploadDocumentRequest
                    {
                        DocType = docType,
                        Category = category,
                        ValidFrom = validFrom,
                        ExpiresAt = expiresAt,
                        Notes = null,
                        IsRequired = false
                    },
                    cancellationToken);

                success.Add(document);
            }
            catch (ServiceException ex)
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                failed.Add(new { filename = file.FileName, error = ex.Message });
            }
            catch (Exception)
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                failed.Add(new { filename = file.FileName, error = "Failed to upload document." });
            }
        }

        return Success(new { success, failed });
    }

    #endregion

    #region 草稿管理

    /// <summary>
    /// 保存供应商草稿
    /// POST /api/suppliers/:id/draft
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="request">草稿数据请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    /// <response code="200">草稿保存成功</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">供应商不存在</response>
    [HttpPost("{id:int}/draft")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveDraft(int id, [FromBody] SaveDraftRequest request, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        var saved = await _supplierService.SaveDraftAsync(id, request.DraftData!, user.Name ?? user.Id, cancellationToken);
        if (!saved)
            return NotFound("Supplier not found.");

        return Success(null, "Draft saved successfully.");
    }

    /// <summary>
    /// 获取供应商草稿
    /// GET /api/suppliers/:id/draft
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>草稿数据</returns>
    /// <response code="200">成功返回草稿</response>
    [HttpGet("{id:int}/draft")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDraft(int id, CancellationToken cancellationToken)
    {
        var draft = await _supplierService.GetDraftAsync(id, cancellationToken);
        return Success(draft);
    }

    /// <summary>
    /// 删除供应商草稿
    /// DELETE /api/suppliers/:id/draft
    /// </summary>
    /// <param name="id">供应商ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    /// <response code="204">草稿删除成功</response>
    /// <response code="404">草稿不存在</response>
    [HttpDelete("{id:int}/draft")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDraft(int id, CancellationToken cancellationToken)
    {
        var deleted = await _supplierService.DeleteDraftAsync(id, cancellationToken);
        if (!deleted)
            return NotFound("Draft not found.");

        return NoContent();
    }

    #endregion

    #region 批量导入

    /// <summary>
    /// 从Excel文件导入供应商
    /// POST /api/suppliers/import
    /// </summary>
    /// <param name="file">上传的Excel文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导入结果</returns>
    /// <response code="200">导入成功</response>
    /// <response code="400">文件格式错误或解析失败</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限执行导入</response>
    [HttpPost("import")]
    [Authorize(Policy = Permissions.AdminRoleManage)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ImportSuppliers(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Excel file is required.");
        }

        // 验证文件扩展名
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (extension != ".xls" && extension != ".xlsx")
        {
            return BadRequest("Only Excel files (.xls, .xlsx) are supported.");
        }

        // 验证文件大小 (8MB limit)
        if (file.Length > 8 * 1024 * 1024)
        {
            return BadRequest("File size exceeds the 8MB limit.");
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            var fileContent = memoryStream.ToArray();

            var result = await _supplierService.ImportSuppliersFromExcelAsync(
                fileContent,
                file.FileName,
                user.Name ?? user.Id,
                CancellationToken.None);

            return Ok(new { data = result });
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Supplier import timed out while processing Excel file");
            return StatusCode(StatusCodes.Status408RequestTimeout, new { message = "Import request timed out. Please retry with a smaller file or try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import suppliers from Excel");
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    private static string? GetFormValue(IReadOnlyList<string> values, int index)
    {
        if (values.Count == 0)
        {
            return null;
        }

        if (index < values.Count)
        {
            return values[index];
        }

        return values[0];
    }

    private static bool IsInvalidDateRange(string? validFrom, string? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(validFrom) || string.IsNullOrWhiteSpace(expiresAt))
        {
            return false;
        }

        if (!DateTime.TryParse(validFrom, out var validFromDate))
        {
            return false;
        }

        if (!DateTime.TryParse(expiresAt, out var expiresAtDate))
        {
            return false;
        }

        return validFromDate >= expiresAtDate;
    }
}
