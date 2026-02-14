using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : NodeControllerBase
{
    private static readonly string[] StaffPermissions =
    {
        Permissions.PurchaserSegmentManage,
        Permissions.ProcurementManagerUpgradeApprove,
        Permissions.AdminRoleManage,
    };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly NotificationService _notificationService;

    public NotificationsController(
        SupplierSystemDbContext dbContext,
        NotificationService notificationService,
        IWebHostEnvironment environment) : base(environment)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var status = Request.Query["status"].ToString();
        var limit = ParseInt(Request.Query["limit"], 50);
        var offset = ParseInt(Request.Query["offset"], 0);
        limit = Math.Max(1, Math.Min(200, limit));
        offset = Math.Max(0, offset);

        var query = _dbContext.Notifications.AsNoTracking().AsQueryable();
        if (user.SupplierId.HasValue)
        {
            query = query.Where(n => n.SupplierId == user.SupplierId.Value);
        }
        else
        {
            query = query.Where(n => n.UserId == user.Id);
        }

        if (string.Equals(status, "read", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(n => n.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var parsed = notifications.Select(ToResponse).ToList();

        var unreadQuery = _dbContext.Notifications.AsNoTracking().Where(n => n.Status == "unread");
        unreadQuery = user.SupplierId.HasValue
            ? unreadQuery.Where(n => n.SupplierId == user.SupplierId.Value)
            : unreadQuery.Where(n => n.UserId == user.Id);
        var unreadCount = await unreadQuery.CountAsync(cancellationToken);

        return Ok(new
        {
            data = parsed,
            pagination = new
            {
                total,
                limit,
                offset,
                hasMore = offset + limit < total,
            },
            meta = new
            {
                unreadCount,
            }
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var query = _dbContext.Notifications.AsNoTracking().Where(n => n.Status == "unread");
        query = user.SupplierId.HasValue
            ? query.Where(n => n.SupplierId == user.SupplierId.Value)
            : query.Where(n => n.UserId == user.Id);

        var count = await query.CountAsync(cancellationToken);
        return Ok(new { data = new { count } });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetNotification(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var notification = await _dbContext.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notification == null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        if (!CanAccessNotification(user, notification))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        return Ok(new { data = ToResponse(notification) });
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notification == null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        if (!CanAccessNotification(user, notification))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        notification.Status = "read";
        notification.ReadAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { data = ToResponse(notification) });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var query = _dbContext.Notifications.Where(n => n.Status == "unread");
        query = user.SupplierId.HasValue
            ? query.Where(n => n.SupplierId == user.SupplierId.Value)
            : query.Where(n => n.UserId == user.Id);

        var notifications = await query.ToListAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            notification.Status = "read";
            notification.ReadAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "All notifications marked as read",
            data = new { updated = notifications.Count }
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notification == null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        if (!CanAccessNotification(user, notification))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        _dbContext.Notifications.Remove(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var supplierId = ReadInt(body, "supplierId", "supplier_id");
        var type = ReadString(body, "type");
        var title = ReadString(body, "title");
        var message = ReadString(body, "message");
        var priority = ReadString(body, "priority") ?? NotificationPriority.Normal;
        var relatedEntityType = ReadString(body, "relatedEntityType", "related_entity_type");
        var relatedEntityId = ReadInt(body, "relatedEntityId", "related_entity_id");
        var metadata = body.TryGetProperty("metadata", out var metadataElement) ? metadataElement : (JsonElement?)null;

        if (!supplierId.HasValue || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { message = "Missing required fields." });
        }

        var createdId = await _notificationService.CreateSupplierNotificationAsync(
            supplierId.Value,
            type!,
            title!,
            message!,
            priority,
            relatedEntityType,
            relatedEntityId,
            null,
            metadata?.Clone(),
            cancellationToken);

        if (!createdId.HasValue)
        {
            return StatusCode(500, new { message = "Failed to create notification." });
        }

        var created = await _dbContext.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == createdId.Value, cancellationToken);

        return StatusCode(201, new { data = created == null ? null : ToResponse(created) });
    }

    private static bool CanAccessNotification(AuthUser user, Notification notification)
    {
        var hasStaffPermission = user.Permissions != null && user.Permissions.Any(p => StaffPermissions.Contains(p));
        if (hasStaffPermission)
        {
            return true;
        }

        if (notification.SupplierId.HasValue)
        {
            return user.SupplierId.HasValue && user.SupplierId.Value == notification.SupplierId.Value;
        }

        if (!string.IsNullOrWhiteSpace(notification.UserId))
        {
            return string.Equals(user.Id, notification.UserId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static object ToResponse(Notification notification)
    {
        return new
        {
            notification.Id,
            notification.SupplierId,
            notification.UserId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.Priority,
            notification.Status,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.CreatedAt,
            notification.ReadAt,
            notification.ExpiresAt,
            metadata = ParseMetadata(notification.Metadata),
        };
    }

    private static object ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static string? ReadString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    private static int? ReadInt(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }
        }

        return null;
    }

    private static IActionResult? RequirePermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.All(granted.Contains))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
