using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService : ISupplierService
{
    private readonly SupplierSystemDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<SupplierService> _logger;

    public SupplierService(SupplierSystemDbContext context, IAuditService auditService, ILogger<SupplierService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }
}
