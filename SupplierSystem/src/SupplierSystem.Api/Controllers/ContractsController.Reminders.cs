using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Controllers;

public sealed partial class ContractsController
{
    private static readonly JsonSerializerOptions ReminderJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ContractReminderSettingsDto DefaultReminderSettings = new()
    {
        LeadDays = [7, 30],
        Channels = ["inbox", "email"],
        AutoNotify = true,
        RemindExpired = true
    };

    [HttpGet("reminders/summary")]
    public async Task<IActionResult> GetReminderSummary(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: false);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var settings = await LoadReminderSettingsAsync(cancellationToken);
        var items = await ListMandatoryContractsForRemindersAsync(settings, DateTime.UtcNow, cancellationToken);
        var summary = BuildReminderSummary(items, settings);
        return Ok(summary);
    }

    [HttpGet("reminders")]
    public async Task<IActionResult> ListReminders(
        [FromQuery] string? bucket,
        [FromQuery] int? limit,
        [FromQuery] string? asOf,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: false);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var snapshot = DateTime.UtcNow;
        if (TryParseDate(asOf, out var parsed))
        {
            snapshot = parsed;
        }

        var settings = await LoadReminderSettingsAsync(cancellationToken);
        var items = await ListMandatoryContractsForRemindersAsync(settings, snapshot, cancellationToken);
        if (!string.IsNullOrWhiteSpace(bucket))
        {
            items = items.Where(item => string.Equals(item.Bucket, bucket, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var response = new ReminderListResponse
        {
            Settings = settings,
            Total = items.Count,
            Items = items
        };

        if (limit.HasValue && limit.Value > 0)
        {
            response.Items = items.Take(limit.Value).ToList();
        }

        return Ok(response);
    }

    [HttpGet("reminders/settings")]
    public async Task<IActionResult> GetReminderSettings(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: false);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var settings = await LoadReminderSettingsAsync(cancellationToken);
        return Ok(new
        {
            settings,
            defaults = DefaultReminderSettings
        });
    }

    [HttpPut("reminders/settings")]
    public async Task<IActionResult> UpdateReminderSettings(
        [FromBody] ContractReminderSettingsUpdateRequest payload,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: false);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var current = await LoadReminderSettingsAsync(cancellationToken);
        var next = MergeReminderSettings(current, payload);
        var row = await _dbContext.ContractReminderSettings
            .FirstOrDefaultAsync(r => r.Scope == "global", cancellationToken);

        var now = DateTime.UtcNow.ToString("o");
        var actor = user?.Name ?? "system";

        if (row == null)
        {
            row = new Domain.Entities.ContractReminderSetting
            {
                Scope = "global",
                Settings = JsonSerializer.Serialize(next, ReminderJsonOptions),
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedBy = actor
            };
            _dbContext.ContractReminderSettings.Add(row);
        }
        else
        {
            row.Settings = JsonSerializer.Serialize(next, ReminderJsonOptions);
            row.UpdatedAt = now;
            row.UpdatedBy = actor;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(next);
    }

    private async Task<ContractReminderSettingsDto> LoadReminderSettingsAsync(CancellationToken cancellationToken)
    {
        var row = await _dbContext.ContractReminderSettings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Scope == "global", cancellationToken);

        if (row == null || string.IsNullOrWhiteSpace(row.Settings))
        {
            return CloneSettings(DefaultReminderSettings);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ContractReminderSettingsDto>(row.Settings, ReminderJsonOptions)
                         ?? CloneSettings(DefaultReminderSettings);
            return NormalizeReminderSettings(parsed);
        }
        catch (JsonException)
        {
            return CloneSettings(DefaultReminderSettings);
        }
    }

    private static ContractReminderSettingsDto MergeReminderSettings(
        ContractReminderSettingsDto current,
        ContractReminderSettingsUpdateRequest payload)
    {
        var merged = new ContractReminderSettingsDto
        {
            LeadDays = payload.LeadDays ?? current.LeadDays,
            Channels = payload.Channels ?? current.Channels,
            AutoNotify = payload.AutoNotify ?? current.AutoNotify,
            RemindExpired = payload.RemindExpired ?? current.RemindExpired
        };

        return NormalizeReminderSettings(merged);
    }

    private static ContractReminderSettingsDto NormalizeReminderSettings(ContractReminderSettingsDto input)
    {
        return new ContractReminderSettingsDto
        {
            LeadDays = NormalizeLeadDays(input.LeadDays),
            Channels = NormalizeChannels(input.Channels),
            AutoNotify = input.AutoNotify,
            RemindExpired = input.RemindExpired
        };
    }

    private static List<int> NormalizeLeadDays(IEnumerable<int> input)
    {
        var values = input
            .Select(value => Math.Abs(value))
            .Where(value => value > 0 && value <= 365)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        return values.Count > 0 ? values : new List<int>(DefaultReminderSettings.LeadDays);
    }

    private static List<string> NormalizeChannels(IEnumerable<string> input)
    {
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

    private async Task<List<ContractReminderItem>> ListMandatoryContractsForRemindersAsync(
        ContractReminderSettingsDto settings,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var horizon = settings.LeadDays.Count > 0 ? settings.LeadDays.Max() : 30;
        var lowerBound = now.Date.AddDays(-30);
        var upperBound = now.Date.AddDays(horizon);

        var contracts = await _dbContext.Contracts.AsNoTracking()
            .Where(c => c.IsMandatory && c.EffectiveTo != null)
            .ToListAsync(cancellationToken);

        var filtered = contracts
            .Select(contract => new { Contract = contract, EffectiveTo = ParseDateOrNull(contract.EffectiveTo) })
            .Where(entry => entry.EffectiveTo.HasValue &&
                            entry.EffectiveTo.Value.Date >= lowerBound &&
                            entry.EffectiveTo.Value.Date <= upperBound)
            .ToList();

        var supplierIds = filtered
            .Select(entry => entry.Contract.SupplierId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var supplierMap = await _dbContext.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.CompanyName, cancellationToken);

        var items = new List<ContractReminderItem>();
        foreach (var entry in filtered.OrderBy(e => e.EffectiveTo))
        {
            var contract = entry.Contract;
            var supplierName = contract.SupplierId.HasValue && supplierMap.TryGetValue(contract.SupplierId.Value, out var name)
                ? name
                : string.Empty;
            var diffDays = (int)Math.Ceiling((entry.EffectiveTo!.Value.Date - now.Date).TotalDays);
            var bucket = ResolveReminderBucket(settings.LeadDays, diffDays);

            items.Add(new ContractReminderItem
            {
                Id = contract.Id,
                SupplierId = contract.SupplierId ?? 0,
                SupplierName = supplierName,
                Title = contract.Title,
                AgreementNumber = contract.AgreementNumber,
                Amount = contract.Amount,
                Currency = contract.Currency,
                Status = contract.Status,
                EffectiveTo = contract.EffectiveTo,
                AutoRenew = contract.AutoRenew,
                IsMandatory = contract.IsMandatory,
                Bucket = bucket,
                DaysRemaining = diffDays
            });
        }

        return items;
    }

    private static DateTime? ParseDateOrNull(string? value)
    {
        return TryParseDate(value, out var parsed) ? parsed : null;
    }

    private static string ResolveReminderBucket(IEnumerable<int> leadDays, int diffDays)
    {
        if (diffDays < 0)
        {
            return "expired";
        }

        foreach (var lead in leadDays.OrderBy(value => value))
        {
            if (diffDays <= lead)
            {
                return $"within_{lead}";
            }
        }

        return "future";
    }

    private static ContractReminderSummary BuildReminderSummary(
        IReadOnlyList<ContractReminderItem> items,
        ContractReminderSettingsDto settings)
    {
        var summary = new ContractReminderSummary
        {
            Settings = settings,
            Expired = new ReminderBucketStats(),
            Buckets = settings.LeadDays.Select(lead => new ReminderBucketSummary
            {
                Key = $"within_{lead}",
                LeadDays = lead
            }).ToList()
        };

        var expiredSuppliers = new HashSet<int>();
        var bucketSuppliers = summary.Buckets.ToDictionary(b => b.Key, _ => new HashSet<int>());

        foreach (var item in items)
        {
            if (item.Bucket == "expired")
            {
                summary.Expired.ContractCount += 1;
                expiredSuppliers.Add(item.SupplierId);
                continue;
            }

            var bucket = summary.Buckets.FirstOrDefault(b => b.Key == item.Bucket);
            if (bucket == null)
            {
                continue;
            }

            bucket.ContractCount += 1;
            bucketSuppliers[item.Bucket].Add(item.SupplierId);
        }

        summary.Expired.SupplierCount = expiredSuppliers.Count;
        foreach (var bucket in summary.Buckets)
        {
            bucket.SupplierCount = bucketSuppliers[bucket.Key].Count;
        }

        return summary;
    }

    private static ContractReminderSettingsDto CloneSettings(ContractReminderSettingsDto source)
    {
        return new ContractReminderSettingsDto
        {
            LeadDays = new List<int>(source.LeadDays),
            Channels = new List<string>(source.Channels),
            AutoNotify = source.AutoNotify,
            RemindExpired = source.RemindExpired
        };
    }

    public sealed class ContractReminderSettingsUpdateRequest
    {
        public List<int>? LeadDays { get; set; }
        public List<string>? Channels { get; set; }
        public bool? AutoNotify { get; set; }
        public bool? RemindExpired { get; set; }
    }

    private sealed class ContractReminderSettingsDto
    {
        public List<int> LeadDays { get; set; } = new();
        public List<string> Channels { get; set; } = new();
        public bool AutoNotify { get; set; }
        public bool RemindExpired { get; set; }
    }

    private sealed class ContractReminderItem
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? AgreementNumber { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string? Status { get; set; }
        public string? EffectiveTo { get; set; }
        public bool AutoRenew { get; set; }
        public bool IsMandatory { get; set; }
        public string Bucket { get; set; } = string.Empty;
        public int? DaysRemaining { get; set; }
    }

    private sealed class ContractReminderSummary
    {
        public ContractReminderSettingsDto Settings { get; set; } = new();
        public ReminderBucketStats Expired { get; set; } = new();
        public List<ReminderBucketSummary> Buckets { get; set; } = new();
    }

    private class ReminderBucketStats
    {
        public int ContractCount { get; set; }
        public int SupplierCount { get; set; }
    }

    private sealed class ReminderBucketSummary : ReminderBucketStats
    {
        public string Key { get; set; } = string.Empty;
        public int? LeadDays { get; set; }
    }

    private sealed class ReminderListResponse
    {
        public ContractReminderSettingsDto Settings { get; set; } = new();
        public int Total { get; set; }
        public List<ContractReminderItem> Items { get; set; } = new();
    }
}
