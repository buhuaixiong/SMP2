using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.DTOs.Registrations;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.Registrations;

public sealed record RegistrationDraftSaveResult(
    string DraftToken,
    string Status,
    string? ExpiresAt,
    string? LastStep,
    SupplierRegistrationValidationResult Validation,
    SupplierRegistrationNormalizedPayload Normalized,
    bool IsNew);

public sealed record RegistrationDraftGetResult(
    string DraftToken,
    string Status,
    string? LastStep,
    string? ExpiresAt,
    string? CreatedAt,
    string? UpdatedAt,
    JsonElement Form,
    JsonElement Normalized,
    Dictionary<string, string> Errors,
    int? SubmittedApplicationId);

public sealed record RegistrationSubmitResult(
    int ApplicationId,
    string? SupplierCode,
    string TrackingToken,
    string TrackingUrl,
    string TrackingUsername,
    string TrackingPassword,
    string TrackingMessage,
    string? DraftToken,
    string? AssignedPurchaserId,
    string? AssignedPurchaserName,
    string? AssignedPurchaserEmail,
    string Status,
    string NextStep);

public sealed partial class SupplierRegistrationService
{
    private const string DraftStatusActive = "active";
    private const string DraftStatusSubmitted = "submitted";
    private const string DraftStatusExpired = "expired";
    private const int DraftTtlDays = 30;
    private const int MaxDocumentSizeBytes = 10 * 1024 * 1024;
    private const string DefaultTrackingPassword = "666666";
    private const string SmtpConfigKey = "smtp_config";
    private const string DefaultFromAddress = "no-reply@supplier-system.local";
    private const string DefaultFromName = "Supplier Management System";

    private static readonly IReadOnlyDictionary<string, string> ExtensionByMime =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = "pdf",
            ["image/jpeg"] = "jpg",
            ["image/png"] = "png",
            ["image/jpg"] = "jpg",
            ["image/gif"] = "gif",
            ["image/bmp"] = "bmp",
            ["application/msword"] = "doc",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "docx",
        };

    private static readonly Regex DataUrlRegex =
        new("^data:([^;]+);base64,(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions CamelCaseJson =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

    public async Task<RegistrationDraftSaveResult> SaveDraftAsync(
        JsonElement formPayload,
        string? draftToken,
        string? lastStep,
        CancellationToken cancellationToken)
    {
        if (formPayload.ValueKind != JsonValueKind.Object || !formPayload.EnumerateObject().Any())
        {
            throw new HttpResponseException(400, new { message = "INVALID_PAYLOAD", error = "MISSING_FORM_DATA" });
        }

        var trimmedToken = string.IsNullOrWhiteSpace(draftToken) ? null : draftToken.Trim();
        SupplierRegistrationDraft? existingDraft = null;

        if (!string.IsNullOrWhiteSpace(trimmedToken))
        {
            existingDraft = await _dbContext.SupplierRegistrationDrafts
                .FirstOrDefaultAsync(draft => draft.DraftToken == trimmedToken, cancellationToken);

            if (existingDraft?.Status == DraftStatusSubmitted)
            {
                throw new HttpResponseException(409, new
                {
                    message = "DRAFT_ALREADY_SUBMITTED",
                    submittedApplicationId = existingDraft.SubmittedApplicationId,
                });
            }
        }

        var validation = SupplierRegistrationValidation.ValidateRegistration(
            formPayload,
            SupplierRegistrationValidationMode.Draft);

        var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var token = existingDraft?.DraftToken ?? trimmedToken ?? Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(DraftTtlDays).ToString("o", CultureInfo.InvariantCulture);
        var normalizedJson = JsonSerializer.Serialize(validation.Normalized, CamelCaseJson);
        var errorsJson = JsonSerializer.Serialize(validation.Errors, CamelCaseJson);
        var isNew = existingDraft == null;

        if (existingDraft == null)
        {
            existingDraft = new SupplierRegistrationDraft
            {
                DraftToken = token,
                CreatedAt = nowIso,
            };
            _dbContext.SupplierRegistrationDrafts.Add(existingDraft);
        }

        existingDraft.Status = DraftStatusActive;
        existingDraft.RawPayload = formPayload.GetRawText();
        existingDraft.NormalizedPayload = normalizedJson;
        existingDraft.ValidationErrors = errorsJson;
        existingDraft.LastStep = string.IsNullOrWhiteSpace(lastStep) ? null : lastStep.Trim();
        existingDraft.ContactEmail = validation.Normalized.ContactEmail;
        existingDraft.ProcurementEmail = validation.Normalized.ProcurementEmail;
        existingDraft.ExpiresAt = expiresAt;
        existingDraft.UpdatedAt = nowIso;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RegistrationDraftSaveResult(
            token,
            DraftStatusActive,
            expiresAt,
            existingDraft.LastStep,
            validation,
            validation.Normalized,
            isNew);
    }

    public async Task<RegistrationDraftGetResult> GetDraftAsync(
        string draftToken,
        CancellationToken cancellationToken)
    {
        var trimmed = draftToken?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new HttpResponseException(400, new { message = "INVALID_DRAFT_TOKEN" });
        }

        var draft = await _dbContext.SupplierRegistrationDrafts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DraftToken == trimmed, cancellationToken);

        if (draft == null)
        {
            throw new HttpResponseException(404, new { message = "DRAFT_NOT_FOUND" });
        }

        if (draft.Status == DraftStatusSubmitted)
        {
            throw new HttpResponseException(409, new
            {
                message = "DRAFT_ALREADY_SUBMITTED",
                submittedApplicationId = draft.SubmittedApplicationId,
            });
        }

        if (IsDraftExpired(draft.ExpiresAt))
        {
            draft.Status = DraftStatusExpired;
            draft.UpdatedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            _dbContext.SupplierRegistrationDrafts.Update(draft);
            await _dbContext.SaveChangesAsync(cancellationToken);

            throw new HttpResponseException(410, new { message = "DRAFT_EXPIRED" });
        }

        var rawPayload = ParseJsonElement(draft.RawPayload);
        var normalizedPayload = ParseJsonElement(draft.NormalizedPayload);
        var errors = ParseErrors(draft.ValidationErrors);

        return new RegistrationDraftGetResult(
            draft.DraftToken,
            draft.Status ?? DraftStatusActive,
            draft.LastStep,
            draft.ExpiresAt,
            draft.CreatedAt,
            draft.UpdatedAt,
            rawPayload,
            normalizedPayload,
            errors,
            draft.SubmittedApplicationId);
    }

    public async Task<RegistrationSubmitResult> SubmitRegistrationAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new HttpResponseException(400, new { message = "VALIDATION_FAILED", errors = new { } });
        }

        var draftToken = GetStringProperty(payload, "draftToken");
        var validation = SupplierRegistrationValidation.ValidateRegistration(
            payload,
            SupplierRegistrationValidationMode.Final);

        if (!validation.Valid)
        {
            throw new HttpResponseException(400, new
            {
                message = "VALIDATION_FAILED",
                errors = validation.Errors,
            });
        }

        var normalized = validation.Normalized;
        var trackingAccountId = normalized.ContactEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(trackingAccountId))
        {
            var existingUsername = await _dbContext.Users.AsNoTracking()
                .Where(u => u.Username.ToLower() == trackingAccountId)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(existingUsername) &&
                !string.Equals(existingUsername, trackingAccountId, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpResponseException(409, new
                {
                    message = "DUPLICATE_REGISTRATION",
                    error = "A user with this contact email already exists.",
                    field = "contactEmail"
                });
            }
        }

        DocumentMeta businessLicense;
        DocumentMeta bankAccount;
        var uploadRoot = ResolveUploadRoot();
        var savedPaths = new List<string>();
        try
        {
            businessLicense = PersistRegistrationDocument(normalized.BusinessLicenseFile, "business-license");
            if (!string.IsNullOrWhiteSpace(businessLicense.FilePath))
            {
                savedPaths.Add(Path.Combine(uploadRoot, businessLicense.FilePath));
            }

            bankAccount = PersistRegistrationDocument(normalized.BankAccountFile, "bank-account");
            if (!string.IsNullOrWhiteSpace(bankAccount.FilePath))
            {
                savedPaths.Add(Path.Combine(uploadRoot, bankAccount.FilePath));
            }
        }
        catch (HttpResponseException)
        {
            CleanupSavedFiles(savedPaths);
            throw;
        }
        catch (Exception ex)
        {
            CleanupSavedFiles(savedPaths);
            throw new HttpResponseException(400, new { message = ex.Message });
        }

        try
        {
            var classification = normalized.SupplierClassification;
            if (string.IsNullOrWhiteSpace(classification) ||
                !SupplierRegistrationValidation.Classifications.Contains(classification))
            {
                throw new HttpResponseException(400, new { message = "supplierClassification must be DM or IDM" });
            }

            var currency = normalized.OperatingCurrency;
            if (string.IsNullOrWhiteSpace(currency) ||
                !SupplierRegistrationValidation.Currencies.Contains(currency))
            {
                throw new HttpResponseException(400, new { message = "operatingCurrency must be one of: RMB, USD, EUR, GBP, KRW, THB, JPY" });
            }

            var prefixes = ResolveSupplierCodePrefixes(classification, currency);
            if (prefixes.Count == 0)
            {
                throw new HttpResponseException(400, new
                {
                    message = $"Unsupported currency {currency} for classification {classification}"
                });
            }

            await EnsureRegistrationNotBlacklistedAsync(normalized, cancellationToken);

            // 检查是否存在重复的注册申请
            await EnsureNoDuplicateRegistrationAsync(normalized, draftToken, cancellationToken);

            var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            var assignedPurchaser = await FindAssignedPurchaserAsync(normalized.ProcurementEmail, cancellationToken);
            if (!string.IsNullOrWhiteSpace(normalized.ProcurementEmail) && assignedPurchaser == null)
            {
                throw new HttpResponseException(400, new
                {
                    message = "VALIDATION_FAILED",
                    errors = new Dictionary<string, string> { { "procurementEmail", "NOT_FOUND" } },
                });
            }
            var paymentMethodsJson = normalized.PaymentMethods.Count > 0
                ? JsonSerializer.Serialize(normalized.PaymentMethods, CamelCaseJson)
                : null;

            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var application = new SupplierRegistrationApplication
            {
                CompanyName = normalized.CompanyName ?? string.Empty,
                EnglishName = normalized.EnglishName,
                ChineseName = normalized.ChineseName,
                CompanyType = normalized.CompanyType ?? string.Empty,
                CompanyTypeOther = normalized.CompanyTypeOther,
                AuthorizedCapital = normalized.AuthorizedCapital,
                IssuedCapital = normalized.IssuedCapital,
                Directors = normalized.Directors,
                Owners = normalized.Owners,
                RegisteredOffice = normalized.RegisteredOffice ?? string.Empty,
                BusinessRegistrationNumber = normalized.BusinessRegistrationNumber ?? string.Empty,
                BusinessAddress = normalized.BusinessAddress ?? string.Empty,
                BusinessPhone = normalized.BusinessPhone,
                BusinessFax = normalized.BusinessFax,
                ContactName = normalized.ContactName ?? string.Empty,
                ContactEmail = normalized.ContactEmail ?? string.Empty,
                ProcurementEmail = normalized.ProcurementEmail,
                ContactPhone = normalized.ContactPhone ?? string.Empty,
                FinanceContactName = normalized.FinanceContactName,
                FinanceContactEmail = normalized.FinanceContactEmail,
                FinanceContactPhone = normalized.FinanceContactPhone,
                BusinessNature = normalized.BusinessNature,
                OperatingCurrency = normalized.OperatingCurrency ?? string.Empty,
                DeliveryLocation = normalized.DeliveryLocation ?? string.Empty,
                ShipCode = normalized.ShipCode ?? string.Empty,
                ProductOrigin = normalized.ProductOrigin,
                ProductTypes = normalized.ProductTypes,
                InvoiceType = normalized.InvoiceType,
                PaymentTermsDays = normalized.PaymentTermsDays,
                PaymentMethods = paymentMethodsJson,
                PaymentMethodsOther = normalized.PaymentMethodsOther,
                BankName = normalized.BankName ?? string.Empty,
                BankAddress = normalized.BankAddress ?? string.Empty,
                BankAccountNumber = normalized.BankAccountNumber ?? string.Empty,
                SwiftCode = normalized.SwiftCode,
                Notes = normalized.Notes,
                SupplierClassification = normalized.SupplierClassification,
                SupplierCode = null,
                SupplierId = null,
                TempAccountUserId = null,
                DraftToken = string.IsNullOrWhiteSpace(draftToken) ? null : draftToken.Trim(),
                BaselineVersion = 0,
                Status = RegistrationConstants.RegistrationStatusPendingPurchaser,
                AssignedPurchaserEmail = normalized.ProcurementEmail,
                AssignedPurchaserId = assignedPurchaser?.Id,
                BusinessLicenseFileName = businessLicense.OriginalName,
                BusinessLicenseFilePath = businessLicense.FilePath,
                BusinessLicenseFileMime = businessLicense.MimeType,
                BusinessLicenseFileSize = businessLicense.Size,
                BankAccountFileName = bankAccount.OriginalName,
                BankAccountFilePath = bankAccount.FilePath,
                BankAccountFileMime = bankAccount.MimeType,
                BankAccountFileSize = bankAccount.Size,
                CreatedAt = nowIso,
                UpdatedAt = nowIso,
            };

            _dbContext.SupplierRegistrationApplications.Add(application);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var trackingPasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultTrackingPassword, 12);
            var existingUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == trackingAccountId, cancellationToken);

            if (existingUser == null)
            {
                existingUser = new User
                {
                    Id = trackingAccountId,
                    CreatedAt = nowIso,
                };
                _dbContext.Users.Add(existingUser);
            }

            if (string.IsNullOrWhiteSpace(existingUser.CreatedAt))
            {
                existingUser.CreatedAt = nowIso;
            }

            existingUser.Name = $"{normalized.CompanyName} (Tracking Account)";
            existingUser.Username = trackingAccountId;
            existingUser.Role = "tracking";
            existingUser.Password = trackingPasswordHash;
            existingUser.AuthVersion = existingUser.AuthVersion <= 0 ? 1 : existingUser.AuthVersion + 1;
            existingUser.Email = normalized.ContactEmail;
            existingUser.AccountType = "tracking";
            existingUser.RelatedApplicationId = application.Id;
            existingUser.MustChangePassword = false;
            existingUser.ForcePasswordReset = false;
            if (string.IsNullOrWhiteSpace(existingUser.Status))
            {
                existingUser.Status = "active";
            }
            existingUser.UpdatedAt = nowIso;

            application.TrackingAccountId = trackingAccountId;
            application.UpdatedAt = nowIso;
            _dbContext.SupplierRegistrationApplications.Update(application);

            if (!string.IsNullOrWhiteSpace(draftToken))
            {
                var draft = await _dbContext.SupplierRegistrationDrafts
                    .FirstOrDefaultAsync(d => d.DraftToken == draftToken, cancellationToken);
                if (draft != null)
                {
                    draft.Status = DraftStatusSubmitted;
                    draft.SubmittedApplicationId = application.Id;
                    draft.UpdatedAt = nowIso;
                    _dbContext.SupplierRegistrationDrafts.Update(draft);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = trackingAccountId,
                ActorName = normalized.CompanyName,
                EntityType = "supplier_registration",
                EntityId = application.Id.ToString(CultureInfo.InvariantCulture),
                Action = "submit",
                Changes = JsonSerializer.Serialize(new
                {
                    supplierClassification = normalized.SupplierClassification,
                    assignedPurchaser = assignedPurchaser?.Name ?? "Not assigned",
                    status = RegistrationConstants.RegistrationStatusPendingPurchaser,
                }, CamelCaseJson),
            });

            await tx.CommitAsync(cancellationToken);

            await TrySendRegistrationNotificationAsync(application, normalized, cancellationToken);

            var trackingToken = Guid.NewGuid().ToString();

            return new RegistrationSubmitResult(
                application.Id,
                null,
                trackingToken,
                $"/registration-status/{trackingToken}",
                trackingAccountId,
                DefaultTrackingPassword,
                "Registration submitted. Use the tracking account to follow approval progress.",
                string.IsNullOrWhiteSpace(draftToken) ? null : draftToken.Trim(),
                assignedPurchaser?.Id,
                assignedPurchaser?.Name,
                assignedPurchaser?.Email,
                RegistrationConstants.RegistrationStatusPendingPurchaser,
                "Quality Manager Approval");
        }
        catch
        {
            CleanupSavedFiles(savedPaths);
            throw;
        }
    }

    private async Task EnsureRegistrationNotBlacklistedAsync(
        SupplierRegistrationNormalizedPayload normalized,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var creditCode = normalized.BusinessRegistrationNumber?.Trim().ToLowerInvariant();
        var email = normalized.ContactEmail?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(creditCode) && string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var query = _dbContext.SupplierRegistrationBlacklist.AsNoTracking()
            .Where(b => b.IsActive);

        if (!string.IsNullOrWhiteSpace(creditCode) && !string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(b => (b.BlacklistType == "credit_code" && b.BlacklistValue == creditCode) ||
                                     (b.BlacklistType == "email" && b.BlacklistValue == email));
        }
        else if (!string.IsNullOrWhiteSpace(creditCode))
        {
            query = query.Where(b => b.BlacklistType == "credit_code" && b.BlacklistValue == creditCode);
        }
        else
        {
            query = query.Where(b => b.BlacklistType == "email" && b.BlacklistValue == email);
        }

        var candidates = await query.ToListAsync(cancellationToken);

        foreach (var entry in candidates)
        {
            if (IsBlacklistExpired(entry.ExpiresAt, now))
            {
                continue;
            }

            var reason = entry.Severity != null && entry.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase)
                ? "Permanently blocked"
                : entry.Reason;

            throw new HttpResponseException(403, new
            {
                message = entry.BlacklistType == "credit_code"
                    ? "Registration blocked: This business registration number is not eligible for registration."
                    : "Registration blocked: This email address is not eligible for registration.",
                reason,
                blacklist_type = entry.BlacklistType,
            });
        }
    }

    private async Task EnsureNoDuplicateRegistrationAsync(
        SupplierRegistrationNormalizedPayload normalized,
        string? draftToken,
        CancellationToken cancellationToken)
    {
        var creditCode = normalized.BusinessRegistrationNumber?.Trim().ToLowerInvariant();
        var email = normalized.ContactEmail?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(creditCode) && string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        IQueryable<SupplierRegistrationApplication> query = _dbContext.SupplierRegistrationApplications;

        // 如果有 draftToken，排除同一草稿的申请
        if (!string.IsNullOrWhiteSpace(draftToken))
        {
            query = query.Where(a => a.DraftToken != draftToken.Trim());
        }

        // Only block duplicates for in-flight applications.
        var activatedStatus = RegistrationConstants.RegistrationStatusActivated;
        var rejectedStatus = RegistrationConstants.RegistrationStatusRejected;
        query = query.Where(a => a.Status == null || (a.Status != activatedStatus && a.Status != rejectedStatus));

        // 检查信用代码重复
        if (!string.IsNullOrWhiteSpace(creditCode))
        {
            var existingByCreditCode = await query
                .Where(a => a.BusinessRegistrationNumber.ToLower() == creditCode)
                .Select(a => new { a.Id, a.Status, a.CompanyName })
                .FirstOrDefaultAsync(cancellationToken);

            if (existingByCreditCode != null)
            {
                throw new HttpResponseException(409, new
                {
                    message = "DUPLICATE_REGISTRATION",
                    error = "A registration with this business registration number already exists.",
                    existingApplicationId = existingByCreditCode.Id,
                    existingStatus = existingByCreditCode.Status,
                    existingCompanyName = existingByCreditCode.CompanyName,
                    field = "businessRegistrationNumber"
                });
            }
        }

        // 检查邮箱重复
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingByEmail = await query
                .Where(a => a.ContactEmail.ToLower() == email)
                .Select(a => new { a.Id, a.Status, a.CompanyName })
                .FirstOrDefaultAsync(cancellationToken);

            if (existingByEmail != null)
            {
                throw new HttpResponseException(409, new
                {
                    message = "DUPLICATE_REGISTRATION",
                    error = "A registration with this contact email already exists.",
                    existingApplicationId = existingByEmail.Id,
                    existingStatus = existingByEmail.Status,
                    existingCompanyName = existingByEmail.CompanyName,
                    field = "contactEmail"
                });
            }
        }
    }

    private async Task<AssignedPurchaser?> FindAssignedPurchaserAsync(
        string? procurementEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(procurementEmail))
        {
            return null;
        }

        var normalizedEmail = procurementEmail.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Email != null && u.Email.ToLower() == normalizedEmail && u.Role == "purchaser")
            .Select(u => new AssignedPurchaser(u.Id, u.Name, u.Email))
            .FirstOrDefaultAsync(cancellationToken);

        return user;
    }

    private static bool IsBlacklistExpired(string? expiresAt, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(expiresAt))
        {
            return false;
        }

        if (!DateTime.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry))
        {
            return false;
        }

        return expiry.ToUniversalTime() <= nowUtc;
    }

    private static bool IsDraftExpired(string? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(expiresAt))
        {
            return false;
        }

        if (!DateTime.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return false;
        }

        return parsed.ToUniversalTime() < DateTime.UtcNow;
    }

    private static Dictionary<string, string> ParseErrors(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(payload) ??
                   new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static JsonElement ParseJsonElement(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(payload);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }

    private static string? GetStringProperty(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return value.ToString();
    }

    private DocumentMeta PersistRegistrationDocument(SupplierRegistrationDocumentUpload? document, string slug)
    {
        if (document == null)
        {
            throw new HttpResponseException(400, new { message = $"INVALID_{slug.ToUpperInvariant()}_DOCUMENT" });
        }

        var decoded = DecodeBase64Content(document.Content, document.Type);
        if (decoded.Buffer.Length == 0)
        {
            throw new HttpResponseException(400, new { message = $"INVALID_{slug.ToUpperInvariant()}_DOCUMENT" });
        }

        if (decoded.Buffer.Length > MaxDocumentSizeBytes)
        {
            throw new HttpResponseException(400, new { message = $"{slug}_document_exceeds_limit" });
        }

        // 验证文件实际类型（通过文件头字节）
        var detectedMimeType = DetectMimeType(decoded.Buffer);
        if (detectedMimeType == null)
        {
            throw new HttpResponseException(400, new { message = $"INVALID_{slug.ToUpperInvariant()}_DOCUMENT", reason = "UNSUPPORTED_FILE_TYPE" });
        }

        // 验证检测到的类型是否与声称的类型匹配
        var claimedMimeType = decoded.MimeType?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(claimedMimeType) && !IsMimeTypeCompatible(claimedMimeType, detectedMimeType))
        {
            throw new HttpResponseException(400, new
            {
                message = $"INVALID_{slug.ToUpperInvariant()}_DOCUMENT",
                reason = "MIME_TYPE_MISMATCH",
                expected = detectedMimeType,
                received = claimedMimeType
            });
        }

        var originalName = SanitizeFileName(document.Name);
        var extension = ResolveExtension(originalName, detectedMimeType ?? decoded.MimeType ?? "application/octet-stream");
        var randomSuffix = Guid.NewGuid().ToString("N")[..8];
        var fileName = $"{slug}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{randomSuffix}.{extension}";
        var relativePath = Path.Combine("registration", fileName).Replace("\\", "/");

        var uploadRoot = ResolveUploadRoot();
        var absolutePath = Path.Combine(uploadRoot, relativePath);
        EnsureDirectory(Path.GetDirectoryName(absolutePath)!);

        File.WriteAllBytes(absolutePath, decoded.Buffer);

        return new DocumentMeta(originalName, relativePath, detectedMimeType ?? decoded.MimeType, decoded.Buffer.Length);
    }

    private static string? DetectMimeType(byte[] buffer)
    {
        if (buffer.Length < 4)
        {
            return null;
        }

        // JPEG
        if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // PNG
        if (buffer.Length >= 8 &&
            buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
            buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A)
        {
            return "image/png";
        }

        // GIF
        if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38)
        {
            return "image/gif";
        }

        // BMP
        if (buffer[0] == 0x42 && buffer[1] == 0x4D)
        {
            return "image/bmp";
        }

        // PDF
        if (buffer.Length >= 4 &&
            buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
        {
            return "application/pdf";
        }

        // DOC (old Word format)
        if (buffer.Length >= 8 &&
            buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0 &&
            buffer[4] == 0xA1 && buffer[5] == 0xB1 && buffer[6] == 0x1A && buffer[7] == 0xE1)
        {
            return "application/msword";
        }

        // DOCX (ZIP-based)
        if (buffer.Length >= 4 &&
            buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
        {
            return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        }

        return null;
    }

    private static bool IsMimeTypeCompatible(string claimed, string detected)
    {
        // 允许一些常见的兼容映射
        var compatibilityMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = new[] { "image/jpeg", "image/jpg" },
            ["image/jpg"] = new[] { "image/jpeg", "image/jpg" },
            ["image/png"] = new[] { "image/png" },
            ["image/gif"] = new[] { "image/gif" },
            ["image/bmp"] = new[] { "image/bmp" },
            ["application/pdf"] = new[] { "application/pdf" },
            ["application/msword"] = new[] { "application/msword" },
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        };

        if (compatibilityMap.TryGetValue(detected, out var compatible))
        {
            return compatible.Contains(claimed, StringComparer.OrdinalIgnoreCase);
        }

        return claimed.Equals(detected, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveUploadRoot()
    {
        return UploadPathHelper.GetGenericUploadsRoot(_environment);
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "document";
        }

        var trimmed = name.Trim();
        var safe = Regex.Replace(trimmed, "[^a-zA-Z0-9._-]+", "_");
        return safe.Length > 0 ? safe : "document";
    }

    private static string ResolveExtension(string name, string mime)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var extension = Path.GetExtension(name);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension.TrimStart('.').ToLowerInvariant();
            }
        }

        if (!string.IsNullOrWhiteSpace(mime) && ExtensionByMime.TryGetValue(mime.Trim(), out var mapped))
        {
            return mapped;
        }

        return "bin";
    }

    private static DecodedDocument DecodeBase64Content(string content, string? fallbackMime)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new DecodedDocument(Array.Empty<byte>(), fallbackMime ?? "application/octet-stream");
        }

        var trimmed = content.Trim();
        var mimeType = fallbackMime ?? "application/octet-stream";
        var base64 = trimmed;

        var match = DataUrlRegex.Match(trimmed);
        if (match.Success)
        {
            mimeType = match.Groups[1].Value;
            base64 = match.Groups[2].Value;
        }

        try
        {
            var buffer = Convert.FromBase64String(base64);
            return new DecodedDocument(buffer, mimeType);
        }
        catch (FormatException)
        {
            return new DecodedDocument(Array.Empty<byte>(), mimeType);
        }
    }

    private static void CleanupSavedFiles(IEnumerable<string> absolutePaths)
    {
        foreach (var path in absolutePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private sealed record DocumentMeta(string? OriginalName, string? FilePath, string? MimeType, long Size);

    private sealed record DecodedDocument(byte[] Buffer, string MimeType);

    private sealed record AssignedPurchaser(string Id, string? Name, string? Email);
}
