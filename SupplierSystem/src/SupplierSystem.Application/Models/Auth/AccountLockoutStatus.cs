namespace SupplierSystem.Application.Models.Auth;

public sealed class AccountLockoutStatus
{
    public bool Locked { get; set; }
    public int RemainingTimeMinutes { get; set; }
    public int Attempts { get; set; }
}

public sealed class FailedAttemptResult
{
    public bool Locked { get; set; }
    public int RemainingAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}

public sealed class LockoutStats
{
    public int TotalAccounts { get; set; }
    public int CurrentlyLocked { get; set; }
    public int AccountsWithFailures { get; set; }
    public int MaxFailedAttempts { get; set; }
    public int LockoutDurationMinutes { get; set; }
}
