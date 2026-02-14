using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Reminders;

public sealed class ReminderQueueService
{
    private static readonly JsonSerializerOptions ReminderJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ContractReminderSettings DefaultReminderSettings = new()
    {
        LeadDays = new List<int> { 7, 30 },
        Channels = new List<string> { "inbox", "email" },
        AutoNotify = true,
        RemindExpired = true
    };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly ReminderNotifier _notifier;
    private readonly ILogger<ReminderQueueService> _logger;

    public ReminderQueueService(
        SupplierSystemDbContext dbContext,
        ReminderNotifier notifier,
        ILogger<ReminderQueueService> logger)
    {
        _dbContext = dbContext;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<int> ProcessDocumentExpirationsAsync(
        DateTimeOffset now,
        IReadOnlyList<int> reminderWindowsDays,
        int horizonDays,
        CancellationToken cancellationToken)
    {
        var horizon = now.AddDays(Math.Max(1, horizonDays));
        var documents = await _dbContext.SupplierDocuments.AsNoTracking()
            .Where(d => !string.IsNullOrWhiteSpace(d.ExpiresAt))
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var doc in documents)
        {
            if (!TryParseDate(doc.ExpiresAt, out var expiresAt))
            {
                continue;
            }

            if (expiresAt < now || expiresAt > horizon)
            {
                continue;
            }

            foreach (var days in reminderWindowsDays.Where(value => value > 0))
            {
                var reminderDate = expiresAt.AddDays(-days);
                if (reminderDate > now)
                {
                    continue;
                }

                var type = $"document-expiry-{days}";
                var exists = await ReminderExistsAsync(type, "supplier_document", doc.Id.ToString(), expiresAt, cancellationToken);
                if (exists)
                {
                    continue;
                }

                var payload = JsonSerializer.Serialize(new
                {
                    id = doc.Id,
                    supplierId = doc.SupplierId,
                    docType = doc.DocType,
                    expiresAt = expiresAt.ToString("o"),
                    status = doc.Status,
                    originalName = doc.OriginalName
                });

                _dbContext.ReminderQueues.Add(new ReminderQueue
                {
                    Type = type,
                    EntityType = "supplier_document",
                    EntityId = doc.Id.ToString(),
                    DueAt = expiresAt.ToString("o"),
                    Status = "pending",
                    Payload = payload,
                    CreatedAt = now.ToString("o")
                });
                created++;
            }
        }

        if (created > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    public async Task<int> ProcessContractExpirationsAsync(
        DateTimeOffset now,
        int expiredLookbackDays,
        CancellationToken cancellationToken)
    {
        var settings = await LoadReminderSettingsAsync(cancellationToken);
        if (!settings.AutoNotify)
        {
            return 0;
        }
        var leadDays = settings.LeadDays;
        var horizon = leadDays.Count > 0 ? leadDays.Max() : 30;
        var lookback = Math.Max(1, expiredLookbackDays);

        var lowerBound = now.Date.AddDays(-lookback);
        var upperBound = now.Date.AddDays(horizon);

        var contracts = await (from contract in _dbContext.Contracts.AsNoTracking()
                               join supplier in _dbContext.Suppliers.AsNoTracking()
                                   on contract.SupplierId equals supplier.Id
                               where contract.IsMandatory && contract.EffectiveTo != null
                               select new ContractReminderEntry(contract, supplier.CompanyName ?? string.Empty))
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var entry in contracts)
        {
            if (!TryParseDate(entry.Contract.EffectiveTo, out var effectiveTo))
            {
                continue;
            }

            if (effectiveTo.Date < lowerBound || effectiveTo.Date > upperBound)
            {
                continue;
            }

            var diffDays = (int)Math.Ceiling((effectiveTo.Date - now.Date).TotalDays);
            if (diffDays < 0)
            {
                if (!settings.RemindExpired)
                {
                    continue;
                }

                var type = "contract-expired";
                if (await ReminderExistsAsync(type, "contract", entry.Contract.Id.ToString(), effectiveTo, cancellationToken))
                {
                    continue;
                }

                _dbContext.ReminderQueues.Add(BuildContractReminder(entry, type, effectiveTo, now, diffDays));
                created++;
                continue;
            }

            foreach (var lead in leadDays.OrderBy(value => value))
            {
                if (diffDays > lead)
                {
                    continue;
                }

                var type = $"contract-expiry-{lead}";
                if (await ReminderExistsAsync(type, "contract", entry.Contract.Id.ToString(), effectiveTo, cancellationToken))
                {
                    break;
                }

                _dbContext.ReminderQueues.Add(BuildContractReminder(entry, type, effectiveTo, now, diffDays));
                created++;
                break;
            }
        }

        if (created > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    public async Task<int> DispatchPendingRemindersAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var pending = await _dbContext.ReminderQueues
            .Where(item => item.Status == "pending" && !string.IsNullOrWhiteSpace(item.DueAt))
            .OrderBy(item => item.DueAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var sentCount = 0;
        foreach (var item in pending)
        {
            if (!IsDue(item.DueAt, now))
            {
                continue;
            }

            try
            {
                await _notifier.NotifyAsync(item, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Reminder] Dispatch failed.");
            }

            item.Status = "sent";
            item.SentAt = now.ToString("o");
            sentCount++;
        }

        if (sentCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return sentCount;
    }

    private async Task<ContractReminderSettings> LoadReminderSettingsAsync(CancellationToken cancellationToken)
    {
        var row = await _dbContext.ContractReminderSettings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Scope == "global", cancellationToken);

        if (row == null || string.IsNullOrWhiteSpace(row.Settings))
        {
            return CloneSettings(DefaultReminderSettings);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ContractReminderSettings>(row.Settings, ReminderJsonOptions)
                         ?? CloneSettings(DefaultReminderSettings);
            return NormalizeReminderSettings(parsed);
        }
        catch (JsonException)
        {
            return CloneSettings(DefaultReminderSettings);
        }
    }

    private static ContractReminderSettings NormalizeReminderSettings(ContractReminderSettings input)
    {
        return new ContractReminderSettings
        {
            LeadDays = NormalizeLeadDays(input.LeadDays),
            Channels = NormalizeChannels(input.Channels),
            AutoNotify = input.AutoNotify,
            RemindExpired = input.RemindExpired
        };
    }

    private static List<int> NormalizeLeadDays(IEnumerable<int>? input)
    {
        if (input == null)
        {
            return new List<int>(DefaultReminderSettings.LeadDays);
        }

        var values = input
            .Select(value => Math.Abs(value))
            .Where(value => value > 0 && value <= 365)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        return values.Count > 0 ? values : new List<int>(DefaultReminderSettings.LeadDays);
    }

    private static List<string> NormalizeChannels(IEnumerable<string>? input)
    {
        if (input == null)
        {
            return new List<string>(DefaultReminderSettings.Channels);
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inbox",
            "email",
            "teams"
        };

        var values = input
            .Select(value => value?.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value) && allowed.Contains(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count > 0 ? values : new List<string>(DefaultReminderSettings.Channels);
    }

    private static ContractReminderSettings CloneSettings(ContractReminderSettings source)
    {
        return new ContractReminderSettings
        {
            LeadDays = new List<int>(source.LeadDays),
            Channels = new List<string>(source.Channels),
            AutoNotify = source.AutoNotify,
            RemindExpired = source.RemindExpired
        };
    }

    private static ReminderQueue BuildContractReminder(
        ContractReminderEntry entry,
        string type,
        DateTimeOffset effectiveTo,
        DateTimeOffset now,
        int diffDays)
    {
        var contract = entry.Contract;
        var payload = JsonSerializer.Serialize(new
        {
            id = contract.Id,
            supplierId = contract.SupplierId,
            title = contract.Title,
            agreementNumber = contract.AgreementNumber,
            amount = contract.Amount,
            currency = contract.Currency,
            status = contract.Status,
            paymentCycle = contract.PaymentCycle,
            effectiveFrom = contract.EffectiveFrom,
            effectiveTo = effectiveTo.ToString("o"),
            autoRenew = contract.AutoRenew,
            supplierName = entry.SupplierName,
            daysRemaining = diffDays
        });

        return new ReminderQueue
        {
            Type = type,
            EntityType = "contract",
            EntityId = contract.Id.ToString(),
            DueAt = effectiveTo.ToString("o"),
            Status = "pending",
            Payload = payload,
            CreatedAt = now.ToString("o")
        };
    }

    private async Task<bool> ReminderExistsAsync(
        string type,
        string entityType,
        string entityId,
        DateTimeOffset dueAt,
        CancellationToken cancellationToken)
    {
        var due = dueAt.ToString("o");
        return await _dbContext.ReminderQueues.AsNoTracking()
            .AnyAsync(item =>
                item.Type == type &&
                item.EntityType == entityType &&
                item.EntityId == entityId &&
                item.DueAt == due,
                cancellationToken);
    }

    private static bool TryParseDate(string? value, out DateTimeOffset parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = default;
            return false;
        }

        return DateTimeOffset.TryParse(value, out parsed);
    }

    private static bool IsDue(string? dueAt, DateTimeOffset now)
    {
        return DateTimeOffset.TryParse(dueAt, out var due) && due <= now;
    }

    private sealed class ContractReminderSettings
    {
        public List<int> LeadDays { get; set; } = new();
        public List<string> Channels { get; set; } = new();
        public bool AutoNotify { get; set; }
        public bool RemindExpired { get; set; }
    }

    private sealed record ContractReminderEntry(Contract Contract, string SupplierName);
}
