using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace SupplierSystem.Api.Helpers;

/// <summary>
/// Shared helper methods for exception handling and response formatting.
/// </summary>
public static class ExceptionHelper
{
    /// <summary>
    /// Extract property dictionary from an object.
    /// </summary>
    public static bool TryGetPropertyMap(object value, out IReadOnlyDictionary<string, object?> map)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        map = result;

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var item in readOnlyDictionary)
                result[item.Key] = item.Value;
            return result.Count > 0;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            foreach (var item in dictionary)
                result[item.Key] = item.Value;
            return result.Count > 0;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in element.EnumerateObject())
                result[item.Name] = item.Value;
            return result.Count > 0;
        }

        var properties = value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var property in properties)
        {
            if (property.GetIndexParameters().Length != 0) continue;
            try
            {
                result[property.Name] = property.GetValue(value);
            }
            catch
            {
                // Ignore property access exceptions
            }
        }

        return result.Count > 0;
    }

    /// <summary>
    /// Get string value from dictionary.
    /// </summary>
    public static bool TryGetString(IReadOnlyDictionary<string, object?> map, string key, out string? value)
    {
        value = null;
        if (!map.TryGetValue(key, out var raw) || raw == null) return false;

        value = raw is JsonElement element ? element.ToString() : raw.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Get default message for HTTP status code.
    /// </summary>
    public static string GetDefaultMessage(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad request",
            StatusCodes.Status401Unauthorized => "Authentication required",
            StatusCodes.Status403Forbidden => "Access denied",
            StatusCodes.Status404NotFound => "Resource not found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status423Locked => "Locked",
            StatusCodes.Status429TooManyRequests => "Too many requests. Please try again later.",
            StatusCodes.Status500InternalServerError => "An error occurred",
            StatusCodes.Status502BadGateway => "Bad gateway",
            StatusCodes.Status503ServiceUnavailable => "Service unavailable",
            StatusCodes.Status504GatewayTimeout => "Gateway timeout",
            _ => "An error occurred"
        };
    }

    /// <summary>
    /// Get default error code for HTTP status code.
    /// </summary>
    public static string GetDefaultCode(int statusCode)
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

    /// <summary>
    /// Extract message from exception value.
    /// </summary>
    public static string? ExtractMessage(object? value)
    {
        if (value == null) return null;

        if (value is string message) return message;

        if (value is ValidationProblemDetails validation)
            return validation.Title ?? "Validation failed";

        if (value is ProblemDetails problem)
            return problem.Title ?? problem.Detail;

        if (TryGetPropertyMap(value, out var map))
        {
            if (TryGetString(map, "message", out var messageValue))
                return messageValue;

            if (TryGetString(map, "error", out var errorValue))
                return errorValue;
        }

        return null;
    }

    /// <summary>
    /// Extract error code from exception value.
    /// </summary>
    public static string? ExtractCode(object? value)
    {
        if (value == null) return null;

        if (TryGetPropertyMap(value, out var map))
        {
            if (TryGetString(map, "code", out var codeValue))
                return codeValue;

            if (TryGetString(map, "errorCode", out var errorCodeValue))
                return errorCodeValue;
        }

        return null;
    }

    /// <summary>
    /// Extract details from exception value.
    /// </summary>
    public static object? ExtractDetails(object? value)
    {
        if (value == null) return null;

        if (value is ValidationProblemDetails validation)
            return validation.Errors;

        if (TryGetPropertyMap(value, out var map))
        {
            if (map.TryGetValue("details", out var detailsValue))
                return detailsValue;

            if (map.TryGetValue("errors", out var errorsValue))
                return errorsValue;
        }

        return null;
    }
}
