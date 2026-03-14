using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Api.Services.Email;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Reminders;

public sealed class ReminderNotifier
{
    private const string SmtpConfigKey = "smtp_config";
    private const string DefaultFromAddress = "no-reply@supplier-system.local";
    private const string DefaultFromName = "Supplier Management System";
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReminderNotifier> _logger;

    public ReminderNotifier(
        SupplierSystemDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<ReminderNotifier> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> NotifyAsync(ReminderQueue item, CancellationToken cancellationToken)
    {
        var delivered = false;
        var description = DescribeReminder(item);
        var dueAt = TryFormatDueAt(item.DueAt);
        var subject = $"[Reminder] {description} due";
        var text = $"{description}\nDue at: {dueAt}\nAction: {item.Type}";

        try
        {
            var emailSent = await SendEmailAsync(subject, text, cancellationToken);
            delivered = delivered || emailSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reminder] Email send failed.");
        }

        try
        {
            var teamsSent = await SendTeamsAsync(subject, text, cancellationToken);
            delivered = delivered || teamsSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reminder] Teams notification failed.");
        }

        return delivered;
    }

    private async Task<bool> SendEmailAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var recipients = GetReminderRecipients();
        if (recipients.Count == 0)
        {
            return false;
        }

        var smtpConfig = await LoadSmtpConfigAsync(cancellationToken);
        if (smtpConfig == null)
        {
            return false;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        foreach (var recipient in recipients)
        {
            message.To.Add(new MailAddress(recipient));
        }

        using var client = smtpConfig.UsePickup
            ? CreatePickupClient()
            : CreateNetworkClient(smtpConfig);

        await client.SendMailAsync(message, cancellationToken);
        return true;
    }

    private async Task<bool> SendTeamsAsync(string title, string text, CancellationToken cancellationToken)
    {
        var webhook = Environment.GetEnvironmentVariable("REMINDER_TEAMS_WEBHOOK");
        if (string.IsNullOrWhiteSpace(webhook))
        {
            return false;
        }

        var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsJsonAsync(webhook, new
        {
            text = $"**{title}**\n{text}"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task<SmtpConfig?> LoadSmtpConfigAsync(CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfigs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == SmtpConfigKey, cancellationToken);

        if (config == null || string.IsNullOrWhiteSpace(config.Value))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(config.Value);
            var root = doc.RootElement;
            var testMode = ReadBool(root, "testMode") ?? false;
            var effectiveUsePickup = testMode;
            var host = ReadString(root, "host");
            var port = ReadInt(root, "port") ?? 587;
            var secure = ReadBool(root, "secure") ?? false;
            var user = ReadString(root, "user");
            var passwordRaw = ReadString(root, "password");
            var password = string.IsNullOrWhiteSpace(passwordRaw) ? null : SmtpConfigCrypto.Decrypt(passwordRaw);
            var from = ReadString(root, "from");
            var fromName = ReadString(root, "fromName");

            var fromAddress = string.IsNullOrWhiteSpace(from) ? user : from;
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                fromAddress = DefaultFromAddress;
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                if (!effectiveUsePickup)
                {
                    return null;
                }

                host = "pickup";
            }

            var displayName = string.IsNullOrWhiteSpace(fromName) ? DefaultFromName : fromName;

            return new SmtpConfig(host, port, secure, user, password, fromAddress, displayName, effectiveUsePickup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reminder] Failed to parse SMTP configuration.");
            return null;
        }
    }

    private static SmtpClient CreatePickupClient()
    {
        var pickupDirectory = SmtpDeliveryMode.ResolvePickupDirectory();
        Directory.CreateDirectory(pickupDirectory);
        return new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupDirectory
        };
    }

    private static SmtpClient CreateNetworkClient(SmtpConfig smtpConfig)
    {
        var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port)
        {
            EnableSsl = smtpConfig.Secure,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(smtpConfig.User) && !string.IsNullOrWhiteSpace(smtpConfig.Password))
        {
            client.Credentials = new NetworkCredential(smtpConfig.User, smtpConfig.Password);
        }

        return client;
    }

    private static List<string> GetReminderRecipients()
    {
        var raw = Environment.GetEnvironmentVariable("REMINDER_EMAIL_RECIPIENTS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        return raw
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0)
            .ToList();
    }

    private static string DescribeReminder(ReminderQueue item)
    {
        var payload = TryParsePayload(item.Payload);
        var kind = item.Type ?? string.Empty;

        if (kind.StartsWith("document", StringComparison.OrdinalIgnoreCase))
        {
            var docType = ReadPayloadString(payload, "docType") ?? "Document";
            var supplierId = ReadPayloadString(payload, "supplierId") ?? item.EntityId ?? "unknown";
            return $"{docType} for supplier #{supplierId}";
        }

        if (kind.StartsWith("contract", StringComparison.OrdinalIgnoreCase))
        {
            var title = ReadPayloadString(payload, "title") ?? "Contract";
            var supplierId = ReadPayloadString(payload, "supplierId") ?? item.EntityId ?? "unknown";
            return $"{title} (supplier #{supplierId})";
        }

        return $"{kind} for {item.EntityType} #{item.EntityId}";
    }

    private static string TryFormatDueAt(string? dueAt)
    {
        if (string.IsNullOrWhiteSpace(dueAt))
        {
            return "Unknown due date";
        }

        return DateTimeOffset.TryParse(dueAt, out var parsed)
            ? parsed.ToString("yyyy-MM-dd HH:mm:ss")
            : dueAt;
    }

    private static JsonElement? TryParsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadPayloadString(JsonElement? payload, string key)
    {
        if (!payload.HasValue || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!payload.Value.TryGetProperty(key, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static string? ReadString(JsonElement body, string key)
    {
        if (!body.TryGetProperty(key, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? ReadInt(JsonElement body, string key)
    {
        if (!body.TryGetProperty(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static bool? ReadBool(JsonElement body, string key)
    {
        if (!body.TryGetProperty(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String &&
            bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed record SmtpConfig(
        string Host,
        int Port,
        bool Secure,
        string? User,
        string? Password,
        string FromAddress,
        string FromName,
        bool UsePickup);
}
