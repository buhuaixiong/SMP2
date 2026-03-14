using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Repositories;

public abstract class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly SupplierSystemDbContext DbContext;
    protected readonly DbSet<T> DbSet;
    private readonly PropertyInfo? _softDeleteProperty;

    protected BaseRepository(SupplierSystemDbContext dbContext)
    {
        DbContext = dbContext;
        DbSet = dbContext.Set<T>();
        _softDeleteProperty = typeof(T).GetProperty("IsDeleted");
    }

    public virtual async Task<T?> FindByIdAsync(object id, CancellationToken cancellationToken)
    {
        if (id == null)
        {
            return null;
        }

        var entity = await DbSet.FindAsync([id], cancellationToken);
        if (entity != null)
        {
            DbContext.Entry(entity).State = EntityState.Detached;
        }

        return entity;
    }

    public virtual async Task<IReadOnlyList<T>> FindAllAsync(CancellationToken cancellationToken)
    {
        return await Query().ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> FindPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Max(1, pageSize);
        var offset = (normalizedPage - 1) * normalizedSize;

        return await Query()
            .Skip(offset)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return await Query().CountAsync(cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken)
    {
        if (id == null)
        {
            return false;
        }

        var entity = await DbSet.FindAsync([id], cancellationToken);
        if (entity == null)
        {
            return false;
        }

        DbContext.Entry(entity).State = EntityState.Detached;
        return true;
    }

    public virtual async Task<T> CreateAsync(T entity, CancellationToken cancellationToken)
    {
        DbSet.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        DbSet.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<bool> DeleteAsync(object id, CancellationToken cancellationToken)
    {
        var entity = await DbSet.FindAsync([id], cancellationToken);
        if (entity == null)
        {
            return false;
        }

        DbSet.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public virtual async Task<bool> SoftDeleteAsync(object id, CancellationToken cancellationToken)
    {
        if (_softDeleteProperty == null)
        {
            return await DeleteAsync(id, cancellationToken);
        }

        var entity = await DbSet.FindAsync([id], cancellationToken);
        if (entity == null)
        {
            return false;
        }

        if (_softDeleteProperty.PropertyType == typeof(bool))
        {
            _softDeleteProperty.SetValue(entity, true);
        }
        else if (_softDeleteProperty.PropertyType == typeof(bool?))
        {
            _softDeleteProperty.SetValue(entity, true);
        }
        else
        {
            return await DeleteAsync(id, cancellationToken);
        }

        DbSet.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    protected virtual IQueryable<T> Query()
    {
        var query = DbSet.AsNoTracking();
        if (_softDeleteProperty == null)
        {
            return query;
        }

        if (_softDeleteProperty.PropertyType == typeof(bool))
        {
            return query.Where(entity => !EF.Property<bool>(entity, _softDeleteProperty.Name));
        }

        if (_softDeleteProperty.PropertyType == typeof(bool?))
        {
            return query.Where(entity => EF.Property<bool?>(entity, _softDeleteProperty.Name) != true);
        }

        return query;
    }
}
