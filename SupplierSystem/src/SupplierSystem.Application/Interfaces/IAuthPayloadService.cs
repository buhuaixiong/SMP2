using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Application.Interfaces;

public interface IAuthPayloadService
{
    Task<AuthUser?> BuildAsync(string userId);
}
