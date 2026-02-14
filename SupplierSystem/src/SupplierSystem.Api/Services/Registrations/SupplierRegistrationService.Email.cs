using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Email;
using SupplierSystem.Application.DTOs.Registrations;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.Registrations;

public sealed partial class SupplierRegistrationService
{
    private async Task TryNotifyNextApproverAsync(
        SupplierRegistrationApplication application,
        RegistrationWorkflowStep step,
        string? nextStatus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nextStatus))
        {
            return;
        }

        if (string.Equals(nextStatus, RegistrationConstants.RegistrationStatusActivated, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nextStatus, RegistrationConstants.RegistrationStatusRejected, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var recipients = await ResolveNextApproverEmailsAsync(application, nextStatus, cancellationToken);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("No email recipients found for next registration step {NextStatus}.", nextStatus);
            return;
        }

        SmtpConfig? smtpConfig;
        try
        {
            smtpConfig = await LoadSmtpConfigAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SMTP config for approval notification.");
            return;
        }

        if (smtpConfig == null)
        {
            _logger.LogWarning("SMTP not configured; skipping approval notification email.");
            return;
        }

        try
        {
            var subject = BuildApprovalNotificationSubject(application, nextStatus);
            var body = BuildApprovalNotificationBody(application, step, nextStatus);

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
            _logger.LogError(ex, "Failed to send approval notification email.");
        }
    }

    private async Task<List<string>> ResolveNextApproverEmailsAsync(
        SupplierRegistrationApplication application,
        string nextStatus,
        CancellationToken cancellationToken)
    {
        var recipients = new List<string>();

        if (string.Equals(nextStatus, RegistrationConstants.RegistrationStatusPendingPurchaser, StringComparison.OrdinalIgnoreCase))
        {
            var purchaserEmail = application.AssignedPurchaserEmail?.Trim();
            if (!string.IsNullOrWhiteSpace(purchaserEmail))
            {
                recipients.Add(purchaserEmail);
                return recipients;
            }
        }

        var role = ApprovalWorkflow
            .FirstOrDefault(item => string.Equals(item.Status, nextStatus, StringComparison.OrdinalIgnoreCase))
            ?.Role;

        if (string.IsNullOrWhiteSpace(role) &&
            string.Equals(nextStatus, RegistrationConstants.RegistrationStatusPendingCodeBinding, StringComparison.OrdinalIgnoreCase))
        {
            role = "finance_accountant";
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            return recipients;
        }

        var normalizedRole = string.IsNullOrWhiteSpace(role)
            ? null
            : role.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return recipients;
        }

        recipients = await _dbContext.Users.AsNoTracking()
            .Where(user => user.Role != null &&
                           user.Email != null &&
                           user.Role.ToLower() == normalizedRole)
            .Select(user => user.Email!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return recipients;
    }

    private static string BuildApprovalNotificationSubject(
        SupplierRegistrationApplication application,
        string nextStatus)
    {
        var stepLabel = MapStatusToLabel(nextStatus);
        return $"Supplier registration awaiting {stepLabel} approval - {application.CompanyName}";
    }

    private static string BuildApprovalNotificationBody(
        SupplierRegistrationApplication application,
        RegistrationWorkflowStep step,
        string nextStatus)
    {
        var submittedAt = string.IsNullOrWhiteSpace(application.CreatedAt)
            ? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            : application.CreatedAt;

        var currentStep = MapStatusToLabel(step.Status);
        var nextStep = MapStatusToLabel(nextStatus);
        var contactName = application.ContactName;
        var contactEmail = application.ContactEmail;
        var contactPhone = application.ContactPhone;

        return $@"
Supplier registration approval required.

Application ID: {application.Id}
Company Name: {application.CompanyName}
Current Step: {currentStep}
Next Step: {nextStep}
Contact: {contactName}
Contact Email: {contactEmail}
Contact Phone: {contactPhone}
Submitted At (UTC): {submittedAt}
";
    }

    private async Task TryNotifyApplicantActivatedAsync(
        SupplierRegistrationApplication application,
        CancellationToken cancellationToken)
    {
        var recipient = application.ContactEmail?.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return;
        }

        SmtpConfig? smtpConfig;
        try
        {
            smtpConfig = await LoadSmtpConfigAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SMTP config for registration activation notification.");
            return;
        }

        if (smtpConfig == null)
        {
            _logger.LogWarning("SMTP not configured; skipping registration activation email.");
            return;
        }

        try
        {
            var subject = $"Supplier registration approved - {application.CompanyName}";
            var body = BuildRegistrationActivatedBody(application);

            using var message = new MailMessage
            {
                From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            message.To.Add(new MailAddress(recipient));

            using var client = smtpConfig.UsePickup
                ? CreatePickupClient()
                : CreateNetworkClient(smtpConfig);

            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration activation email.");
        }
    }

    private static string BuildRegistrationActivatedBody(SupplierRegistrationApplication application)
    {
        var activatedAt = string.IsNullOrWhiteSpace(application.ActivatedAt)
            ? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            : application.ActivatedAt;
        var supplierCode = string.IsNullOrWhiteSpace(application.SupplierCode) ? "N/A" : application.SupplierCode;
        var contactEmail = application.ContactEmail ?? "N/A";
        var systemUrl = Environment.GetEnvironmentVariable("CLIENT_ORIGIN") ?? "http://localhost:5173";

        return $@"
Supplier registration approved.

Company Name: {application.CompanyName}
Supplier Code: {supplierCode}
Login Email: {contactEmail}
Default Password: {DefaultTrackingPassword}
Activated At (UTC): {activatedAt}
Login: {systemUrl}

Please log in and change your password immediately.
";
    }

    private async Task TryNotifyApplicantRejectedAsync(
        SupplierRegistrationApplication application,
        RegistrationWorkflowStep step,
        string reason,
        CancellationToken cancellationToken)
    {
        var recipient = application.ContactEmail?.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return;
        }

        SmtpConfig? smtpConfig;
        try
        {
            smtpConfig = await LoadSmtpConfigAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SMTP config for registration rejection notification.");
            return;
        }

        if (smtpConfig == null)
        {
            _logger.LogWarning("SMTP not configured; skipping registration rejection email.");
            return;
        }

        try
        {
            var subject = $"Supplier registration rejected - {application.CompanyName}";
            var body = BuildRegistrationRejectionBody(application, step, reason);

            using var message = new MailMessage
            {
                From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            message.To.Add(new MailAddress(recipient));

            using var client = smtpConfig.UsePickup
                ? CreatePickupClient()
                : CreateNetworkClient(smtpConfig);

            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration rejection email.");
        }
    }

    private static string BuildRegistrationRejectionBody(
        SupplierRegistrationApplication application,
        RegistrationWorkflowStep step,
        string reason)
    {
        var submittedAt = string.IsNullOrWhiteSpace(application.CreatedAt)
            ? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            : application.CreatedAt;

        var stepLabel = MapStatusToLabel(step.Status);
        var contactName = application.ContactName ?? "N/A";
        var contactEmail = application.ContactEmail ?? "N/A";
        var contactPhone = application.ContactPhone ?? "N/A";

        return $@"
Supplier registration rejected.

Application ID: {application.Id}
Company Name: {application.CompanyName}
Rejected Step: {stepLabel}
Reason: {reason}
Contact: {contactName}
Contact Email: {contactEmail}
Contact Phone: {contactPhone}
Submitted At (UTC): {submittedAt}
";
    }

    private static string MapStatusToLabel(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Unknown";
        }

        return status switch
        {
            RegistrationConstants.RegistrationStatusPendingPurchaser => "Purchaser",
            "pending_quality_manager" => "Quality Manager",
            "pending_procurement_manager" => "Procurement Manager",
            "pending_procurement_director" => "Procurement Director",
            "pending_finance_director" => "Finance Director",
            "pending_accountant" => "Finance Accountant",
            RegistrationConstants.RegistrationStatusPendingCodeBinding => "Finance Accountant (Code Binding)",
            RegistrationConstants.RegistrationStatusPendingCashier => "Finance Cashier",
            _ => status
        };
    }

    private async Task TrySendRegistrationNotificationAsync(
        SupplierRegistrationApplication application,
        SupplierRegistrationNormalizedPayload normalized,
        CancellationToken cancellationToken)
    {
        var recipient = application.AssignedPurchaserEmail?.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return;
        }

        SmtpConfig? smtpConfig;
        try
        {
            smtpConfig = await LoadSmtpConfigAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SMTP config for registration notification.");
            return;
        }

        if (smtpConfig == null)
        {
            _logger.LogWarning("SMTP not configured; skipping registration notification email.");
            return;
        }

        try
        {
            var subject = $"New supplier registration - {application.CompanyName}";
            var body = BuildRegistrationNotificationBody(application, normalized);

            using var message = new MailMessage
            {
                From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            message.To.Add(new MailAddress(recipient));

            using var client = smtpConfig.UsePickup
                ? CreatePickupClient()
                : CreateNetworkClient(smtpConfig);

            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration notification email.");
        }
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
            _logger.LogError(ex, "Failed to parse SMTP config for registration notification.");
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

    private static string BuildRegistrationNotificationBody(
        SupplierRegistrationApplication application,
        SupplierRegistrationNormalizedPayload normalized)
    {
        var submittedAt = string.IsNullOrWhiteSpace(application.CreatedAt)
            ? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            : application.CreatedAt;

        var contactName = normalized.ContactName ?? application.ContactName ?? "N/A";
        var contactEmail = normalized.ContactEmail ?? application.ContactEmail ?? "N/A";
        var contactPhone = normalized.ContactPhone ?? application.ContactPhone ?? "N/A";
        var procurementEmail = application.AssignedPurchaserEmail ?? "N/A";
        var registrationNumber = normalized.BusinessRegistrationNumber ?? application.BusinessRegistrationNumber ?? "N/A";
        var classification = normalized.SupplierClassification ?? application.SupplierClassification ?? "N/A";
        var currency = normalized.OperatingCurrency ?? application.OperatingCurrency ?? "N/A";

        return $@"
Supplier registration submitted.

Application ID: {application.Id}
Company Name: {application.CompanyName}
Registration Number: {registrationNumber}
Classification: {classification}
Operating Currency: {currency}
Contact: {contactName}
Contact Email: {contactEmail}
Contact Phone: {contactPhone}
Assigned Procurement Email: {procurementEmail}
Submitted At (UTC): {submittedAt}
";
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
