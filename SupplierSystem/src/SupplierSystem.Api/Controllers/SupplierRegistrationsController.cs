using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services.Registrations;
using SupplierSystem.Application.Exceptions;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/supplier-registrations")]
public sealed class SupplierRegistrationsController : ControllerBase
{
    private readonly SupplierRegistrationService _registrationService;

    public SupplierRegistrationsController(SupplierRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        try
        {
            var data = await _registrationService.GetPendingAsync(user, cancellationToken);
            return Ok(new { data });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        try
        {
            var data = await _registrationService.GetApprovedAsync(user, cancellationToken);
            return Ok(new { data });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _registrationService.GetByIdAsync(id, cancellationToken);
            return Ok(new { data });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpGet("applications/{id:int}")]
    public async Task<IActionResult> GetApplication(int id, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _registrationService.GetByIdAsync(id, cancellationToken);
            return Ok(new { data });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/status")]
    public async Task<IActionResult> GetStatus(int id, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _registrationService.GetStatusAsync(id, cancellationToken);
            return Ok(new { data });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpGet("current/status")]
    public async Task<IActionResult> GetCurrentStatus([FromQuery] int? supplierId, CancellationToken cancellationToken)
    {
        try
        {
            if (supplierId.HasValue && supplierId.Value > 0)
            {
                var data = await _registrationService.GetStatusBySupplierAsync(supplierId.Value, cancellationToken);
                return Ok(new { data });
            }

            var user = HttpContext.GetAuthUser();
            if (user == null)
            {
                return Unauthorized(new { message = "Authentication required." });
            }

            var selfData = await _registrationService.GetStatusForCurrentUserAsync(user, cancellationToken);
            return Ok(new { data = selfData });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpPost("drafts")]
    [AllowAnonymous]
    public async Task<IActionResult> SaveDraft(
        [FromBody] SupplierRegistrationDraftRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new SupplierRegistrationDraftRequest();
        var formPayload = request.GetFormPayload();
        var draftToken = request.DraftToken;
        var lastStep = request.LastStep;

        try
        {
            var result = await _registrationService.SaveDraftAsync(
                formPayload,
                draftToken,
                lastStep,
                cancellationToken);

            var response = new
            {
                draftToken = result.DraftToken,
                status = result.Status,
                expiresAt = result.ExpiresAt,
                lastStep = result.LastStep,
                validation = new
                {
                    valid = result.Validation.Valid,
                    errors = result.Validation.Errors,
                },
                normalized = result.Normalized,
            };

            return StatusCode(result.IsNew ? StatusCodes.Status201Created : StatusCodes.Status200OK, response);
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpGet("drafts/{draftToken}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDraft(string draftToken, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _registrationService.GetDraftAsync(draftToken, cancellationToken);
            return Ok(new
            {
                draftToken = result.DraftToken,
                status = result.Status,
                lastStep = result.LastStep,
                expiresAt = result.ExpiresAt,
                createdAt = result.CreatedAt,
                updatedAt = result.UpdatedAt,
                form = result.Form,
                normalized = result.Normalized,
                validation = new
                {
                    valid = result.Errors.Count == 0,
                    errors = result.Errors,
                },
                submittedApplicationId = result.SubmittedApplicationId ?? null,
            });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] ApproveRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        try
        {
            var result = await _registrationService.ApproveAsync(
                id,
                user,
                request?.Comment,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
                nextStatus = result.NextStatus,
                activationToken = result.ActivationToken,
            });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] RejectRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        try
        {
            var result = await _registrationService.RejectAsync(
                id,
                user,
                request?.Reason ?? string.Empty,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
            });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/request-info")]
    public async Task<IActionResult> RequestInfo(
        int id,
        [FromBody] RequestInfoRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        try
        {
            var result = await _registrationService.RequestInfoAsync(
                id,
                user,
                request?.Message ?? string.Empty,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = result.Message,
            });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/bind-code")]
    public async Task<IActionResult> BindCode(
        int id,
        [FromBody] BindSupplierCodeRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        try
        {
            var result = await _registrationService.BindSupplierCodeAsync(
                id,
                user,
                request?.SupplierCode,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Supplier code bound successfully; account converted to temp supplier",
                supplierCode = result.SupplierCode,
                supplierId = result.SupplierId,
                loginMethods = result.LoginMethods.Select(method => new
                {
                    type = method.Type,
                    value = method.Value,
                }),
            });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    public sealed class ApproveRegistrationRequest
    {
        public string? Comment { get; set; }
    }

    public sealed class RejectRegistrationRequest
    {
        public string? Reason { get; set; }
    }

    public sealed class RequestInfoRegistrationRequest
    {
        public string? Message { get; set; }
    }

    public sealed class BindSupplierCodeRequest
    {
        public string? SupplierCode { get; set; }
    }

    public sealed class SupplierRegistrationDraftRequest
    {
        public JsonElement? Form { get; set; }
        public JsonElement? FormData { get; set; }
        public JsonElement? Payload { get; set; }
        public string? DraftToken { get; set; }
        public string? LastStep { get; set; }

        public JsonElement GetFormPayload()
        {
            if (Form.HasValue && Form.Value.ValueKind == JsonValueKind.Object)
            {
                return Form.Value;
            }

            if (FormData.HasValue && FormData.Value.ValueKind == JsonValueKind.Object)
            {
                return FormData.Value;
            }

            if (Payload.HasValue && Payload.Value.ValueKind == JsonValueKind.Object)
            {
                return Payload.Value;
            }

            return default;
        }
    }
}
