using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services.Email;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/email-settings")]
public sealed class EmailSettingsController : ControllerBase
{
    private const string ConfigKey = "smtp_config";
    private const string DefaultFromAddress = "no-reply@supplier-system.local";
    private const string DefaultFromName = "Supplier Management System";
    private const string EncryptionKeyEnv = "CONFIG_ENCRYPTION_KEY";
    private const string DefaultEncryptionKey = "default-32-char-encryption-key!!";
    private const int ScryptCost = 16384;
    private const int ScryptBlockSize = 8;
    private const int ScryptParallel = 1;
    private const int ScryptKeyLength = 32;
    private static readonly Lazy<byte[]> DerivedKey = new(CreateDerivedKey);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SupplierSystemDbContext _dbContext;

    public EmailSettingsController(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var config = await LoadConfigAsync(cancellationToken);
        if (config == null)
        {
            return Ok(new
            {
                data = new
                {
                    configured = false,
                    provider = (string?)null,
                    host = string.Empty,
                    port = 587,
                    secure = false,
                    user = string.Empty,
                    from = string.Empty,
                    fromName = string.Empty,
                    testMode = false
                }
            });
        }

        var safeConfig = new
        {
            configured = true,
            provider = ReadString(config.Value, "provider") ?? "custom",
            host = ReadString(config.Value, "host") ?? string.Empty,
            port = ReadInt(config.Value, "port") ?? 587,
            secure = ReadBool(config.Value, "secure") ?? false,
            user = ReadString(config.Value, "user") ?? string.Empty,
            from = ReadString(config.Value, "from") ?? string.Empty,
            fromName = ReadString(config.Value, "fromName") ?? string.Empty,
            testMode = ReadBool(config.Value, "testMode") ?? false,
            hasPassword = !string.IsNullOrWhiteSpace(ReadString(config.Value, "password"))
        };

        return Ok(new { data = safeConfig });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateConfig([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var provider = ReadString(body, "provider");
        var host = ReadString(body, "host");
        var port = ReadInt(body, "port");
        var secure = ReadBool(body, "secure") ?? false;
        var user = ReadString(body, "user") ?? string.Empty;
        var password = ReadString(body, "password");
        var from = ReadString(body, "from") ?? string.Empty;
        var fromName = ReadString(body, "fromName") ?? DefaultFromName;
        var testMode = ReadBool(body, "testMode") ?? false;

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(host) || !port.HasValue)
        {
            return BadRequest(new { message = "provider, host, and port are required." });
        }

        if (port < 1 || port > 65535)
        {
            return BadRequest(new { message = "Invalid port number." });
        }

        var existing = await LoadConfigAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(password) && existing.HasValue)
        {
            var existingPassword = ReadString(existing.Value, "password");
            if (!string.IsNullOrWhiteSpace(existingPassword))
            {
                var decryptedPassword = DecryptPassword(existingPassword);
                if (decryptedPassword == null)
                {
                    return StatusCode(500, new { message = "Failed to decrypt SMTP password." });
                }

                password = decryptedPassword;
            }
        }

        string? encryptedPassword = null;
        if (!string.IsNullOrWhiteSpace(password))
        {
            encryptedPassword = EncryptPassword(password);
            if (encryptedPassword == null)
            {
                return StatusCode(500, new { message = "Failed to encrypt SMTP password." });
            }
        }

        var payload = new Dictionary<string, object?>
        {
            ["provider"] = provider,
            ["host"] = host,
            ["port"] = port.Value,
            ["secure"] = secure,
            ["user"] = user,
            ["password"] = encryptedPassword ?? string.Empty,
            ["from"] = from,
            ["fromName"] = fromName,
            ["testMode"] = testMode,
            ["updatedAt"] = DateTimeOffset.UtcNow.ToString("o")
        };

        var serialized = JsonSerializer.Serialize(payload, JsonOptions);
        await SaveConfigAsync(serialized, cancellationToken);

        return Ok(new
        {
            message = "SMTP configuration saved successfully",
            data = new
            {
                configured = true,
                provider,
                host,
                port = port.Value,
                secure,
                user,
                from,
                fromName,
                testMode,
                hasPassword = !string.IsNullOrWhiteSpace(password)
            }
        });
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestConfig([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var testEmail = ReadString(body, "testEmail")?.Trim();
        if (string.IsNullOrWhiteSpace(testEmail))
        {
            return BadRequest(new { message = "Test email address is required." });
        }

        if (!IsValidEmail(testEmail))
        {
            return BadRequest(new { message = "Invalid email address." });
        }

        var config = await LoadConfigAsync(cancellationToken);
        var configTestMode = config.HasValue && (ReadBool(config.Value, "testMode") ?? false);
        var usePickup = configTestMode;

        if (config == null && !usePickup)
        {
            return BadRequest(new { message = "SMTP not configured. Please configure SMTP settings first." });
        }

        string? host = null;
        var port = 587;
        var secure = false;
        string? user = null;
        string? passwordRaw = null;
        var provider = "custom";
        string? from = null;
        var fromName = DefaultFromName;

        if (config.HasValue)
        {
            host = ReadString(config.Value, "host");
            port = ReadInt(config.Value, "port") ?? 587;
            secure = ReadBool(config.Value, "secure") ?? false;
            user = ReadString(config.Value, "user");
            passwordRaw = ReadString(config.Value, "password");
            provider = ReadString(config.Value, "provider") ?? "custom";
            from = ReadString(config.Value, "from");
            fromName = ReadString(config.Value, "fromName") ?? DefaultFromName;
            if (configTestMode)
            {
                provider = "local-pickup";
            }
        }
        else
        {
            provider = "local-pickup";
            host = "pickup";
            port = 0;
        }

        if (string.IsNullOrWhiteSpace(host) && usePickup)
        {
            host = "pickup";
        }

        if (!usePickup)
        {
            if (string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535)
            {
                return BadRequest(new { message = "SMTP configuration is incomplete." });
            }
        }

        var effectiveHost = string.IsNullOrWhiteSpace(host) ? "pickup" : host;

        var password = DecryptPassword(passwordRaw);
        if (!string.IsNullOrWhiteSpace(passwordRaw) && password == null)
        {
            return StatusCode(500, new { message = "Failed to decrypt SMTP password." });
        }

        var fromAddress = string.IsNullOrWhiteSpace(from) ? user : from;
        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            fromAddress = DefaultFromAddress;
        }

        var testedBy = HttpContext.GetAuthUser()?.Name ?? HttpContext.GetAuthUser()?.Id ?? "unknown";
        var messageId = $"<{Guid.NewGuid():N}@supplier-system>";
        var timestamp = DateTimeOffset.UtcNow.ToString("o");

        try
        {
            var subject = "SMTP Configuration Test - Supplier Management System";
            var htmlBody = BuildTestEmailHtml(provider, effectiveHost, port, secure, testedBy, timestamp);
            var textBody = BuildTestEmailText(provider, effectiveHost, port, secure, testedBy, timestamp);

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = textBody,
                IsBodyHtml = false,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            message.To.Add(new MailAddress(testEmail));
            message.Headers.Add("Message-Id", messageId);
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

            using var client = usePickup
                ? CreatePickupClient()
                : CreateNetworkClient(effectiveHost, port, secure, user, password);

            await client.SendMailAsync(message, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Test email sent successfully",
                data = new
                {
                    messageId,
                    testEmail,
                    timestamp
                }
            });
        }
        catch (SmtpException ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "SMTP test failed",
                error = ex.Message,
                details = ex.StatusCode.ToString()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "SMTP test failed",
                error = ex.Message
            });
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

    private static SmtpClient CreateNetworkClient(
        string host,
        int port,
        bool secure,
        string? user,
        string? password)
    {
        var client = new SmtpClient(host, port)
        {
            EnableSsl = secure,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(user, password);
        }

        return client;
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteConfig(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var existing = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(item => item.Key == ConfigKey, cancellationToken);

        if (existing != null)
        {
            _dbContext.SystemConfigs.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "SMTP configuration removed successfully." });
    }

    [HttpGet("templates")]
    public IActionResult GetTemplates()
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var templates = new[]
        {
            new
            {
                id = "supplier_invitation",
                name = "Supplier Invitation",
                description = "Sent when inviting a supplier to submit a quote",
                category = "rfq",
                variables = new[] { "companyName", "contactPerson", "rfqTitle", "registrationUrl", "expiryDate", "inviterName" }
            },
            new
            {
                id = "contract_expiry_30",
                name = "Contract Expiry (30 days)",
                description = "Reminder sent 30 days before contract expiration",
                category = "contract",
                variables = new[] { "contractTitle", "agreementNumber", "expiryDate", "supplierName", "daysRemaining" }
            },
            new
            {
                id = "contract_expiry_7",
                name = "Contract Expiry (7 days)",
                description = "Urgent reminder sent 7 days before contract expiration",
                category = "contract",
                variables = new[] { "contractTitle", "agreementNumber", "expiryDate", "supplierName", "daysRemaining" }
            },
            new
            {
                id = "document_expiring",
                name = "Document Expiring",
                description = "Alert when supplier documents are about to expire",
                category = "document",
                variables = new[] { "docType", "originalName", "expiresAt", "supplierName", "daysRemaining" }
            },
            new
            {
                id = "document_expired",
                name = "Document Expired",
                description = "Alert when supplier documents have expired",
                category = "document",
                variables = new[] { "docType", "originalName", "expiresAt", "supplierName", "daysExpired" }
            },
            new
            {
                id = "quote_modification",
                name = "Quote Modified",
                description = "Notification when a supplier modifies their quote",
                category = "rfq",
                variables = new[] { "rfqTitle", "supplierName", "version" }
            },
            new
            {
                id = "rfq_published",
                name = "RFQ Published",
                description = "Notification when an RFQ is published to suppliers",
                category = "rfq",
                variables = new[] { "rfqTitle", "rfqId", "link", "previousStep" }
            },
            new
            {
                id = "rfq_quote_submitted",
                name = "RFQ Quote Submitted",
                description = "Notification to purchasers when a supplier submits a quote",
                category = "rfq",
                variables = new[] { "rfqTitle", "rfqId", "link", "previousStep" }
            },
            new
            {
                id = "rfq_pending_director",
                name = "RFQ Pending Director Approval",
                description = "Notification to procurement director after purchaser submits approval",
                category = "rfq",
                variables = new[] { "rfqTitle", "rfqId", "link", "previousStep" }
            },
            new
            {
                id = "rfq_director_approved",
                name = "RFQ Director Approved",
                description = "Notification to purchasers when director approves",
                category = "rfq",
                variables = new[] { "rfqTitle", "rfqId", "link", "previousStep" }
            },
            new
            {
                id = "rfq_director_rejected",
                name = "RFQ Director Rejected",
                description = "Notification to purchasers when director rejects",
                category = "rfq",
                variables = new[] { "rfqTitle", "rfqId", "link", "previousStep" }
            },
            new
            {
                id = "rfq_pr_exported",
                name = "PR Exported",
                description = "Notification to department users after purchaser exports PR",
                category = "rfq",
                variables = new[] { "rfqTitle", "rfqId", "link", "previousStep" }
            },
            new
            {
                id = "registration_pending_approval",
                name = "Supplier Registration Pending Approval",
                description = "Notification to next approver when registration moves forward",
                category = "registration",
                variables = new[]
                {
                    "applicationId",
                    "companyName",
                    "currentStep",
                    "nextStep",
                    "contactName",
                    "contactEmail",
                    "contactPhone",
                    "submittedAt"
                }
            },
            new
            {
                id = "registration_rejected",
                name = "Supplier Registration Rejected",
                description = "Notification to supplier when registration is rejected",
                category = "registration",
                variables = new[]
                {
                    "applicationId",
                    "companyName",
                    "rejectedStep",
                    "reason",
                    "contactName",
                    "contactEmail",
                    "contactPhone",
                    "submittedAt"
                }
            },
            new
            {
                id = "registration_approved",
                name = "Supplier Registration Approved",
                description = "Notification to supplier when registration is approved",
                category = "registration",
                variables = new[]
                {
                    "companyName",
                    "supplierCode",
                    "loginEmail",
                    "defaultPassword",
                    "activatedAt",
                    "loginUrl"
                }
            }
        };

        return Ok(new { data = templates });
    }

    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var providers = new[]
        {
            new
            {
                id = "sendgrid",
                name = "SendGrid",
                host = "smtp.sendgrid.net",
                port = 587,
                secure = false,
                userLabel = "API Key Username (usually \"apikey\")",
                passwordLabel = "API Key",
                docs = (string?)"https://docs.sendgrid.com/for-developers/sending-email/integrating-with-the-smtp-api",
                note = (string?)null
            },
            new
            {
                id = "aws-ses",
                name = "Amazon SES",
                host = "email-smtp.us-east-1.amazonaws.com",
                port = 587,
                secure = false,
                userLabel = "SMTP Username",
                passwordLabel = "SMTP Password",
                docs = (string?)"https://docs.aws.amazon.com/ses/latest/dg/smtp-credentials.html",
                note = (string?)"Remember to verify your sender email address in SES console"
            },
            new
            {
                id = "gmail",
                name = "Gmail",
                host = "smtp.gmail.com",
                port = 587,
                secure = false,
                userLabel = "Gmail Address",
                passwordLabel = "App Password (not your Gmail password)",
                docs = (string?)"https://support.google.com/accounts/answer/185833",
                note = (string?)"You must enable 2FA and create an App Password"
            },
            new
            {
                id = "outlook",
                name = "Outlook/Office 365",
                host = "smtp.office365.com",
                port = 587,
                secure = false,
                userLabel = "Email Address",
                passwordLabel = "Password",
                docs = (string?)"https://support.microsoft.com/en-us/office/pop-imap-and-smtp-settings-8361e398-8af4-4e97-b147-6c6c4ac95353",
                note = (string?)null
            },
            new
            {
                id = "mailgun",
                name = "Mailgun",
                host = "smtp.mailgun.org",
                port = 587,
                secure = false,
                userLabel = "SMTP Username (from Mailgun dashboard)",
                passwordLabel = "SMTP Password",
                docs = (string?)"https://documentation.mailgun.com/en/latest/user_manual.html#sending-via-smtp",
                note = (string?)null
            },
            new
            {
                id = "custom",
                name = "Custom SMTP Server",
                host = string.Empty,
                port = 587,
                secure = false,
                userLabel = "Username",
                passwordLabel = "Password",
                docs = (string?)null,
                note = (string?)null
            }
        };

        return Ok(new { data = providers });
    }

    private async Task<JsonElement?> LoadConfigAsync(CancellationToken cancellationToken)
    {
        var existing = await _dbContext.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == ConfigKey, cancellationToken);

        if (existing == null || string.IsNullOrWhiteSpace(existing.Value))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(existing.Value);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveConfigAsync(string payload, CancellationToken cancellationToken)
    {
        var actor = HttpContext.GetAuthUser();
        var now = DateTimeOffset.UtcNow.ToString("o");
        var existing = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(item => item.Key == ConfigKey, cancellationToken);

        if (existing == null)
        {
            _dbContext.SystemConfigs.Add(new SystemConfig
            {
                Key = ConfigKey,
                Value = payload,
                UpdatedAt = now,
                UpdatedBy = actor?.Name ?? actor?.Id ?? "system"
            });
        }
        else
        {
            existing.Value = payload;
            existing.UpdatedAt = now;
            existing.UpdatedBy = actor?.Name ?? actor?.Id ?? "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IActionResult? RequirePermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.All(granted.Contains))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }

    private static string? EncryptPassword(string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return null;
        }

        try
        {
            var iv = RandomNumberGenerator.GetBytes(16);
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DerivedKey.Value;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return $"{ToHex(iv)}:{ToHex(cipherBytes)}";
        }
        catch
        {
            return null;
        }
    }

    private static string? DecryptPassword(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return null;
        }

        if (!cipherText.Contains(':'))
        {
            return cipherText;
        }

        var parts = cipherText.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        try
        {
            var iv = FromHex(parts[0]);
            var cipherBytes = FromHex(parts[1]);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DerivedKey.Value;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    // Match Node's crypto.scryptSync(CONFIG_ENCRYPTION_KEY, "salt", 32).
    private static byte[] CreateDerivedKey()
    {
        var rawKey = Environment.GetEnvironmentVariable(EncryptionKeyEnv);
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            rawKey = DefaultEncryptionKey;
        }

        return Scrypt(
            Encoding.UTF8.GetBytes(rawKey),
            Encoding.UTF8.GetBytes("salt"),
            ScryptCost,
            ScryptBlockSize,
            ScryptParallel,
            ScryptKeyLength);
    }

    private static byte[] Scrypt(byte[] password, byte[] salt, int cost, int blockSize, int parallel, int derivedKeyLength)
    {
        if (cost <= 1 || (cost & (cost - 1)) != 0)
        {
            throw new ArgumentException("Cost must be a power of two.", nameof(cost));
        }

        var blockLength = 128 * blockSize;
        var buffer = Pbkdf2(password, salt, parallel * blockLength);

        for (var i = 0; i < parallel; i++)
        {
            SMix(buffer, i * blockLength, blockSize, cost);
        }

        return Pbkdf2(password, buffer, derivedKeyLength);
    }

    private static void SMix(byte[] buffer, int offset, int blockSize, int cost)
    {
        var blockLength = 128 * blockSize;
        var x = new byte[blockLength];
        Buffer.BlockCopy(buffer, offset, x, 0, blockLength);

        var v = new byte[cost * blockLength];
        var y = new byte[blockLength];
        var scratch = new byte[64];

        for (var i = 0; i < cost; i++)
        {
            Buffer.BlockCopy(x, 0, v, i * blockLength, blockLength);
            BlockMix(x, blockSize, y, scratch);
            Swap(ref x, ref y);
        }

        for (var i = 0; i < cost; i++)
        {
            var j = (int)(Integerify(x, blockSize) & (uint)(cost - 1));
            Xor(x, v, j * blockLength, blockLength);
            BlockMix(x, blockSize, y, scratch);
            Swap(ref x, ref y);
        }

        Buffer.BlockCopy(x, 0, buffer, offset, blockLength);
    }

    private static void BlockMix(byte[] buffer, int blockSize, byte[] output, byte[] scratch)
    {
        const int blockLength = 64;
        Buffer.BlockCopy(buffer, (2 * blockSize - 1) * blockLength, scratch, 0, blockLength);

        for (var i = 0; i < 2 * blockSize; i++)
        {
            Xor(scratch, buffer, i * blockLength, blockLength);
            Salsa208(scratch);
            Buffer.BlockCopy(scratch, 0, output, i * blockLength, blockLength);
        }

        var outIndex = 0;
        for (var i = 0; i < blockSize; i++)
        {
            Buffer.BlockCopy(output, i * 2 * blockLength, buffer, outIndex, blockLength);
            outIndex += blockLength;
        }

        for (var i = 0; i < blockSize; i++)
        {
            Buffer.BlockCopy(output, (i * 2 + 1) * blockLength, buffer, outIndex, blockLength);
            outIndex += blockLength;
        }
    }

    private static void Salsa208(byte[] block)
    {
        Span<uint> x = stackalloc uint[16];
        for (var i = 0; i < 16; i++)
        {
            x[i] = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(i * 4, 4));
        }

        Span<uint> z = stackalloc uint[16];
        x.CopyTo(z);

        for (var i = 0; i < 8; i += 2)
        {
            x[4] ^= RotateLeft(x[0] + x[12], 7);
            x[8] ^= RotateLeft(x[4] + x[0], 9);
            x[12] ^= RotateLeft(x[8] + x[4], 13);
            x[0] ^= RotateLeft(x[12] + x[8], 18);

            x[9] ^= RotateLeft(x[5] + x[1], 7);
            x[13] ^= RotateLeft(x[9] + x[5], 9);
            x[1] ^= RotateLeft(x[13] + x[9], 13);
            x[5] ^= RotateLeft(x[1] + x[13], 18);

            x[14] ^= RotateLeft(x[10] + x[6], 7);
            x[2] ^= RotateLeft(x[14] + x[10], 9);
            x[6] ^= RotateLeft(x[2] + x[14], 13);
            x[10] ^= RotateLeft(x[6] + x[2], 18);

            x[3] ^= RotateLeft(x[15] + x[11], 7);
            x[7] ^= RotateLeft(x[3] + x[15], 9);
            x[11] ^= RotateLeft(x[7] + x[3], 13);
            x[15] ^= RotateLeft(x[11] + x[7], 18);

            x[1] ^= RotateLeft(x[0] + x[3], 7);
            x[2] ^= RotateLeft(x[1] + x[0], 9);
            x[3] ^= RotateLeft(x[2] + x[1], 13);
            x[0] ^= RotateLeft(x[3] + x[2], 18);

            x[6] ^= RotateLeft(x[5] + x[4], 7);
            x[7] ^= RotateLeft(x[6] + x[5], 9);
            x[4] ^= RotateLeft(x[7] + x[6], 13);
            x[5] ^= RotateLeft(x[4] + x[7], 18);

            x[11] ^= RotateLeft(x[10] + x[9], 7);
            x[8] ^= RotateLeft(x[11] + x[10], 9);
            x[9] ^= RotateLeft(x[8] + x[11], 13);
            x[10] ^= RotateLeft(x[9] + x[8], 18);

            x[12] ^= RotateLeft(x[15] + x[14], 7);
            x[13] ^= RotateLeft(x[12] + x[15], 9);
            x[14] ^= RotateLeft(x[13] + x[12], 13);
            x[15] ^= RotateLeft(x[14] + x[13], 18);
        }

        for (var i = 0; i < 16; i++)
        {
            x[i] += z[i];
        }

        for (var i = 0; i < 16; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(i * 4, 4), x[i]);
        }
    }

    private static uint RotateLeft(uint value, int shift)
    {
        return (value << shift) | (value >> (32 - shift));
    }

    private static ulong Integerify(byte[] buffer, int blockSize)
    {
        var index = (2 * blockSize - 1) * 64;
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(index, 8));
    }

    private static void Xor(byte[] target, byte[] source, int sourceOffset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            target[i] ^= source[sourceOffset + i];
        }
    }

    private static void Swap(ref byte[] first, ref byte[] second)
    {
        var temp = first;
        first = second;
        second = temp;
    }

    private static byte[] Pbkdf2(byte[] password, byte[] salt, int length)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 1, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(length);
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static byte[] FromHex(string hex)
    {
        return Convert.FromHexString(hex);
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

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildTestEmailHtml(
        string provider,
        string host,
        int port,
        bool secure,
        string testedBy,
        string timestamp)
    {
        var safeProvider = WebUtility.HtmlEncode(provider);
        var safeHost = WebUtility.HtmlEncode(host);
        var safeSecurity = WebUtility.HtmlEncode(secure ? "SSL/TLS" : "STARTTLS");
        var safeTime = WebUtility.HtmlEncode(timestamp);
        var safeTester = WebUtility.HtmlEncode(testedBy);

        return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
    .header {{ background-color: #409eff; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
    .content {{ background-color: #f9f9f9; padding: 30px; border: 1px solid #ddd; }}
    .badge {{ display: inline-block; background-color: #67c23a; color: white; padding: 8px 16px; border-radius: 4px; font-weight: bold; }}
    .info-table {{ width: 100%; margin: 20px 0; }}
    .info-table td {{ padding: 8px; border-bottom: 1px solid #ddd; }}
    .info-table td:first-child {{ font-weight: bold; width: 150px; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>SMTP Test Successful</h1>
    </div>
    <div class=""content"">
      <p><span class=""badge"">Connection Verified</span></p>
      <p>Your SMTP configuration is working correctly.</p>
      <table class=""info-table"">
        <tr><td>Provider:</td><td>{safeProvider}</td></tr>
        <tr><td>SMTP Server:</td><td>{safeHost}:{port}</td></tr>
        <tr><td>Security:</td><td>{safeSecurity}</td></tr>
        <tr><td>Test Time:</td><td>{safeTime}</td></tr>
        <tr><td>Tested By:</td><td>{safeTester}</td></tr>
      </table>
      <p>Best regards,<br>Supplier Management System</p>
    </div>
  </div>
</body>
</html>";
    }

    private static string BuildTestEmailText(
        string provider,
        string host,
        int port,
        bool secure,
        string testedBy,
        string timestamp)
    {
        var security = secure ? "SSL/TLS" : "STARTTLS";
        return $@"
SMTP Test Successful

Your SMTP configuration is working correctly.

Configuration Details:
- Provider: {provider}
- SMTP Server: {host}:{port}
- Security: {security}
- Test Time: {timestamp}
- Tested By: {testedBy}

Best regards,
Supplier Management System
";
    }
}
