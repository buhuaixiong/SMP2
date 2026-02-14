using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 辅助方法

    private static readonly ProfileRequirementDefinition[] RequiredProfileFields =
    {
        new("companyName", "Company name", supplier => supplier.CompanyName),
        new("companyId", "Supplier code", supplier => supplier.CompanyId),
        new("contactPerson", "Primary contact name", supplier => supplier.ContactPerson),
        new("contactPhone", "Primary contact phone", supplier => supplier.ContactPhone),
        new("contactEmail", "Primary contact email", supplier => supplier.ContactEmail),
        new("category", "Supplier category", supplier => supplier.Category),
        new("address", "Registered address", supplier => supplier.Address),
        new("businessRegistrationNumber", "Business registration number", supplier => supplier.BusinessRegistrationNumber),
        new("paymentTerms", "Payment terms", supplier => supplier.PaymentTerms),
        new("paymentCurrency", "Payment currency", supplier => supplier.PaymentCurrency),
        new("bankAccount", "Bank account", supplier => supplier.BankAccount),
        new("region", "Region", supplier => supplier.Region)
    };

    private static readonly DocumentRequirementDefinition[] RequiredDocumentTypes =
    {
        new("business_license", "Business license", new[] { "business licence", "business_license" }),
        new("tax_certificate", "Tax / VAT certificate", new[] { "tax certificate", "vat certificate", "vat_license" }),
        new("bank_information", "Bank account information", new[] { "bank certificate", "bank statement", "banking_details" })
    };

    private async Task<SupplierResponse> EnrichSupplierAsync(Supplier supplier, bool includeExemptions, CancellationToken cancellationToken)
    {
        var response = MapToResponse(supplier);
        var tags = await GetSupplierTagsAsync(supplier.Id, cancellationToken);
        var documents = await GetSupplierDocumentsAsync(supplier.Id, cancellationToken);
        var ratingsSummary = await GetSupplierStatsAsync(supplier.Id, cancellationToken);
        var latestRating = await GetLatestRatingAsync(supplier.Id, cancellationToken);
        var contracts = await GetSupplierContractsAsync(supplier.Id, cancellationToken);
        var approvalHistory = await GetApprovalHistoryAsync(supplier.Id, cancellationToken);
        var fileApprovals = await GetFileApprovalsAsync(supplier.Id, cancellationToken);
        var files = await GetSupplierFilesAsync(supplier.Id, cancellationToken);

        var profileCompletion = response.ProfileCompletion;
        var documentCompletion = response.DocumentCompletion;
        var completionScore = response.CompletionScore;
        var completionStatus = response.CompletionStatus;
        SupplierComplianceSummaryResponse? complianceSummary = null;
        var missingRequirements = new List<SupplierMissingRequirement>();

        if (profileCompletion == null || completionScore == null)
        {
            var whitelist = includeExemptions
                ? await GetWhitelistedDocumentTypesAsync(supplier.Id, cancellationToken)
                : Array.Empty<string>();
            complianceSummary = BuildComplianceSummary(supplier, documents, whitelist);
            profileCompletion = complianceSummary.ProfileScore;
            documentCompletion = complianceSummary.DocumentScore;
            completionScore = complianceSummary.OverallScore;
            completionStatus = complianceSummary.CompletionCategory;
            missingRequirements = complianceSummary.MissingItems.ToList();
        }
        else if (includeExemptions)
        {
            var whitelist = await GetWhitelistedDocumentTypesAsync(supplier.Id, cancellationToken);
            complianceSummary = BuildComplianceSummary(supplier, documents, whitelist);
            missingRequirements = complianceSummary.MissingItems.ToList();
        }

        response.Tags = tags;
        response.Documents = documents;
        response.RatingsSummary = ratingsSummary ?? new SupplierStatsResponse();
        response.Stats = response.RatingsSummary;
        response.LatestRating = latestRating;
        response.Contracts = contracts;
        response.ApprovalHistory = approvalHistory;
        response.FileApprovals = fileApprovals;
        response.Files = files;
        response.ComplianceSummary = complianceSummary;
        response.MissingRequirements = missingRequirements;
        response.ProfileCompletion = profileCompletion;
        response.DocumentCompletion = documentCompletion;
        response.CompletionScore = completionScore;
        response.CompletionStatus = completionStatus;

        return response;
    }

    private SupplierResponse MapToResponse(Supplier supplier)
    {
        return new SupplierResponse
        {
            Id = supplier.Id,
            CompanyName = supplier.CompanyName,
            CompanyId = supplier.CompanyId,
            ContactPerson = supplier.ContactPerson,
            ContactPhone = supplier.ContactPhone,
            ContactEmail = supplier.ContactEmail,
            Category = supplier.Category,
            Address = supplier.Address,
            Status = supplier.Status,
            CurrentApprover = supplier.CurrentApprover,
            CreatedBy = supplier.CreatedBy,
            CreatedAt = supplier.CreatedAt,
            UpdatedAt = supplier.UpdatedAt,
            Notes = supplier.Notes,
            BankAccount = supplier.BankAccount,
            PaymentTerms = supplier.PaymentTerms,
            CreditRating = supplier.CreditRating,
            ServiceCategory = supplier.ServiceCategory,
            Region = supplier.Region,
            Importance = supplier.Importance,
            ComplianceStatus = supplier.ComplianceStatus,
            ComplianceNotes = supplier.ComplianceNotes,
            ComplianceOwner = supplier.ComplianceOwner,
            ComplianceReviewedAt = supplier.ComplianceReviewedAt,
            FinancialContact = supplier.FinancialContact,
            PaymentCurrency = supplier.PaymentCurrency,
            FaxNumber = supplier.FaxNumber,
            BusinessRegistrationNumber = supplier.BusinessRegistrationNumber,
            Stage = supplier.Stage,
            ProfileCompletion = supplier.ProfileCompletion,
            DocumentCompletion = supplier.DocumentCompletion,
            CompletionScore = supplier.CompletionScore,
            CompletionStatus = supplier.CompletionStatus,
            SupplierCode = supplier.SupplierCode
        };
    }

    private async Task<SupplierRatingResponse?> GetLatestRatingAsync(int supplierId, CancellationToken cancellationToken)
    {
        var rating = await _context.SupplierRatings
            .AsNoTracking()
            .Where(r => r.SupplierId == supplierId)
            .OrderByDescending(r => r.PeriodEnd ?? r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (rating == null)
        {
            return null;
        }

        return new SupplierRatingResponse
        {
            Id = rating.Id,
            SupplierId = rating.SupplierId,
            PeriodStart = rating.PeriodStart,
            PeriodEnd = rating.PeriodEnd,
            OnTimeDelivery = rating.OnTimeDelivery,
            QualityScore = rating.QualityScore,
            ServiceScore = rating.ServiceScore,
            CostScore = rating.CostScore,
            OverallScore = rating.OverallScore,
            Notes = rating.Notes,
            CreatedAt = rating.CreatedAt,
            CreatedBy = rating.CreatedBy
        };
    }

    private async Task<List<SupplierContractResponse>> GetSupplierContractsAsync(int supplierId, CancellationToken cancellationToken)
    {
        var contracts = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.SupplierId == supplierId)
            .OrderByDescending(c => c.EffectiveTo ?? c.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return contracts.Select(contract => new SupplierContractResponse
        {
            Id = contract.Id,
            Title = contract.Title,
            AgreementNumber = contract.AgreementNumber,
            Status = contract.Status,
            EffectiveFrom = contract.EffectiveFrom,
            EffectiveTo = contract.EffectiveTo,
            Amount = contract.Amount,
            Currency = contract.Currency,
            PaymentCycle = contract.PaymentCycle,
            AutoRenew = contract.AutoRenew
        }).ToList();
    }

    private async Task<List<SupplierApprovalHistoryResponse>> GetApprovalHistoryAsync(int supplierId, CancellationToken cancellationToken)
    {
        var history = await _context.ApprovalHistories
            .AsNoTracking()
            .Where(record => record.SupplierId == supplierId)
            .OrderByDescending(record => record.Date)
            .ToListAsync(cancellationToken);

        return history.Select(record => new SupplierApprovalHistoryResponse
        {
            Step = record.Step,
            Approver = record.Approver,
            Result = record.Result,
            Date = record.Date,
            Comments = NormalizeComment(record.Comments),
            Source = "status_approval",
            DecidedAt = NormalizeDateTime(record.Date),
            DecidedByName = record.Approver
        }).ToList();
    }

    private async Task<List<SupplierFileApprovalResponse>> GetFileApprovalsAsync(int supplierId, CancellationToken cancellationToken)
    {
        var approvals = await (from approval in _context.SupplierFileApprovals.AsNoTracking()
                               join upload in _context.SupplierFileUploads.AsNoTracking()
                                   on approval.UploadId equals upload.Id
                               where upload.SupplierId == supplierId
                               orderby approval.CreatedAt
                               select new { approval, upload })
            .ToListAsync(cancellationToken);

        return approvals.Select(entry => new SupplierFileApprovalResponse
        {
            Id = entry.approval.Id,
            UploadId = entry.approval.UploadId,
            StepKey = entry.approval.Step,
            StepName = entry.approval.StepName,
            Decision = entry.approval.Decision,
            Comments = NormalizeComment(entry.approval.Comments),
            DecidedById = entry.approval.ApproverId,
            DecidedByName = entry.approval.ApproverName,
            DecidedAt = NormalizeDateTime(entry.approval.CreatedAt),
            FileName = entry.upload.FileName,
            FileDescription = entry.upload.FileDescription,
            RiskLevel = entry.upload.RiskLevel,
            Source = "file_upload"
        }).ToList();
    }

    private async Task<List<SupplierFileResponse>> GetSupplierFilesAsync(int supplierId, CancellationToken cancellationToken)
    {
        var result = new List<SupplierFileResponse>();
        var connection = _context.Database.GetDbConnection();
        var closeAfter = false;

        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                closeAfter = true;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT id, agreementNumber, fileType, validFrom, validTo, supplier_id, status,
       uploadTime, uploaderName, originalName, storedName
FROM files
WHERE supplier_id = @supplierId
ORDER BY uploadTime DESC;";
            AddParameter(command, "@supplierId", supplierId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new SupplierFileResponse
                {
                    Id = ReadInt(reader, "id"),
                    AgreementNumber = ReadString(reader, "agreementNumber"),
                    FileType = ReadString(reader, "fileType"),
                    ValidFrom = ReadString(reader, "validFrom"),
                    ValidTo = ReadString(reader, "validTo"),
                    SupplierId = ReadInt(reader, "supplier_id"),
                    Status = ReadString(reader, "status"),
                    UploadTime = ReadString(reader, "uploadTime"),
                    UploaderName = ReadString(reader, "uploaderName"),
                    OriginalName = ReadString(reader, "originalName"),
                    StoredName = ReadString(reader, "storedName")
                });
            }
        }
        catch
        {
            return new List<SupplierFileResponse>();
        }
        finally
        {
            if (closeAfter)
            {
                await connection.CloseAsync();
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<string>> GetWhitelistedDocumentTypesAsync(int supplierId, CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var entries = await _context.SupplierDocumentWhitelists
                .AsNoTracking()
                .Where(entry => entry.SupplierId == supplierId && entry.IsActive)
                .ToListAsync(cancellationToken);

            return entries
                .Where(entry =>
                    string.IsNullOrWhiteSpace(entry.ExpiresAt) ||
                    !TryParseDate(entry.ExpiresAt, out var expiresAt) ||
                    expiresAt > now)
                .Select(entry => entry.DocumentType)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static SupplierComplianceSummaryResponse BuildComplianceSummary(
        Supplier supplier,
        IReadOnlyList<SupplierDocumentResponse> documents,
        IReadOnlyCollection<string> whitelist)
    {
        var profileChecks = RequiredProfileFields
            .Select(field =>
            {
                var value = field.ValueSelector(supplier);
                return new SupplierComplianceField
                {
                    Key = field.Key,
                    Label = field.Label,
                    Value = value,
                    Complete = !IsValueMissing(value)
                };
            })
            .ToList();

        var missingProfileFields = profileChecks
            .Where(item => !item.Complete)
            .Select(item => new SupplierMissingField { Key = item.Key, Label = item.Label })
            .ToList();

        var documentTypeSet = new HashSet<string>(
            documents.Select(doc => NormalizeDocKey(doc.DocType)).Where(key => key.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var whitelistSet = new HashSet<string>(
            whitelist.Select(NormalizeDocKey).Where(key => key.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var documentChecks = RequiredDocumentTypes
            .Select(requirement =>
            {
                var keys = new List<string> { requirement.Type };
                if (requirement.Aliases.Count > 0)
                {
                    keys.AddRange(requirement.Aliases);
                }

                var normalizedKeys = keys
                    .Select(NormalizeDocKey)
                    .Where(key => key.Length > 0)
                    .ToList();

                var uploaded = normalizedKeys.Any(key => documentTypeSet.Contains(key));
                var exempted = normalizedKeys.Any(key => whitelistSet.Contains(key));

                return new SupplierDocumentRequirementStatus
                {
                    Type = requirement.Type,
                    Label = requirement.Label,
                    Uploaded = uploaded,
                    Exempted = exempted
                };
            })
            .ToList();

        var missingDocumentTypes = documentChecks
            .Where(item => !item.Uploaded && !item.Exempted)
            .Select(item => new SupplierMissingDocumentType { Type = item.Type, Label = item.Label })
            .ToList();

        var profileScore = Percent(profileChecks.Count - missingProfileFields.Count, profileChecks.Count);
        var completedDocuments = documentChecks.Count(item => item.Uploaded || item.Exempted);
        var documentScore = Percent(completedDocuments, documentChecks.Count);
        var totalRequirements = profileChecks.Count + documentChecks.Count;
        var satisfiedRequirements = profileChecks.Count - missingProfileFields.Count + completedDocuments;
        var overallScore = Percent(satisfiedRequirements, totalRequirements);
        var completionCategory = GetCompletionCategory(overallScore);

        var missingItems = missingProfileFields
            .Select(item => new SupplierMissingRequirement
            {
                Type = "profile",
                Key = item.Key,
                Label = item.Label
            })
            .Concat(missingDocumentTypes.Select(item => new SupplierMissingRequirement
            {
                Type = "document",
                Key = item.Type,
                Label = item.Label
            }))
            .ToList();

        var exemptedDocumentTypes = documentChecks
            .Where(item => item.Exempted && !item.Uploaded)
            .Select(item => new SupplierMissingDocumentType
            {
                Type = item.Type,
                Label = item.Label
            })
            .ToList();

        return new SupplierComplianceSummaryResponse
        {
            RequiredProfileFields = profileChecks,
            MissingProfileFields = missingProfileFields,
            RequiredDocumentTypes = documentChecks,
            MissingDocumentTypes = missingDocumentTypes,
            ExemptedDocumentTypes = exemptedDocumentTypes,
            IsProfileComplete = profileScore == 100,
            IsDocumentComplete = documentScore == 100,
            IsComplete = overallScore == 100,
            ProfileScore = profileScore,
            DocumentScore = documentScore,
            OverallScore = overallScore,
            CompletionCategory = completionCategory,
            MissingItems = missingItems
        };
    }

    private static int Percent(int complete, int total)
    {
        if (total <= 0)
        {
            return 100;
        }

        if (complete <= 0)
        {
            return 0;
        }

        return (int)Math.Round((double)complete / total * 100);
    }

    private static string GetCompletionCategory(int overallScore)
    {
        if (overallScore >= 100)
        {
            return "complete";
        }

        if (overallScore >= 80)
        {
            return "mostly_complete";
        }

        return "needs_attention";
    }

    private static bool IsValueMissing(object? value)
    {
        if (value == null)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            try
            {
                return !enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        return false;
    }

    private static string NormalizeDocKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        var normalized = value.Trim().ToLowerInvariant();

        foreach (var ch in normalized)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                builder.Append(ch);
            }
            else if (builder.Length == 0 || builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString().Trim('_');
    }

    private static string? NormalizeDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToString("o")
            : value;
    }

    private static string? NormalizeComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool TryParseDate(string? value, out DateTimeOffset date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = default;
            return false;
        }

        return DateTimeOffset.TryParse(value, out date);
    }

    private static int ReadInt(System.Data.Common.DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            decimal decimalValue => (int)decimalValue,
            double doubleValue => (int)doubleValue,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }

    private static string? ReadString(System.Data.Common.DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset offsetValue => offsetValue.ToString("o"),
            DateTime dateValue => new DateTimeOffset(dateValue).ToString("o"),
            _ => value.ToString()
        };
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed class ProfileRequirementDefinition
    {
        public ProfileRequirementDefinition(string key, string label, Func<Supplier, object?> valueSelector)
        {
            Key = key;
            Label = label;
            ValueSelector = valueSelector;
        }

        public string Key { get; }
        public string Label { get; }
        public Func<Supplier, object?> ValueSelector { get; }
    }

    private sealed class DocumentRequirementDefinition
    {
        public DocumentRequirementDefinition(string type, string label, IReadOnlyList<string> aliases)
        {
            Type = type;
            Label = label;
            Aliases = aliases;
        }

        public string Type { get; }
        public string Label { get; }
        public IReadOnlyList<string> Aliases { get; }
    }

    private async Task<List<TagResponse>> GetSupplierTagsAsync(int supplierId, CancellationToken cancellationToken)
    {
        var tags = await _context.SupplierTags
            .AsNoTracking()
            .Where(st => st.SupplierId == supplierId)
            .Join(_context.TagDefs.AsNoTracking(),
                  st => st.TagId,
                  t => t.Id,
                  (st, t) => t)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tags.Select(t => new TagResponse
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            Color = t.Color
        }).ToList();
    }

    private async Task<Dictionary<int, List<TagResponse>>> GetSupplierTagsLookupAsync(
        IReadOnlyCollection<int> supplierIds,
        CancellationToken cancellationToken)
    {
        var lookup = new Dictionary<int, List<TagResponse>>();
        if (supplierIds.Count == 0)
        {
            return lookup;
        }

        var tags = await _context.SupplierTags
            .AsNoTracking()
            .Where(st => supplierIds.Contains(st.SupplierId))
            .Join(_context.TagDefs.AsNoTracking(),
                st => st.TagId,
                t => t.Id,
                (st, t) => new { st.SupplierId, Tag = t })
            .OrderBy(entry => entry.Tag.Name)
            .ToListAsync(cancellationToken);

        foreach (var entry in tags)
        {
            if (!lookup.TryGetValue(entry.SupplierId, out var list))
            {
                list = new List<TagResponse>();
                lookup[entry.SupplierId] = list;
            }

            list.Add(new TagResponse
            {
                Id = entry.Tag.Id,
                Name = entry.Tag.Name,
                Description = entry.Tag.Description,
                Color = entry.Tag.Color
            });
        }

        return lookup;
    }

    private async Task<Dictionary<int, SupplierCompletionHistory>> GetLatestCompletionHistoryLookupAsync(
        IReadOnlyCollection<int> supplierIds,
        CancellationToken cancellationToken)
    {
        var lookup = new Dictionary<int, SupplierCompletionHistory>();
        if (supplierIds.Count == 0)
        {
            return lookup;
        }

        var histories = await _context.SupplierCompletionHistories
            .AsNoTracking()
            .Where(history => supplierIds.Contains(history.SupplierId))
            .OrderByDescending(history => history.RecordedAt)
            .ToListAsync(cancellationToken);

        foreach (var history in histories)
        {
            if (!lookup.ContainsKey(history.SupplierId))
            {
                lookup[history.SupplierId] = history;
            }
        }

        return lookup;
    }

    private static SupplierComplianceSummaryResponse BuildComplianceSummaryFromHistory(
        Supplier supplier,
        SupplierCompletionHistory? history)
    {
        var missingProfileKeys = ParseMissingKeys(history?.MissingProfileFields);
        var missingDocumentKeys = ParseMissingKeys(history?.MissingDocumentTypes);

        var profileLabels = RequiredProfileFields.ToDictionary(
            field => field.Key,
            field => field.Label,
            StringComparer.OrdinalIgnoreCase);
        var documentLabels = RequiredDocumentTypes.ToDictionary(
            requirement => requirement.Type,
            requirement => requirement.Label,
            StringComparer.OrdinalIgnoreCase);

        var missingProfileFields = missingProfileKeys
            .Select(key => new SupplierMissingField
            {
                Key = key,
                Label = profileLabels.TryGetValue(key, out var label) ? label : key
            })
            .ToList();

        var missingDocumentTypes = missingDocumentKeys
            .Select(key => new SupplierMissingDocumentType
            {
                Type = key,
                Label = documentLabels.TryGetValue(key, out var label) ? label : key
            })
            .ToList();

        var missingItems = missingProfileFields
            .Select(field => new SupplierMissingRequirement
            {
                Type = "profile",
                Key = field.Key,
                Label = field.Label
            })
            .Concat(missingDocumentTypes.Select(doc => new SupplierMissingRequirement
            {
                Type = "document",
                Key = doc.Type,
                Label = doc.Label
            }))
            .ToList();

        var profileScore = NormalizeScore(history?.ProfileCompletion ?? supplier.ProfileCompletion);
        var documentScore = NormalizeScore(history?.DocumentCompletion ?? supplier.DocumentCompletion);
        var overallScore = NormalizeScore(history?.CompletionScore ?? supplier.CompletionScore);

        var completionCategory = history?.CompletionStatus ?? supplier.CompletionStatus;
        if (string.IsNullOrWhiteSpace(completionCategory))
        {
            completionCategory = GetCompletionCategory(overallScore);
        }

        return new SupplierComplianceSummaryResponse
        {
            MissingProfileFields = missingProfileFields,
            MissingDocumentTypes = missingDocumentTypes,
            MissingItems = missingItems,
            ProfileScore = profileScore,
            DocumentScore = documentScore,
            OverallScore = overallScore,
            CompletionCategory = completionCategory,
            IsProfileComplete = profileScore >= 100,
            IsDocumentComplete = documentScore >= 100,
            IsComplete = overallScore >= 100
        };
    }

    private static List<string> ParseMissingKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json);
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList()
                ?? new List<string>();
        }
        catch
        {
            return new List<string>();
    }
    }

    private static int NormalizeScore(decimal? value)
    {
        if (!value.HasValue)
        {
            return 0;
        }

        var rounded = (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, 0, 100);
    }

    private async Task<List<TagResponse>> SetSupplierTagsAsync(int supplierId, List<string> tagNames, CancellationToken cancellationToken)
    {
        // 获取或创建标签
        var tagIds = new List<int>();
        foreach (var tagName in tagNames)
        {
            var normalizedName = tagName.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedName))
                continue;

            var tag = await _context.TagDefs
                .FirstOrDefaultAsync(t => t.Name == normalizedName, cancellationToken);

            if (tag == null)
            {
                tag = new TagDef
                {
                    Name = normalizedName,
                    Description = "User defined tag",
                    Color = null,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                _context.TagDefs.Add(tag);
                await _context.SaveChangesAsync(cancellationToken);
            }

            tagIds.Add(tag.Id);
        }

        // 删除现有关联
        var existingRelations = await _context.SupplierTags
            .Where(st => st.SupplierId == supplierId)
            .ToListAsync(cancellationToken);
        _context.SupplierTags.RemoveRange(existingRelations);

        // 添加新关联
        foreach (var tagId in tagIds)
        {
            // 避免重复添加
            if (!existingRelations.Any(er => er.TagId == tagId))
            {
                _context.SupplierTags.Add(new SupplierTag
                {
                    SupplierId = supplierId,
                    TagId = tagId
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetSupplierTagsAsync(supplierId, cancellationToken);
    }

    #endregion
}
