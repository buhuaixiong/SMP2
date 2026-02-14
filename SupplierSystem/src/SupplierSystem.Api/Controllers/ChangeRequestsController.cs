using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Models.ChangeRequests;
using SupplierSystem.Api.Services.ChangeRequests;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[NodeResponse]
[Route("api/change-requests")]
public sealed class ChangeRequestsController : ControllerBase
{
    private readonly ChangeRequestService _service;
    private readonly ILogger<ChangeRequestsController> _logger;

    public ChangeRequestsController(ChangeRequestService service, ILogger<ChangeRequestsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateChangeRequest(
        [FromBody] ChangeRequestSubmission? request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (request?.SupplierId == null || request.Changes == null)
        {
            return BadRequest(new { message = "supplierId and changes are required" });
        }

        try
        {
            var result = await _service.CreateChangeRequestAsync(
                user,
                request.SupplierId.Value,
                request.Changes,
                cancellationToken);
            var message = result.TryGetValue("message", out var value) ? value?.ToString() : null;
            return StatusCode(201, new
            {
                message = message ?? "变更申请已提交",
                data = result
            });
        }
        catch (ChangeRequestServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message ?? "Failed to create change request" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating change request.");
            var status = ContainsPermissionError(ex.Message) ? 403 : 400;
            return StatusCode(status, new { message = ex.Message ?? "Failed to create change request" });
        }
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        var limitValue = NormalizeLimit(limit, 50);
        var offsetValue = NormalizeOffset(offset);

        try
        {
            var requests = await _service.GetPendingApprovalsAsync(user, limitValue, offsetValue, cancellationToken);
            return Ok(new
            {
                data = requests,
                pagination = new
                {
                    limit = limitValue,
                    offset = offsetValue,
                    total = requests.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending approvals.");
            return StatusCode(500, new { message = ex.Message ?? "Failed to fetch pending approvals" });
        }
    }

    [HttpGet("approved")]
    public async Task<IActionResult> GetApprovedApprovals(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        var limitValue = NormalizeLimit(limit, 50);
        var offsetValue = NormalizeOffset(offset);

        try
        {
            var requests = await _service.GetApprovedApprovalsAsync(user, limitValue, offsetValue, cancellationToken);
            return Ok(new
            {
                data = requests,
                pagination = new
                {
                    limit = limitValue,
                    offset = offsetValue,
                    total = requests.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching approved change requests.");
            return StatusCode(500, new { message = ex.Message ?? "Failed to fetch approved change requests" });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetChangeRequestDetails(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid request ID" });
        }

        try
        {
            var details = await _service.GetChangeRequestDetailsAsync(user, id, cancellationToken);
            return Ok(new { data = details });
        }
        catch (ChangeRequestServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message ?? "Failed to fetch change request details" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching change request details.");
            return StatusCode(500, new { message = ex.Message ?? "Failed to fetch change request details" });
        }
    }

    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> ApproveChangeRequest(
        int id,
        [FromBody] ChangeRequestApprovalRequest? request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid request ID" });
        }

        var decision = request?.Decision;
        if (string.IsNullOrWhiteSpace(decision) ||
            (!string.Equals(decision, "approved", StringComparison.Ordinal) &&
             !string.Equals(decision, "rejected", StringComparison.Ordinal)))
        {
            return BadRequest(new { message = "Decision must be \"approved\" or \"rejected\"" });
        }

        try
        {
            var result = await _service.ApproveChangeRequestAsync(
                user,
                id,
                decision,
                request?.Comments ?? string.Empty,
                cancellationToken);
            var message = result.TryGetValue("message", out var value) ? value?.ToString() : null;
            return Ok(new
            {
                message = message,
                data = result
            });
        }
        catch (ChangeRequestServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message ?? "Failed to approve change request" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving change request.");
            var status = ex.Message?.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0 ? 403 : 400;
            return StatusCode(status, new { message = ex.Message ?? "Failed to approve change request" });
        }
    }

    [HttpGet("supplier/{supplierId:int}")]
    public async Task<IActionResult> GetSupplierChangeRequests(
        int supplierId,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (supplierId <= 0)
        {
            return BadRequest(new { message = "Invalid supplier ID" });
        }

        var limitValue = NormalizeLimit(limit, 50);
        var offsetValue = NormalizeOffset(offset);

        try
        {
            var requests = await _service.GetSupplierChangeRequestsAsync(
                user,
                supplierId,
                status,
                limitValue,
                offsetValue,
                cancellationToken);
            return Ok(new
            {
                data = requests,
                pagination = new
                {
                    limit = limitValue,
                    offset = offsetValue,
                    total = requests.Count
                }
            });
        }
        catch (ChangeRequestServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message ?? "Failed to fetch supplier change requests" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching supplier change requests.");
            var statusCode = ex.Message?.IndexOf("Access denied", StringComparison.OrdinalIgnoreCase) >= 0 ? 403 : 500;
            return StatusCode(statusCode, new { message = ex.Message ?? "Failed to fetch supplier change requests" });
        }
    }

    private static bool ContainsPermissionError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.IndexOf("Permission", StringComparison.OrdinalIgnoreCase) >= 0 ||
               message.IndexOf("Access", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int NormalizeLimit(int? limit, int fallback)
    {
        if (!limit.HasValue)
        {
            return fallback;
        }

        return limit.Value < 0 ? 0 : limit.Value;
    }

    private static int NormalizeOffset(int? offset)
    {
        if (!offset.HasValue || offset.Value < 0)
        {
            return 0;
        }

        return offset.Value;
    }
}
