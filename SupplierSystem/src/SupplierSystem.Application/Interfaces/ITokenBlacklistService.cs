namespace SupplierSystem.Application.Interfaces;

public interface ITokenBlacklistService
{
    Task<string?> GetReasonAsync(string token);
    Task<bool> IsBlacklistedAsync(string token);
    Task<bool> AddAsync(string token, string? userId, string reason);
    Task<bool> AddTokenHashAsync(string tokenHash, string userId, string expiresAt, string reason);
    Task<int> RemoveExpiredAsync();
}
