using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SupplierSystem.Application.Models.Common;

namespace SupplierSystem.Api.Filters;

public sealed class ApiResponseFilter : IAsyncResultFilter
{
    private readonly IWebHostEnvironment _environment;

    public ApiResponseFilter(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<NodeResponseAttribute>().Any())
        {
            await next();
            return;
        }

        switch (context.Result)
        {
            case JsonResult jsonResult:
                context.Result = WrapJsonResult(jsonResult, _environment.IsDevelopment());
                break;
            case EmptyResult:
                context.Result = WrapStatusCode(StatusCodes.Status200OK, _environment.IsDevelopment());
                break;
            case NoContentResult:
                context.Result = WrapStatusCode(StatusCodes.Status200OK, _environment.IsDevelopment());
                break;
            case ForbidResult:
                context.Result = WrapStatusCode(StatusCodes.Status403Forbidden, _environment.IsDevelopment());
                break;
            case UnauthorizedResult:
                context.Result = WrapStatusCode(StatusCodes.Status401Unauthorized, _environment.IsDevelopment());
                break;
            case NotFoundResult:
                context.Result = WrapStatusCode(StatusCodes.Status404NotFound, _environment.IsDevelopment());
                break;
            case BadRequestResult:
                context.Result = WrapStatusCode(StatusCodes.Status400BadRequest, _environment.IsDevelopment());
                break;
            case StatusCodeResult statusCodeResult:
                context.Result = WrapStatusCode(statusCodeResult.StatusCode, _environment.IsDevelopment());
                break;
            case ObjectResult objectResult:
                context.Result = WrapObjectResult(objectResult, _environment.IsDevelopment());
                break;
        }

        await next();
    }

    private static IActionResult WrapJsonResult(JsonResult jsonResult, bool includeServerErrorDetails)
    {
        var statusCode = jsonResult.StatusCode ?? StatusCodes.Status200OK;
        var objectResult = new ObjectResult(jsonResult.Value) { StatusCode = statusCode };
        return WrapObjectResult(objectResult, includeServerErrorDetails);
    }

    private static IActionResult WrapObjectResult(ObjectResult objectResult, bool includeServerErrorDetails)
    {
        var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;

        if (statusCode >= 400)
        {
            if (objectResult.Value is ApiErrorResponse)
            {
                return objectResult;
            }

            var errorResponse = BuildErrorResponse(objectResult.Value, statusCode, includeServerErrorDetails);
            return new ObjectResult(errorResponse) { StatusCode = statusCode };
        }

        if (IsAlreadyFormatted(objectResult.Value))
        {
            return objectResult;
        }

        if (TryBuildPaginatedResponse(objectResult.Value, out var paginated))
        {
            return new ObjectResult(paginated) { StatusCode = NormalizeSuccessStatus(statusCode) };
        }

        if (TryBuildMergedResponse(objectResult.Value, out var merged))
        {
            return new ObjectResult(merged) { StatusCode = NormalizeSuccessStatus(statusCode) };
        }

        var response = BuildSuccessResponse(objectResult.Value);
        return new ObjectResult(response) { StatusCode = NormalizeSuccessStatus(statusCode) };
    }

    private static IActionResult WrapStatusCode(int statusCode, bool includeServerErrorDetails)
    {
        if (statusCode >= 400)
        {
            var errorResponse = BuildErrorResponse(null, statusCode, includeServerErrorDetails);
            return new ObjectResult(errorResponse) { StatusCode = statusCode };
        }

        var response = new ApiResponse { Success = true };
        return new ObjectResult(response) { StatusCode = NormalizeSuccessStatus(statusCode) };
    }

    private static int NormalizeSuccessStatus(int statusCode)
    {
        return statusCode == StatusCodes.Status204NoContent
            ? StatusCodes.Status200OK
            : statusCode;
    }

    private static ApiResponse BuildSuccessResponse(object? value)
    {
        if (value == null)
        {
            return new ApiResponse { Success = true };
        }

        if (TryGetPropertyMap(value, out var map))
        {
            if (TryGetString(map, "message", out var message))
            {
                if (!map.ContainsKey("data") && map.Count == 1)
                {
                    return new ApiResponse { Success = true, Message = message };
                }
            }

            if (map.TryGetValue("data", out var dataValue))
            {
                var messageValue = TryGetString(map, "message", out var extractedMessage) ? extractedMessage : null;
                if (HasOnlyKeys(map, "data", "message"))
                {
                    return new ApiResponse { Success = true, Data = dataValue, Message = messageValue };
                }
            }
        }

        return new ApiResponse { Success = true, Data = value };
    }

    private static bool TryBuildMergedResponse(object? value, out object merged)
    {
        merged = null!;
        if (value == null)
        {
            return false;
        }

        if (!TryGetPropertyMap(value, out var map))
        {
            return false;
        }

        if (map.ContainsKey("success"))
        {
            return false;
        }

        if (!map.ContainsKey("data") && !map.ContainsKey("message"))
        {
            return false;
        }

        if (HasOnlyKeys(map, "data", "message") || HasOnlyKeys(map, "message"))
        {
            return false;
        }

        var output = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true
        };

        foreach (var item in map)
        {
            output[item.Key] = item.Value;
        }

        merged = output;
        return true;
    }

    private static ApiErrorResponse BuildErrorResponse(object? value, int statusCode, bool includeServerErrorDetails)
    {
        if (value is ApiErrorResponse existing)
        {
            return NormalizeError(existing, statusCode, includeServerErrorDetails);
        }

        string? message = null;
        string? code = null;
        object? details = null;
        string? stack = null;

        if (value is ValidationProblemDetails validationDetails)
        {
            message = validationDetails.Title ?? "Validation failed";
            details = validationDetails.Errors;
        }
        else if (value is ProblemDetails problemDetails)
        {
            message = problemDetails.Title ?? problemDetails.Detail;
            details = problemDetails.Detail;
        }

        if (TryGetPropertyMap(value, out var map))
        {
            if (TryGetString(map, "message", out var messageValue))
            {
                message ??= messageValue;
            }

            if (TryGetString(map, "error", out var errorValue))
            {
                if (message == null)
                {
                    message = errorValue;
                }
                else if (code == null)
                {
                    code = errorValue;
                }
            }

            if (TryGetString(map, "code", out var codeValue))
            {
                code ??= codeValue;
            }

            if (TryGetString(map, "errorCode", out var errorCodeValue))
            {
                code ??= errorCodeValue;
            }

            if (map.TryGetValue("details", out var detailsValue))
            {
                details ??= detailsValue;
            }

            if (map.TryGetValue("errors", out var errorsValue))
            {
                details ??= errorsValue;
            }

            if (TryGetString(map, "stack", out var stackValue))
            {
                stack ??= stackValue;
            }
        }

        message ??= GetDefaultMessage(statusCode);
        code ??= GetDefaultCode(statusCode);

        var response = new ApiErrorResponse
        {
            Error = message,
            Code = code,
            Details = details,
            Stack = stack
        };

        return NormalizeError(response, statusCode, includeServerErrorDetails);
    }

    private static ApiErrorResponse NormalizeError(ApiErrorResponse response, int statusCode, bool includeServerErrorDetails)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError && !includeServerErrorDetails)
        {
            response.Error = "An error occurred processing your request.";
            response.Details = null;
            response.Stack = null;
            response.Code = GetDefaultCode(statusCode);
        }

        response.Error ??= GetDefaultMessage(statusCode);
        response.Code ??= GetDefaultCode(statusCode);
        response.Success = false;
        return response;
    }

    private static bool IsAlreadyFormatted(object? value)
    {
        if (value == null)
        {
            return false;
        }

        if (value is ApiResponse || value is ApiErrorResponse || value is PaginatedResponse)
        {
            return true;
        }

        var type = value.GetType();
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(ApiResponse<>) || definition == typeof(PaginatedResponse<>))
            {
                return true;
            }
        }

        if (TryGetPropertyMap(value, out var map))
        {
            return map.ContainsKey("success");
        }

        return false;
    }

    private static bool TryBuildPaginatedResponse(object? value, out PaginatedResponse response)
    {
        response = null!;
        if (value == null)
        {
            return false;
        }

        if (!TryGetPropertyMap(value, out var map))
        {
            return false;
        }

        if (!map.TryGetValue("data", out var dataValue))
        {
            return false;
        }

        if (!TryGetInt(map, "page", out var page) ||
            !TryGetInt(map, "pageSize", out var pageSize) ||
            !TryGetInt(map, "total", out var total) ||
            !TryGetInt(map, "totalPages", out var totalPages))
        {
            return false;
        }

        if (!HasOnlyKeys(map, "data", "page", "pageSize", "total", "totalPages") &&
            !HasOnlyKeys(map, "data", "page", "pageSize", "total", "totalPages", "message"))
        {
            return false;
        }

        response = new PaginatedResponse
        {
            Success = true,
            Data = dataValue,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages,
            Message = TryGetString(map, "message", out var message) ? message : null
        };
        return true;
    }

    private static bool HasOnlyKeys(IReadOnlyDictionary<string, object?> map, params string[] keys)
    {
        if (map.Count != keys.Length)
        {
            return false;
        }

        foreach (var key in keys)
        {
            if (!map.ContainsKey(key))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPropertyMap(object? value, out IReadOnlyDictionary<string, object?> map)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        map = result;

        if (value == null)
        {
            return false;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var item in readOnlyDictionary)
            {
                result[item.Key] = item.Value;
            }
            return result.Count > 0;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            foreach (var item in dictionary)
            {
                result[item.Key] = item.Value;
            }
            return result.Count > 0;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in element.EnumerateObject())
            {
                result[item.Name] = item.Value;
            }
            return result.Count > 0;
        }

        var properties = value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var property in properties)
        {
            if (property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            result[property.Name] = property.GetValue(value);
        }

        return result.Count > 0;
    }

    private static bool TryGetString(IReadOnlyDictionary<string, object?> map, string key, out string? value)
    {
        value = null;
        if (!map.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        value = ToStringSafe(raw);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt(IReadOnlyDictionary<string, object?> map, string key, out int value)
    {
        value = 0;
        if (!map.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        if (raw is int intValue)
        {
            value = intValue;
            return true;
        }

        if (raw is long longValue)
        {
            value = (int)longValue;
            return true;
        }

        if (raw is JsonElement element && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var jsonValue))
        {
            value = jsonValue;
            return true;
        }

        return int.TryParse(raw.ToString(), out value);
    }

    private static string? ToStringSafe(object raw)
    {
        if (raw is string str)
        {
            return str;
        }

        if (raw is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => element.ToString()
            };
        }

        return raw.ToString();
    }

    private static string GetDefaultMessage(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status423Locked => "Locked",
            StatusCodes.Status429TooManyRequests => "Too many requests",
            _ => "Request failed"
        };
    }

    private static string GetDefaultCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "BAD_REQUEST",
            StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
            StatusCodes.Status403Forbidden => "FORBIDDEN",
            StatusCodes.Status404NotFound => "NOT_FOUND",
            StatusCodes.Status409Conflict => "CONFLICT",
            StatusCodes.Status423Locked => "LOCKED",
            StatusCodes.Status429TooManyRequests => "RATE_LIMITED",
            StatusCodes.Status500InternalServerError => "INTERNAL_ERROR",
            StatusCodes.Status502BadGateway => "BAD_GATEWAY",
            StatusCodes.Status503ServiceUnavailable => "SERVICE_UNAVAILABLE",
            StatusCodes.Status504GatewayTimeout => "TIMEOUT",
            _ => "REQUEST_FAILED"
        };
    }
}
