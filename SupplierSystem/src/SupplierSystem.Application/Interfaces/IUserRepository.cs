using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken);
    Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken);
    Task<bool> SetMustChangePasswordAsync(string id, bool mustChangePassword, CancellationToken cancellationToken);
}
