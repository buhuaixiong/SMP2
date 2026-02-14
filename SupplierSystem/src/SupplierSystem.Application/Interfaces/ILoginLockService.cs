namespace SupplierSystem.Application.Interfaces;

public interface ILoginLockService
{
    bool TryAcquire(string userId);
    void Release(string userId);
}
