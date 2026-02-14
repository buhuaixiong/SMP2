namespace SupplierSystem.Application.Interfaces;

public interface ISessionService
{
    Task RegisterAsync(string token, string userId, string? ipAddress, string? userAgent);
    Task<SessionInvalidationResult> InvalidateUserSessionsAsync(string userId, string reason, string? currentIp, string? currentUserAgent);
    Task<SessionInvalidationResult> InvalidateAllSessionsAsync(string userId, string reason);
    Task<bool> RemoveAsync(string token);
    Task<int> CountActiveAsync(string userId);
    Task<int> InvalidateAllOtherSessionsAsync(string userId, string currentTokenHash);
    string HashToken(string token);
}

public sealed class SessionInvalidationResult
{
    public int Invalidated { get; set; }
    public int Skipped { get; set; }
    public int Total { get; set; }
}
