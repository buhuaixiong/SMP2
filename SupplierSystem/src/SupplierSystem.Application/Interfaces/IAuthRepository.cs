using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Application.Interfaces;

public interface IAuthRepository
{
    Task<User?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken);
    Task<User?> FindByIdAsync(string id, CancellationToken cancellationToken);
}
