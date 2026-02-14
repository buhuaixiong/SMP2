using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Common;

namespace SupplierSystem.Api.Controllers;

/// <summary>
/// 统一 API 控制器基类
/// 提供标准化的响应格式和方法
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// 成功响应 (200 OK)
    /// </summary>
    protected IActionResult Success(object? data = null, string? message = null)
    {
        return Ok(new ApiResponse<object?> { Data = data, Message = message });
    }

    /// <summary>
    /// 成功响应 (200 OK) - 泛型版
    /// </summary>
    protected IActionResult Success<T>(T data, string? message = null)
    {
        return Ok(new ApiResponse<T> { Data = data, Message = message });
    }

    /// <summary>
    /// 已创建 (201 Created)
    /// </summary>
    protected IActionResult Created(object? data = null, string? message = "Created successfully")
    {
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<object?> { Data = data, Message = message });
    }

    /// <summary>
    /// 已创建 (201 Created) - 泛型版
    /// </summary>
    protected IActionResult Created<T>(T data, string? message = "Created successfully")
    {
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<T> { Data = data, Message = message });
    }

    /// <summary>
    /// 无内容 (204 No Content)
    /// </summary>
    protected IActionResult NoContent(string? message = null)
    {
        return StatusCode(StatusCodes.Status204NoContent, message == null ? null : new ApiResponse { Message = message });
    }

    /// <summary>
    /// 错误响应 (400 Bad Request)
    /// </summary>
    protected IActionResult BadRequest(string message, string? code = "BAD_REQUEST", object? details = null)
    {
        return BadRequest(new ApiErrorResponse { Error = message, Code = code, Details = details });
    }

    /// <summary>
    /// 未授权 (401 Unauthorized)
    /// </summary>
    protected IActionResult Unauthorized(string message = "Authentication required", string? code = "UNAUTHORIZED")
    {
        return StatusCode(StatusCodes.Status401Unauthorized, new ApiErrorResponse { Error = message, Code = code });
    }

    /// <summary>
    /// 禁止访问 (403 Forbidden)
    /// </summary>
    protected IActionResult Forbidden(string message = "Access denied", string? code = "FORBIDDEN")
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse { Error = message, Code = code });
    }

    /// <summary>
    /// 资源不存在 (404 Not Found)
    /// </summary>
    protected IActionResult NotFound(string message = "Resource not found", string? code = "NOT_FOUND")
    {
        return StatusCode(StatusCodes.Status404NotFound, new ApiErrorResponse { Error = message, Code = code });
    }

    /// <summary>
    /// 冲突 (409 Conflict)
    /// </summary>
    protected IActionResult Conflict(string message, string? code = "CONFLICT", object? details = null)
    {
        return StatusCode(StatusCodes.Status409Conflict, new ApiErrorResponse { Error = message, Code = code, Details = details });
    }

    /// <summary>
    /// 锁定 (423 Locked) - 用于账户锁定
    /// </summary>
    protected IActionResult Locked(string message, object? details = null)
    {
        return StatusCode(StatusCodes.Status423Locked, new ApiErrorResponse { Error = message, Code = "ACCOUNT_LOCKED", Details = details });
    }

    /// <summary>
    /// 太多请求 (429 Too Many Requests)
    /// </summary>
    protected IActionResult TooManyRequests(string message = "Too many requests", object? details = null, int? retryAfter = null)
    {
        var response = new ApiErrorResponse { Error = message, Code = "RATE_LIMITED", Details = details };
        if (retryAfter.HasValue)
        {
            Response.Headers["Retry-After"] = retryAfter.Value.ToString();
        }
        return StatusCode(StatusCodes.Status429TooManyRequests, response);
    }

    /// <summary>
    /// 服务器错误 (500 Internal Server Error)
    /// </summary>
    protected IActionResult InternalError(string message = "Internal server error", string? code = "INTERNAL_ERROR")
    {
        return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse { Error = message, Code = code });
    }

    /// <summary>
    /// 抛出业务异常
    /// </summary>
    protected void Throw(int statusCode, string message, string? code = null, object? details = null)
    {
        throw new ServiceException(statusCode, message, code, details);
    }

    /// <summary>
    /// 抛出验证异常
    /// </summary>
    protected void ThrowValidation(string message, object? details = null)
    {
        throw new ValidationException(message, details);
    }

    /// <summary>
    /// 抛出未找到异常
    /// </summary>
    protected void ThrowNotFound(string message = "Resource not found")
    {
        throw new ServiceException(StatusCodes.Status404NotFound, message, "NOT_FOUND");
    }

    /// <summary>
    /// 抛出未授权异常
    /// </summary>
    protected void ThrowUnauthorized(string message = "Authentication required")
    {
        throw new ServiceException(StatusCodes.Status401Unauthorized, message, "UNAUTHORIZED");
    }

    /// <summary>
    /// 抛出禁止访问异常
    /// </summary>
    protected void ThrowForbidden(string message = "Access denied")
    {
        throw new ServiceException(StatusCodes.Status403Forbidden, message, "FORBIDDEN");
    }
}
