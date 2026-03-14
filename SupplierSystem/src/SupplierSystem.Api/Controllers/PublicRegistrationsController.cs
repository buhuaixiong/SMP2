using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Services.Registrations;
using SupplierSystem.Application.Exceptions;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public")]
public sealed class PublicRegistrationsController : ControllerBase
{
    private readonly SupplierRegistrationService _registrationService;

    public PublicRegistrationsController(SupplierRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    [HttpPost("supplier-registrations/drafts")]
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

    [HttpGet("supplier-registrations/drafts/{draftToken}")]
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

    [HttpPost("supplier-registrations")]
    public async Task<IActionResult> SubmitRegistration(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _registrationService.SubmitRegistrationAsync(payload, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Registration submitted successfully. Your application is now under review. You can track the approval progress using the link provided.",
                applicationId = result.ApplicationId,
                supplierCode = result.SupplierCode,
                trackingToken = result.TrackingToken,
                trackingUrl = result.TrackingUrl,
                trackingUsername = result.TrackingUsername,
                trackingPassword = result.TrackingPassword,
                trackingMessage = result.TrackingMessage,
                draftToken = result.DraftToken,
                assignedPurchaserId = result.AssignedPurchaserId,
                assignedPurchaserName = result.AssignedPurchaserName,
                assignedPurchaserEmail = result.AssignedPurchaserEmail,
                status = result.Status,
                nextStep = result.NextStep,
            });
        }
        catch (HttpResponseException ex)
        {
            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
    }

    [HttpPost("activate-account")]
    public IActionResult ActivateAccount()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            error = "This endpoint has been deprecated.",
            message = "Accounts are activated when the finance accountant binds the supplier code.",
        });
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
