using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Exceptions;

namespace SupplierSystem.Api.Controllers;

public abstract class NodeControllerBase(IWebHostEnvironment environment) : ControllerBase
{
    private readonly IWebHostEnvironment _environment = environment;

    protected IActionResult Success(object? data, int status = 200, string? message = null)
    {
        var response = new Dictionary<string, object?> { ["success"] = true, ["data"] = data };
        if (!string.IsNullOrWhiteSpace(message))
        {
            response["message"] = message;
        }

        return StatusCode(status, response);
    }

    protected IActionResult SendCreated(object? data, string? message = null)
    {
        return Success(data, 201, message);
    }

    protected IActionResult SendNotFound(string message = "Resource not found")
    {
        return NotFound(new
        {
            success = false,
            error = message,
            code = "NOT_FOUND",
        });
    }

    protected IActionResult HandleError(Exception error, string defaultMessage = "Operation failed")
    {
        if (error is ValidationErrorException validation)
        {
            return StatusCode(400, new
            {
                success = false,
                error = validation.Message,
                details = validation.Details,
                code = "VALIDATION_ERROR",
            });
        }

        if (error is ServiceErrorException serviceError)
        {
            return StatusCode(serviceError.Status, new
            {
                success = false,
                error = serviceError.Message,
                code = serviceError.Code,
            });
        }

        if (error is DbUpdateException)
        {
            return StatusCode(409, new
            {
                success = false,
                error = "Database constraint violation",
                code = "CONSTRAINT_ERROR",
            });
        }

        var isDevelopment = _environment.IsDevelopment();
        return StatusCode(500, new
        {
            success = false,
            error = isDevelopment ? error.Message : defaultMessage,
            code = "INTERNAL_ERROR",
            stack = isDevelopment ? error.StackTrace : null,
        });
    }
}
