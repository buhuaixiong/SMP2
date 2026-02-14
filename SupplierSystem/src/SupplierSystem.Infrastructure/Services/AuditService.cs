using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IServiceScopeFactory scopeFactory, ILogger<AuditService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task LogAsync(AuditEntry entry)
    {
        if (entry == null)
        {
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SupplierSystemDbContext>();
                var archiveService = scope.ServiceProvider.GetRequiredService<IAuditArchiveService>();

                var isSensitive = AuditSensitivity.IsSensitiveAction(entry.Action, entry.EntityType);
                var immutable = isSensitive;

                var log = new AuditLog
                {
                    ActorId = entry.ActorId,
                    ActorName = entry.ActorName,
                    EntityType = entry.EntityType,
                    EntityId = entry.EntityId,
                    Action = entry.Action,
                    Changes = entry.Changes,
                    Summary = entry.Summary,
                    IpAddress = entry.IpAddress,
                    IsSensitive = isSensitive || entry.IsSensitive,
                    Immutable = immutable || entry.Immutable,
                    CreatedAt = DateTime.UtcNow,
                };

                if (log.IsSensitive)
                {
                    var previousHash = await archiveService.GetPreviousHashAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    log.HashChainValue = archiveService.GenerateHashChainValue(log, previousHash);
                }

                dbContext.AuditLogs.Add(log);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);

                if (log.IsSensitive)
                {
                    await archiveService.ArchiveAsync(log, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log write failed.");
            }
        });

        return Task.CompletedTask;
    }
}
