using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Registrations;

public sealed partial class SupplierRegistrationService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SupplierRegistrationService> _logger;

    public SupplierRegistrationService(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<SupplierRegistrationService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _environment = environment;
        _logger = logger;
    }
}
