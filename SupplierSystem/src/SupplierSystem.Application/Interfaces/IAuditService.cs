using SupplierSystem.Application.Models.Audit;

namespace SupplierSystem.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(AuditEntry entry);
}
