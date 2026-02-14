using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Common;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

/// <summary>
/// 发票管理控制器
/// 处理发票的上传、审核、下载等操作
/// </summary>
[ApiController]
[Route("api/invoices")]
[Authorize]
public sealed class InvoicesController : ApiControllerBase
{
    private readonly SupplierSystemDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuditService _auditService;

    public InvoicesController(
        SupplierSystemDbContext context,
        IWebHostEnvironment environment,
        IAuditService auditService)
    {
        _context = context;
        _environment = environment;
        _auditService = auditService;
    }

    /// <summary>
    /// 获取发票列表
    /// GET /api/invoices
    /// </summary>
    /// <param name="type">发票类型筛选</param>
    /// <param name="status">状态筛选</param>
    /// <param name="page">页码</param>
    /// <param name="limit">每页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发票列表和分页信息</returns>
    /// <response code="200">成功返回发票列表</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvoices(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices.AsQueryable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(i => i.Type == type);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(i => i.Status == status);

        var total = await query.CountAsync(cancellationToken);
        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Success(new
        {
            data = invoices,
            pagination = new
            {
                total,
                page,
                limit,
                totalPages = (int)Math.Ceiling(total / (double)limit)
            }
        });
    }

    /// <summary>
    /// 获取发票详情
    /// GET /api/invoices/:id
    /// </summary>
    /// <param name="id">发票ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发票详情</returns>
    /// <response code="200">成功返回发票详情</response>
    /// <response code="404">发票不存在</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoice(int id, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice == null)
            return NotFound("Invoice not found.");

        return Success(invoice);
    }

    /// <summary>
    /// 上传发票
    /// POST /api/invoices/upload
    /// </summary>
    /// <param name="supplier_id">供应商ID</param>
    /// <param name="rfq_id">RFQ ID（可选）</param>
    /// <param name="order_id">订单ID（可选）</param>
    /// <param name="invoice_number">发票号码</param>
    /// <param name="invoice_date">发票日期</param>
    /// <param name="amount">发票金额</param>
    /// <param name="type">发票类型</param>
    /// <param name="tax_rate">税率（可选）</param>
    /// <param name="invoice_type">发票种类（可选）</param>
    /// <param name="pre_payment_proof">预付款证明（可选）</param>
    /// <param name="signature_seal">是否签章</param>
    /// <param name="file">发票文件（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传结果</returns>
    /// <response code="201">发票上传成功</response>
    /// <response code="400">请求参数错误</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">供应商不存在</response>
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadInvoice(
        [FromForm] int supplier_id,
        [FromForm] string? rfq_id,
        [FromForm] string? order_id,
        [FromForm] string invoice_number,
        [FromForm] string invoice_date,
        [FromForm] decimal amount,
        [FromForm] string type,
        [FromForm] string? tax_rate,
        [FromForm] string? invoice_type,
        [FromForm] string? pre_payment_proof,
        [FromForm] bool signature_seal = false,
        [FromForm] IFormFile? file = null,
        CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        // 验证供应商存在
        var supplier = await _context.Suppliers.FindAsync(new object[] { supplier_id }, cancellationToken);
        if (supplier == null)
            return BadRequest("Supplier not found.");

        string? storedFileName = null;
        string? filePath = null;

        if (file != null && file.Length > 0)
        {
            var uploadsPath = UploadPathHelper.GetInvoicesRoot(_environment);

            storedFileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            filePath = Path.Combine(uploadsPath, storedFileName);

            await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }
        }

        var invoice = new Invoice
        {
            SupplierId = supplier_id,
            RfqId = !string.IsNullOrEmpty(rfq_id) ? int.Parse(rfq_id) : null,
            OrderId = !string.IsNullOrEmpty(order_id) ? int.Parse(order_id) : null,
            InvoiceNumber = invoice_number,
            InvoiceDate = DateTime.Parse(invoice_date),
            Amount = amount,
            Type = type,
            TaxRate = tax_rate,
            InvoiceType = invoice_type,
            PrePaymentProof = pre_payment_proof,
            SignatureSeal = signature_seal,
            Status = "pending",
            FileName = file?.FileName,
            StoredFileName = storedFileName,
            FilePath = filePath,
            FileSize = file?.Length,
            FileType = file?.ContentType,
            CreatedBy = int.Parse(user.Id),
            CreatedAt = DateTime.UtcNow
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        // 审计日志
        await _auditService.LogAsync(new AuditEntry
        {
            Action = "INVOICE_UPLOADED",
            EntityType = "Invoice",
            EntityId = invoice.Id.ToString(),
            ActorId = user.Id,
            ActorName = user.Name,
            Changes = $"{{ InvoiceNumber: \"{invoice_number}\", Amount: {amount}, SupplierId: {supplier_id} }}"
        });

        return Created(new { invoiceId = invoice.Id, message = "Invoice uploaded successfully." });
    }

    /// <summary>
    /// 审核发票
    /// POST /api/invoices/:id/review
    /// </summary>
    /// <param name="id">发票ID</param>
    /// <param name="request">审核请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审核结果</returns>
    /// <response code="200">发票审核成功</response>
    /// <response code="400">请求参数错误或发票状态不允许审核</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">发票不存在</response>
    [HttpPost("{id:int}/review")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewInvoice(
        int id,
        [FromBody] ReviewRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        var invoice = await _context.Invoices.FindAsync(new object[] { id }, cancellationToken);
        if (invoice == null)
            return NotFound("Invoice not found.");

        if (invoice.Status != "pending")
            return BadRequest($"Invoice cannot be reviewed. Current status: {invoice.Status}");

        invoice.Status = request.Status;
        invoice.ReviewNotes = request.ReviewNotes;
        invoice.RejectionReason = request.RejectionReason;
        invoice.ReviewedBy = int.Parse(user.Id);
        invoice.ReviewedAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // 审计日志
        await _auditService.LogAsync(new AuditEntry
        {
            Action = "INVOICE_REVIEWED",
            EntityType = "Invoice",
            EntityId = id.ToString(),
            ActorId = user.Id,
            ActorName = user.Name,
            Changes = $"{{ Status: \"{request.Status}\", Notes: \"{request.ReviewNotes}\" }}"
        });

        return Success(new { message = "Invoice reviewed successfully.", status = invoice.Status });
    }

    /// <summary>
    /// 请求协助核实（异常发票）
    /// POST /api/invoices/:id/assistance-request
    /// </summary>
    /// <param name="id">发票ID</param>
    /// <param name="request">协助请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>请求结果</returns>
    /// <response code="200">协助请求创建成功</response>
    /// <response code="400">请求参数错误或发票状态不允许</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">发票不存在</response>
    [HttpPost("{id:int}/assistance-request")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestAssistance(
        int id,
        [FromBody] AssistanceRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.AssistanceType))
            return BadRequest("Assistance type is required.");

        if (string.IsNullOrWhiteSpace(request.VerificationPoints))
            return BadRequest("Verification points are required.");

        if (string.IsNullOrWhiteSpace(request.AssistanceDeadline))
            return BadRequest("Assistance deadline is required.");

        if (!DateTime.TryParse(request.AssistanceDeadline, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var deadline))
            return BadRequest("Assistance deadline is invalid.");

        var invoice = await _context.Invoices.FindAsync(new object[] { id }, cancellationToken);
        if (invoice == null)
            return NotFound("Invoice not found.");

        if (!string.Equals(invoice.Status, "exception", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Assistance can only be requested for exception invoices.");

        invoice.AssistanceRequested = true;
        invoice.AssistanceType = request.AssistanceType;
        invoice.VerificationPoints = request.VerificationPoints;
        invoice.AssistanceDeadline = deadline;
        invoice.AssistanceStatus = "pending";
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            Action = "INVOICE_ASSISTANCE_REQUESTED",
            EntityType = "Invoice",
            EntityId = id.ToString(),
            ActorId = user.Id,
            ActorName = user.Name,
            Changes = $"{{ AssistanceType: \"{request.AssistanceType}\", Deadline: \"{request.AssistanceDeadline}\" }}"
        });

        return Success(new { message = "Assistance request created successfully.", status = invoice.AssistanceStatus });
    }

    /// <summary>
    /// 财务总监审批（大批量发票）
    /// POST /api/invoices/:id/approval
    /// </summary>
    /// <param name="id">发票ID</param>
    /// <param name="request">审批请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审批结果</returns>
    /// <response code="200">审批成功</response>
    /// <response code="400">请求参数错误或金额不满足审批条件</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">发票不存在</response>
    [HttpPost("{id:int}/approval")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DirectorApproval(
        int id,
        [FromBody] DirectorApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.CreditReportUrl))
            return BadRequest("Credit report URL is required for director approval.");

        var invoice = await _context.Invoices.FindAsync(new object[] { id }, cancellationToken);
        if (invoice == null)
            return NotFound("Invoice not found.");

        if (invoice.Amount < 1000000)
            return BadRequest("Director approval is only required for invoices >= 1,000,000.");

        if (!string.Equals(invoice.Status, "verified", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invoice must be verified before director approval.");

        invoice.DirectorApproved = true;
        invoice.DirectorApproverId = int.Parse(user.Id);
        invoice.DirectorApprovedAt = DateTime.UtcNow;
        invoice.DirectorApprovalNotes = request.DirectorApprovalNotes;
        invoice.CreditReportUrl = request.CreditReportUrl;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            Action = "INVOICE_DIRECTOR_APPROVED",
            EntityType = "Invoice",
            EntityId = id.ToString(),
            ActorId = user.Id,
            ActorName = user.Name,
            Changes = $"{{ CreditReportUrl: \"{request.CreditReportUrl}\" }}"
        });

        return Success(new { message = "Director approval completed successfully." });
    }

    /// <summary>
    /// 下载发票文件
    /// GET /api/invoices/:id/download
    /// </summary>
    /// <param name="id">发票ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发票文件</returns>
    /// <response code="200">成功返回发票文件</response>
    /// <response code="404">发票或文件不存在</response>
    [HttpGet("{id:int}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadInvoice(int id, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.FindAsync(new object[] { id }, cancellationToken);
        if (invoice == null)
            return NotFound("Invoice not found.");

        if (string.IsNullOrEmpty(invoice.StoredFileName))
            return NotFound("Invoice file not found.");

        var filePath = invoice.FilePath;
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            // 尝试查找文件
            var uploadsPath = UploadPathHelper.GetInvoicesRoot(_environment);
            filePath = Path.Combine(uploadsPath, invoice.StoredFileName);
            if (!System.IO.File.Exists(filePath))
                return NotFound("Invoice file not found.");
        }

        var downloadName = string.IsNullOrEmpty(invoice.FileName) ? $"invoice_{id}_{invoice.InvoiceNumber}.pdf" : invoice.FileName;
        return PhysicalFile(filePath, invoice.FileType ?? "application/octet-stream", downloadName);
    }

    /// <summary>
    /// 删除发票
    /// DELETE /api/invoices/:id
    /// </summary>
    /// <param name="id">发票ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    /// <response code="200">发票删除成功</response>
    /// <response code="400">发票状态不允许删除</response>
    /// <response code="401">未授权访问</response>
    /// <response code="404">发票不存在</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteInvoice(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
            return Unauthorized();

        var invoice = await _context.Invoices.FindAsync(new object[] { id }, cancellationToken);
        if (invoice == null)
            return NotFound("Invoice not found.");

        if (invoice.Status != "pending")
            return BadRequest("Only pending invoices can be deleted.");

        // 删除文件
        if (!string.IsNullOrEmpty(invoice.StoredFileName))
        {
            var filePath = invoice.FilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                var uploadsPath = UploadPathHelper.GetInvoicesRoot(_environment);
                filePath = Path.Combine(uploadsPath, invoice.StoredFileName);
            }
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return Success(new { message = "Invoice deleted.", invoiceId = id });
    }

    /// <summary>
    /// 获取发票统计
    /// GET /api/invoices/stats/overview
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发票统计数据</returns>
    /// <response code="200">成功返回统计数据</response>
    [HttpGet("stats/overview")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatsOverview(CancellationToken cancellationToken)
    {
        var total = await _context.Invoices.CountAsync(cancellationToken);
        var verified = await _context.Invoices.CountAsync(i => i.Status == "verified", cancellationToken);
        var pending = await _context.Invoices.CountAsync(i => i.Status == "pending", cancellationToken);
        var rejected = await _context.Invoices.CountAsync(i => i.Status == "rejected", cancellationToken);
        var exception = await _context.Invoices.CountAsync(i => i.Status == "exception", cancellationToken);

        // 计算平均审核天数
        var reviewedInvoices = await _context.Invoices
            .Where(i => i.Status == "verified" && i.ReviewedAt.HasValue)
            .ToListAsync(cancellationToken);
        var avgReviewDays = reviewedInvoices.Any()
            ? reviewedInvoices.Average(i => (i.ReviewedAt!.Value - i.CreatedAt).TotalDays)
            : 0;

        return Success(new
        {
            total_invoices = total,
            verified_invoices = verified,
            pending_invoices = pending,
            rejected_invoices = rejected,
            exception_invoices = exception,
            large_amount_invoices = 0, // 可以后续实现
            avg_review_days = Math.Round(avgReviewDays, 1)
        });
    }

    public class ReviewRequest
    {
        public string Status { get; set; } = null!; // verified | rejected | exception
        public string? ReviewNotes { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class AssistanceRequest
    {
        public string AssistanceType { get; set; } = null!;
        public string VerificationPoints { get; set; } = null!;
        public string AssistanceDeadline { get; set; } = null!;
    }

    public class DirectorApprovalRequest
    {
        public string CreditReportUrl { get; set; } = null!;
        public string? DirectorApprovalNotes { get; set; }
    }
}
