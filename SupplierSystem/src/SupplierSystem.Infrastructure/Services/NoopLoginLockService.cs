using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Infrastructure.Services;

public sealed class NoopLoginLockService : ILoginLockService
{
    public bool TryAcquire(string userId)
    {
        return true;
    }

    public void Release(string userId)
    {
    }
}
