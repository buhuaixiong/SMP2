using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public static class NotificationType
{
    public const string DocumentExpiring = "document_expiring";
    public const string DocumentExpired = "document_expired";
    public const string DocumentRenewalReminder = "document_renewal_reminder";
    public const string ProfileIncomplete = "profile_incomplete";
    public const string RfqPendingProcessing = "rfq_pending_processing";
}

public static class NotificationPriority
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Urgent = "urgent";
}

public sealed class NotificationService
{
    private readonly SupplierSystemDbContext _dbContext;

    public NotificationService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int?> CreateSupplierNotificationAsync(
        int supplierId,
        string type,
        string title,
        string message,
        string priority,
        string? relatedEntityType,
        int? relatedEntityId,
        DateTimeOffset? expiresAt,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var existing = await FindRecentNotificationAsync(
            supplierId: supplierId,
            userId: null,
            type: type,
            relatedEntityType: relatedEntityType,
            relatedEntityId: relatedEntityId,
            cancellationToken: cancellationToken);

        if (existing != null)
        {
            return existing.Id;
        }

        var now = DateTimeOffset.UtcNow;
        var notification = new Notification
        {
            SupplierId = supplierId,
            UserId = null,
            Type = type,
            Title = title,
            Message = message,
            Priority = priority,
            Status = "unread",
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedAt = now.ToString("o"),
            ExpiresAt = expiresAt?.ToString("o"),
            Metadata = metadata == null ? null : JsonSerializer.Serialize(metadata)
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return notification.Id;
    }

    public async Task<int?> CreateUserNotificationAsync(
        string userId,
        string type,
        string title,
        string message,
        string priority,
        string? relatedEntityType,
        int? relatedEntityId,
        DateTimeOffset? expiresAt,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var existing = await FindRecentNotificationAsync(
            supplierId: null,
            userId: userId,
            type: type,
            relatedEntityType: relatedEntityType,
            relatedEntityId: relatedEntityId,
            cancellationToken: cancellationToken);

        if (existing != null)
        {
            return existing.Id;
        }

        var now = DateTimeOffset.UtcNow;
        var notification = new Notification
        {
            SupplierId = null,
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Priority = priority,
            Status = "unread",
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedAt = now.ToString("o"),
            ExpiresAt = expiresAt?.ToString("o"),
            Metadata = metadata == null ? null : JsonSerializer.Serialize(metadata)
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return notification.Id;
    }

    private async Task<Notification?> FindRecentNotificationAsync(
        int? supplierId,
        string? userId,
        string type,
        string? relatedEntityType,
        int? relatedEntityId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Notifications.AsNoTracking()
            .Where(n => n.Status == "unread" && n.Type == type);

        if (supplierId.HasValue)
        {
            query = query.Where(n => n.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(n => n.UserId == userId);
        }

        if (relatedEntityType != null)
        {
            query = query.Where(n => n.RelatedEntityType == relatedEntityType);
        }

        if (relatedEntityId.HasValue)
        {
            query = query.Where(n => n.RelatedEntityId == relatedEntityId.Value);
        }

        var candidate = await query.OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.CreatedAt))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(candidate.CreatedAt, out var createdAt))
        {
            return null;
        }

        return createdAt >= DateTimeOffset.UtcNow.AddDays(-7) ? candidate : null;
    }
}
