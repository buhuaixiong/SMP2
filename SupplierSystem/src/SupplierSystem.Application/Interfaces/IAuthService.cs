using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string? userAgent);
    Task<AuthUser?> GetCurrentUserAsync(string userId);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task LogoutAsync(string token, AuthUser user);
}
