namespace SupplierSystem.Application.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> FindByIdAsync(object id, CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> FindAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> FindPagedAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<int> CountAsync(CancellationToken cancellationToken);
    Task<bool> ExistsAsync(object id, CancellationToken cancellationToken);
    Task<T> CreateAsync(T entity, CancellationToken cancellationToken);
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(object id, CancellationToken cancellationToken);
    Task<bool> SoftDeleteAsync(object id, CancellationToken cancellationToken);
}
