using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Application.Interfaces;

public interface IAccountLockoutService
{
    AccountLockoutStatus Check(string username);
    FailedAttemptResult RecordFailedAttempt(string username, string? ipAddress);
    void Reset(string username);
    bool Unlock(string username, string? adminId);
    LockoutStats GetStats();
}
