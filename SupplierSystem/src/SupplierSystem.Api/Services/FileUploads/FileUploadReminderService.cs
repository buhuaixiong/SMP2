using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Email;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.FileUploads;

public sealed class FileUploadReminderService
{
    private const string SmtpConfigKey = "smtp_config";
    private const string DefaultFromAddress = "no-reply@supplier-system.local";
    private const string DefaultFromName = "Supplier Management System";
    private readonly SupplierSystemDbContext _dbContext;
    private readonly CompatibilityMigrationService _migrationService;
    private readonly FileUploadRepository _repository;
    private readonly ILogger<FileUploadReminderService> _logger;

    public FileUploadReminderService(
        SupplierSystemDbContext dbContext,
        CompatibilityMigrationService migrationService,
        FileUploadRepository repository,
        ILogger<FileUploadReminderService> logger)
    {
        _dbContext = dbContext;
        _migrationService = migrationService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<Dictionary<string, object?>> SendSingleFileReminderAsync(
        AuthUser user,
        int uploadId,
        CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new FileUploadServiceException(401, "Authentication required");
        }

        if (!IsPurchaser(user))
        {
            throw new FileUploadServiceException(403, "Only purchasers can send file expiry reminders");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var row = await _repository.GetFileUploadByIdAsync(uploadId, cancellationToken);
        if (row == null)
        {
            throw new FileUploadServiceException(404, "File upload not found");
        }

        var snapshot = MapSnapshot(row);
        if (!string.Equals(snapshot.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileUploadServiceException(400, "Can only send reminders for approved files");
        }

        if (string.IsNullOrWhiteSpace(snapshot.ValidTo))
        {
            throw new FileUploadServiceException(400, "File does not have a validity period");
        }

        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == snapshot.SupplierId, cancellationToken);
        if (supplier == null)
        {
            throw new FileUploadServiceException(404, "Supplier not found");
        }

        if (string.IsNullOrWhiteSpace(supplier.ContactEmail))
        {
            throw new FileUploadServiceException(400, "Supplier does not have a contact email");
        }

        var smtpConfig = await LoadSmtpConfigAsync(cancellationToken);
        await SendReminderEmailAsync(
            smtpConfig,
            supplier.ContactEmail,
            supplier.CompanyName,
            snapshot.FileName ?? "file",
            snapshot.ValidTo,
            cancellationToken);

        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["message"] = "Reminder email sent.",
            ["emailSent"] = true
        };
    }

    public async Task<Dictionary<string, object?>> SendBatchFileRemindersAsync(
        AuthUser user,
        int daysThreshold,
        CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new FileUploadServiceException(401, "Authentication required");
        }

        if (!IsPurchaser(user))
        {
            throw new FileUploadServiceException(403, "Only purchasers can send file expiry reminders");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var normalizedDays = daysThreshold < 0 ? 0 : daysThreshold;
        var rows = await _repository.GetExpiringFileUploadsAsync(normalizedDays, cancellationToken);
        if (rows.Count == 0)
        {
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["message"] = "No expiring files.",
                ["sent"] = 0,
                ["failed"] = 0,
                ["errors"] = Array.Empty<object>()
            };
        }

        var smtpConfig = await LoadSmtpConfigAsync(cancellationToken);

        var uploads = rows.Select(MapSnapshot).ToList();
        var supplierIds = uploads.Select(upload => upload.SupplierId).Distinct().ToList();

        var suppliers = await _dbContext.Suppliers.AsNoTracking()
            .Where(supplier => supplierIds.Contains(supplier.Id))
            .ToListAsync(cancellationToken);

        var supplierMap = suppliers.ToDictionary(supplier => supplier.Id, supplier => supplier);

        var sent = 0;
        var failed = 0;
        var errors = new List<Dictionary<string, object?>>();

        foreach (var upload in uploads)
        {
            if (!supplierMap.TryGetValue(upload.SupplierId, out var supplier) ||
                string.IsNullOrWhiteSpace(supplier.ContactEmail))
            {
                failed++;
                errors.Add(new Dictionary<string, object?>
                {
                    ["uploadId"] = upload.Id,
                    ["reason"] = "Supplier email not found"
                });
                continue;
            }

            try
            {
                await SendReminderEmailAsync(
                    smtpConfig,
                    supplier.ContactEmail,
                    supplier.CompanyName,
                    upload.FileName ?? "file",
                    upload.ValidTo,
                    cancellationToken);
                sent++;
                await Task.Delay(200, cancellationToken);
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add(new Dictionary<string, object?>
                {
                    ["uploadId"] = upload.Id,
                    ["reason"] = ex.Message
                });
            }
        }

        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["message"] = $"Batch reminders complete: sent {sent}, failed {failed}.",
            ["sent"] = sent,
            ["failed"] = failed,
            ["errors"] = errors
        };
    }

    private static bool IsPurchaser(AuthUser user)
    {
        return string.Equals(user.Role, "purchaser", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SmtpConfig> LoadSmtpConfigAsync(CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfigs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == SmtpConfigKey, cancellationToken);

        if (config == null || string.IsNullOrWhiteSpace(config.Value))
        {
            throw new FileUploadServiceException(500, "Email service not configured");
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
            var password = string.IsNullOrWhiteSpace(passwordRaw)
                ? null
                : SmtpConfigCrypto.Decrypt(passwordRaw);
            var from = ReadString(root, "from");
            var fromName = ReadString(root, "fromName");

            if (string.IsNullOrWhiteSpace(host))
            {
                if (effectiveUsePickup)
                {
                    host = "pickup";
                }
                else
                {
                    throw new FileUploadServiceException(500, "SMTP configuration is incomplete");
                }
            }

            if (!effectiveUsePickup && (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)))
            {
                throw new FileUploadServiceException(500, "SMTP configuration is incomplete");
            }

            var fromAddress = string.IsNullOrWhiteSpace(from) ? user : from;
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                fromAddress = DefaultFromAddress;
            }
            var displayName = string.IsNullOrWhiteSpace(fromName) ? DefaultFromName : fromName;

            var safeUser = user ?? string.Empty;
            var safePassword = password ?? string.Empty;
            return new SmtpConfig(
                host,
                port,
                secure,
                safeUser,
                safePassword,
                fromAddress,
                displayName,
                effectiveUsePickup);
        }
        catch (FileUploadServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse SMTP configuration.");
            throw new FileUploadServiceException(500, "Failed to load SMTP configuration");
        }
    }

    private static async Task SendReminderEmailAsync(
        SmtpConfig config,
        string email,
        string companyName,
        string fileName,
        string? validTo,
        CancellationToken cancellationToken)
    {
        var systemUrl = Environment.GetEnvironmentVariable("CLIENT_ORIGIN") ?? "http://localhost:5173";
        var expiryDate = FormatDate(validTo);
        var safeCompany = WebUtility.HtmlEncode(companyName);
        var safeFileName = WebUtility.HtmlEncode(fileName);

        var subject = $"File expiry reminder - {fileName}";
        var body = BuildEmailBody(safeCompany, safeFileName, expiryDate, systemUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(config.FromAddress, config.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(email));

        using var client = config.UsePickup
            ? CreatePickupClient()
            : CreateNetworkClient(config);

        await client.SendMailAsync(message, cancellationToken);
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
        return new SmtpClient(config.Host, config.Port)
        {
            EnableSsl = config.Secure,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(config.User, config.Password)
        };
    }

    private static string BuildEmailBody(string companyName, string fileName, string expiryDate, string systemUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<body style=""font-family: Arial, sans-serif; color: #333;"">
  <h2>File Expiry Reminder</h2>
  <p>Dear {companyName},</p>
  <p>Your uploaded file is nearing expiry. Please update it before the due date.</p>
  <p><strong>File:</strong> {fileName}<br/>
     <strong>Expiry date:</strong> {expiryDate}</p>
  <p>Login: <a href=""{systemUrl}"">{systemUrl}</a></p>
  <p>Thank you,<br/>Supplier Management System</p>
</body>
</html>";
    }

    private static string FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "N/A";
        }

        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToString("yyyy-MM-dd")
            : value;
    }

    private static FileUploadSnapshot MapSnapshot(Dictionary<string, object?> row)
    {
        return new FileUploadSnapshot(
            GetInt(row, "id") ?? 0,
            GetInt(row, "supplierId") ?? GetInt(row, "supplier_id") ?? 0,
            GetInt(row, "fileId") ?? 0,
            GetString(row, "fileName"),
            GetString(row, "fileDescription"),
            GetString(row, "status") ?? string.Empty,
            GetString(row, "currentStep") ?? string.Empty,
            GetString(row, "submittedBy"),
            GetDateString(row, "submittedAt"),
            GetString(row, "riskLevel"),
            GetDateString(row, "validFrom"),
            GetDateString(row, "validTo"),
            GetDateString(row, "createdAt"),
            GetDateString(row, "updatedAt"));
    }

    private static int? GetInt(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            decimal decimalValue => (int)decimalValue,
            double doubleValue => (int)doubleValue,
            bool boolValue => boolValue ? 1 : 0,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private static string? GetString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value.ToString();
    }

    private static string? GetDateString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DateTimeOffset dto)
        {
            return dto.ToString("o");
        }

        if (value is DateTime dt)
        {
            return new DateTimeOffset(dt.ToUniversalTime()).ToString("o");
        }

        return value.ToString();
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
        string User,
        string Password,
        string FromAddress,
        string FromName,
        bool UsePickup);
}
