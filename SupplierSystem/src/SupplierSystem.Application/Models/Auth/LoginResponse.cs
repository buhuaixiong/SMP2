namespace SupplierSystem.Application.Models.Auth;

public sealed class LoginResponse
{
    public string Token { get; set; } = null!;
    public long? ExpiresIn { get; set; }
    public string ExpiresInReadable { get; set; } = null!;
    public AuthUser User { get; set; } = null!;
    public bool MustChangePassword { get; set; }
    public int ActiveSessionCount { get; set; }
    public SessionInfo? SessionInfo { get; set; }
}

public sealed class SessionInfo
{
    public int Invalidated { get; set; }
    public string? Reason { get; set; }
}
