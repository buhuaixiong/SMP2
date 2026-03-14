using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Validation;

namespace SupplierSystem.Api.Filters;

public enum RfqValidationScenario
{
    Create,
    Update,
    SendInvitations,
    SubmitQuote,
    Review
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RfqValidationFilterAttribute : Attribute, IAsyncActionFilter
{
    private readonly RfqValidationScenario _scenario;

    public RfqValidationFilterAttribute(RfqValidationScenario scenario)
    {
        _scenario = scenario;
    }

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!TryGetBody(context, out var body))
        {
            context.Result = new BadRequestObjectResult(new { message = "Request body is required." });
            return Task.CompletedTask;
        }

        var details = Validate(body);
        if (details.Count > 0)
        {
            context.Result = new ObjectResult(new
            {
                success = false,
                error = "Validation failed",
                code = "VALIDATION_ERROR",
                details,
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
            return Task.CompletedTask;
        }

        return next();
    }

    private List<ValidationDetail> Validate(JsonElement body)
    {
        return _scenario switch
        {
            RfqValidationScenario.Create => RfqValidation.ValidateCreateRfq(body),
            RfqValidationScenario.Update => RfqValidation.ValidateUpdateRfq(body),
            RfqValidationScenario.SendInvitations => RfqValidation.ValidateSendInvitations(body),
            RfqValidationScenario.SubmitQuote => RfqValidation.ValidateSubmitQuote(body),
            RfqValidationScenario.Review => RfqValidation.ValidateReview(body),
            _ => new List<ValidationDetail>()
        };
    }

    private static bool TryGetBody(ActionExecutingContext context, out JsonElement body)
    {
        foreach (var value in context.ActionArguments.Values)
        {
            if (value is JsonElement element)
            {
                body = element;
                return true;
            }
        }

        body = default;
        return false;
    }
}
