using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Common;

namespace SupplierSystem.Api.Filters;

/// <summary>
/// 全局异常过滤器 - 统一处理所有异常
/// </summary>
public sealed class GlobalExceptionFilter : IAsyncExceptionFilter
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(IWebHostEnvironment environment, ILogger<GlobalExceptionFilter> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task OnExceptionAsync(ExceptionContext context)
    {
        var exception = context.Exception;
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        // 生产环境不输出堆栈信息，避免敏感信息泄露
        var includeStack = _environment.IsDevelopment();
        var (statusCode, response) = MapException(exception, includeStack);

        // 429 需要添加 Retry-After 头
        if (statusCode == StatusCodes.Status429TooManyRequests)
        {
            context.HttpContext.Response.Headers["Retry-After"] = "60";
        }

        context.Result = new ObjectResult(response) { StatusCode = statusCode };
        context.ExceptionHandled = true;
        return Task.CompletedTask;
    }

    private static (int, ApiErrorResponse) MapException(Exception exception, bool includeStack)
    {
        ApiErrorResponse response;
        int statusCode;

        switch (exception)
        {
            case ValidationException validationException:
                statusCode = validationException.StatusCode;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = validationException.Message,
                    Code = validationException.ErrorCode,
                    Details = validationException.Details
                };
                break;

            case NotFoundException notFoundException:
                statusCode = notFoundException.StatusCode;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = notFoundException.Message,
                    Code = notFoundException.ErrorCode
                };
                break;

            case UnauthorizedException unauthorizedException:
                statusCode = unauthorizedException.StatusCode;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = unauthorizedException.Message,
                    Code = unauthorizedException.ErrorCode
                };
                break;

            case ForbiddenException forbiddenException:
                statusCode = forbiddenException.StatusCode;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = forbiddenException.Message,
                    Code = forbiddenException.ErrorCode
                };
                break;

            case ConflictException conflictException:
                statusCode = conflictException.StatusCode;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = conflictException.Message,
                    Code = conflictException.ErrorCode,
                    Details = conflictException.Details
                };
                break;

            case LockedException lockedException:
                statusCode = lockedException.StatusCode;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = lockedException.Message,
                    Code = lockedException.ErrorCode,
                    Details = lockedException.Details
                };
                break;

            case ServiceException serviceException:
                statusCode = serviceException.StatusCode;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = serviceException.Message,
                    Code = serviceException.ErrorCode,
                    Details = serviceException.Details
                };
                break;

            case HttpResponseException httpResponse:
                (statusCode, response) = MapHttpResponse(httpResponse);
                break;

            case DbUpdateException:
                statusCode = StatusCodes.Status409Conflict;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = "Database constraint violation",
                    Code = "CONSTRAINT_ERROR"
                };
                break;

            case UnauthorizedAccessException:
                statusCode = StatusCodes.Status401Unauthorized;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = "Unauthorized",
                    Code = "UNAUTHORIZED"
                };
                break;

            case ArgumentNullException:
                statusCode = StatusCodes.Status400BadRequest;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = "Invalid argument provided",
                    Code = "BAD_REQUEST"
                };
                break;

            case ArgumentException:
                statusCode = StatusCodes.Status400BadRequest;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = "Invalid argument provided",
                    Code = "BAD_REQUEST"
                };
                break;

            case InvalidOperationException:
                statusCode = StatusCodes.Status400BadRequest;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = "Invalid operation",
                    Code = "BAD_REQUEST"
                };
                break;

            case KeyNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = "Resource not found",
                    Code = "NOT_FOUND"
                };
                break;

            case TimeoutException:
                statusCode = StatusCodes.Status504GatewayTimeout;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = "Request timed out",
                    Code = "TIMEOUT"
                };
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                response = new ApiErrorResponse
                {
                    Success = false,
                    Error = includeStack ? exception.Message : "An error occurred processing your request.",
                    Code = "INTERNAL_ERROR"
                };
                break;
        }

        // 只有开发环境才添加堆栈信息
        if (includeStack && exception.StackTrace != null)
        {
            response.Stack = exception.StackTrace;
        }

        response.Error ??= ExceptionHelper.GetDefaultMessage(statusCode);
        response.Code ??= ExceptionHelper.GetDefaultCode(statusCode);

        return (statusCode, response);
    }

    private static (int, ApiErrorResponse) MapHttpResponse(HttpResponseException exception)
    {
        var statusCode = exception.Status;
        var error = ExceptionHelper.ExtractMessage(exception.Value)
            ?? exception.Message
            ?? ExceptionHelper.GetDefaultMessage(statusCode);
        var code = ExceptionHelper.ExtractCode(exception.Value)
            ?? ExceptionHelper.GetDefaultCode(statusCode);
        var details = ExceptionHelper.ExtractDetails(exception.Value);

        var response = new ApiErrorResponse
        {
            Success = false,
            Error = error,
            Code = code,
            Details = details
        };

        return (statusCode, response);
    }
}
