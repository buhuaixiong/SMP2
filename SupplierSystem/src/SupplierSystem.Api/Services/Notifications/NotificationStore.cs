using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Notifications;

public sealed class NotificationStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public NotificationStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<Notification> QueryNotifications()
    {
        return _dbContext.Notifications.AsNoTracking();
    }

    public IQueryable<Notification> QueryUnreadNotifications()
    {
        return _dbContext.Notifications.AsNoTracking().Where(n => n.Status == "unread");
    }

    public Task<Notification?> FindNotificationAsync(int id, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.Notifications.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public Task<List<Notification>> LoadUnreadNotificationsForUserAsync(AuthScope scope, CancellationToken cancellationToken)
    {
        var query = _dbContext.Notifications.Where(n => n.Status == "unread");
        query = scope.SupplierId.HasValue
            ? query.Where(n => n.SupplierId == scope.SupplierId.Value)
            : query.Where(n => n.UserId == scope.UserId);

        return query.ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(AuthScope scope, CancellationToken cancellationToken)
    {
        var query = QueryUnreadNotifications();
        query = scope.SupplierId.HasValue
            ? query.Where(n => n.SupplierId == scope.SupplierId.Value)
            : query.Where(n => n.UserId == scope.UserId);

        return query.CountAsync(cancellationToken);
    }

    public void RemoveNotification(Notification notification)
    {
        _dbContext.Notifications.Remove(notification);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public readonly record struct AuthScope(string UserId, int? SupplierId);
