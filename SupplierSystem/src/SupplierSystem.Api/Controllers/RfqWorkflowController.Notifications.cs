using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Email;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    private const string SmtpConfigKey = "smtp_config";
    private const string DefaultFromAddress = "no-reply@supplier-system.local";
    private const string DefaultFromName = "Supplier Management System";
    private const string DefaultClientOrigin = "http://localhost:5173";

    private async Task TryNotifyRfqPublishedAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        var recipients = await ResolveSupplierEmailsForRfqAsync((int)rfq.Id, cancellationToken);
        var subject = BuildRfqSubject("RFQ published", rfq);
        await TrySendRfqNotificationAsync(
            rfq,
            recipients,
            subject,
            "Purchaser published the RFQ.",
            cancellationToken);
    }

    private async Task TryNotifyQuoteSubmittedAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        var recipients = await ResolveRoleEmailsAsync("purchaser", cancellationToken);
        var subject = BuildRfqSubject("RFQ quote submitted", rfq);
        await TrySendRfqNotificationAsync(
            rfq,
            recipients,
            subject,
            "Supplier submitted a quote.",
            cancellationToken);
    }

    private async Task TryNotifyRfqSubmittedForApprovalAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        var recipients = await ResolveRoleEmailsAsync("procurement_director", cancellationToken);
        var subject = BuildRfqSubject("RFQ pending director approval", rfq);
        await TrySendRfqNotificationAsync(
            rfq,
            recipients,
            subject,
            "Purchaser selected the quote and submitted for approval.",
            cancellationToken);
    }

    private async Task TryNotifyRfqDirectorApprovedAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        var recipients = await ResolveRoleEmailsAsync("purchaser", cancellationToken);
        var subject = BuildRfqSubject("RFQ director approval completed", rfq);
        await TrySendRfqNotificationAsync(
            rfq,
            recipients,
            subject,
            "Procurement director approved the quote selection.",
            cancellationToken);
    }

    private async Task TryNotifyRfqDirectorRejectedAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        var recipients = await ResolveRoleEmailsAsync("purchaser", cancellationToken);
        var subject = BuildRfqSubject("RFQ director rejected", rfq);
        await TrySendRfqNotificationAsync(
            rfq,
            recipients,
            subject,
            "Procurement director rejected the RFQ.",
            cancellationToken);
    }

    private async Task TryNotifyPrExportedAsync(Rfq rfq, CancellationToken cancellationToken)
    {
        var recipients = await ResolveRoleEmailsAsync("department_user", cancellationToken);
        var subject = BuildRfqSubject("PR exported for RFQ", rfq);
        await TrySendRfqNotificationAsync(
            rfq,
            recipients,
            subject,
            "Purchaser exported PR.",
            cancellationToken);
    }

    private async Task TrySendRfqNotificationAsync(
        Rfq rfq,
        List<string> recipients,
        string subject,
        string previousStep,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
        {
            _logger.LogWarning("No recipients found for RFQ notification {Subject}.", subject);
            return;
        }

        SmtpConfig? smtpConfig;
        try
        {
            smtpConfig = await LoadSmtpConfigAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SMTP config for RFQ notification.");
            return;
        }

        if (smtpConfig == null)
        {
            _logger.LogWarning("SMTP not configured; skipping RFQ notification email.");
            return;
        }

        try
        {
            var link = BuildRfqLink(rfq.Id);
            var body = BuildRfqNotificationBody(rfq, previousStep, link);

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send RFQ notification email.");
        }
    }

    private async Task<List<string>> ResolveSupplierEmailsForRfqAsync(int rfqId, CancellationToken cancellationToken)
    {
        var supplierEmails = await (from inv in _dbContext.SupplierRfqInvitations.AsNoTracking()
                                    join supplier in _dbContext.Suppliers.AsNoTracking()
                                        on inv.SupplierId equals supplier.Id
                                    where inv.RfqId == rfqId
                                    select supplier.ContactEmail)
            .ToListAsync(cancellationToken);

        var invitationEmails = await _dbContext.SupplierRfqInvitations.AsNoTracking()
            .Where(inv => inv.RfqId == rfqId && inv.RecipientEmail != null)
            .Select(inv => inv.RecipientEmail)
            .ToListAsync(cancellationToken);

        var externalEmails = await _dbContext.RfqExternalInvitations.AsNoTracking()
            .Where(inv => inv.RfqId == rfqId)
            .Select(inv => inv.Email)
            .ToListAsync(cancellationToken);

        var combined = supplierEmails.Concat(invitationEmails).Concat(externalEmails);
        return NormalizeEmails(combined);
    }

    private async Task<List<string>> ResolveRoleEmailsAsync(string role, CancellationToken cancellationToken)
    {
        var normalizedRole = string.IsNullOrWhiteSpace(role)
            ? null
            : role.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return new List<string>();
        }

        var emails = await _dbContext.Users.AsNoTracking()
            .Where(user => user.Role != null &&
                           user.Email != null &&
                           user.Role.ToLower() == normalizedRole)
            .Select(user => user.Email!)
            .ToListAsync(cancellationToken);

        return NormalizeEmails(emails);
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
            var host = ReadString(root, "host");
            var port = ReadInt(root, "port") ?? 587;
            var secure = ReadBool(root, "secure") ?? false;
            var user = ReadString(root, "user");
            var passwordRaw = ReadString(root, "password");
            var password = string.IsNullOrWhiteSpace(passwordRaw) ? null : SmtpConfigCrypto.Decrypt(passwordRaw);
            var from = ReadString(root, "from");
            var fromName = ReadString(root, "fromName") ?? DefaultFromName;

            var fromAddress = string.IsNullOrWhiteSpace(from) ? user : from;
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                fromAddress = DefaultFromAddress;
            }

            if (testMode)
            {
                return new SmtpConfig("pickup", 0, false, null, null, fromAddress, fromName, true);
            }

            if (string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535)
            {
                return null;
            }

            return new SmtpConfig(host, port, secure, user, password, fromAddress, fromName, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse SMTP config for RFQ notifications.");
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

    private static SmtpClient CreateNetworkClient(SmtpConfig config)
    {
        var client = new SmtpClient(config.Host, config.Port)
        {
            EnableSsl = config.Secure,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(config.User) && !string.IsNullOrWhiteSpace(config.Password))
        {
            client.Credentials = new NetworkCredential(config.User, config.Password);
        }

        return client;
    }

    private static string BuildRfqLink(long rfqId)
    {
        var origin = Environment.GetEnvironmentVariable("CLIENT_ORIGIN") ?? DefaultClientOrigin;
        return $"{origin}/rfq/{rfqId}";
    }

    private static string BuildRfqSubject(string prefix, Rfq rfq)
    {
        var title = string.IsNullOrWhiteSpace(rfq.Title) ? "RFQ" : rfq.Title.Trim();
        return $"{prefix} - {title} (#{rfq.Id})";
    }

    private static string BuildRfqNotificationBody(Rfq rfq, string previousStep, string link)
    {
        var title = string.IsNullOrWhiteSpace(rfq.Title) ? "N/A" : rfq.Title;
        return $@"
Previous step completed: {previousStep}

RFQ Title: {title}
RFQ ID: {rfq.Id}
Link: {link}
";
    }

    private static List<string> NormalizeEmails(IEnumerable<string?> emails)
    {
        return emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
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
