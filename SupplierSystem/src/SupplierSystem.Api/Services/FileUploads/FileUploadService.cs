using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.FileUploads;

public sealed partial class FileUploadService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly CompatibilityMigrationService _migrationService;
    private readonly FileUploadRepository _repository;

    public FileUploadService(
        SupplierSystemDbContext dbContext,
        CompatibilityMigrationService migrationService,
        FileUploadRepository repository)
    {
        _dbContext = dbContext;
        _migrationService = migrationService;
        _repository = repository;
    }

    public async Task<Dictionary<string, object?>> CreateFileUploadAsync(
        AuthUser user,
        int supplierId,
        int fileId,
        string fileName,
        string? fileDescription,
        string? validFrom,
        string? validTo,
        CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new FileUploadServiceException(401, "Authentication required");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var supplierExists = await _dbContext.Suppliers.AsNoTracking()
            .AnyAsync(supplier => supplier.Id == supplierId, cancellationToken);
        if (!supplierExists)
        {
            throw new FileUploadServiceException(400, "Supplier not found");
        }

        if (fileId <= 0)
        {
            throw new FileUploadServiceException(400, "Invalid file id");
        }

        var dates = ValidateValidityPeriod(validFrom, validTo);
        var now = DateTimeOffset.UtcNow;

        var uploadId = await _repository.CreateFileUploadAsync(new FileUploadRecord
        {
            SupplierId = supplierId,
            FileId = fileId,
            FileName = Sanitize(fileName) ?? fileName,
            FileDescription = Sanitize(fileDescription),
            Status = "pending_purchaser",
            CurrentStep = "purchaser",
            SubmittedBy = string.IsNullOrWhiteSpace(user.Id) ? user.Name : user.Id,
            SubmittedAt = now,
            RiskLevel = "low",
            ValidFrom = dates.validFrom,
            ValidTo = dates.validTo,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        return new Dictionary<string, object?>
        {
            ["uploadId"] = uploadId,
            ["status"] = "pending_purchaser",
            ["currentStep"] = "purchaser",
            ["message"] = "File uploaded and workflow started.",
            ["workflow"] = ToWorkflowPayload()
        };
    }

    public async Task<Dictionary<string, object?>> ApproveFileUploadAsync(
        AuthUser user,
        int uploadId,
        string decision,
        string? comments,
        CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new FileUploadServiceException(401, "Authentication required");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var row = await _repository.GetFileUploadByIdAsync(uploadId, cancellationToken);
        if (row == null)
        {
            throw new FileUploadServiceException(404, "File upload not found");
        }

        var snapshot = MapSnapshot(row);
        if (!snapshot.Status.StartsWith("pending_", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileUploadServiceException(400, "File upload is not pending approval");
        }

        var currentStep = ApprovalWorkflow.FirstOrDefault(step =>
            string.Equals(step.Step, snapshot.CurrentStep, StringComparison.OrdinalIgnoreCase));
        if (currentStep == null)
        {
            throw new FileUploadServiceException(400, "Invalid workflow step");
        }

        if (!HasStepPermission(user, currentStep))
        {
            throw new FileUploadServiceException(403, $"You do not have permission to approve at step: {currentStep.Label}");
        }

        await _repository.CreateApprovalRecordAsync(new FileUploadApprovalRecord
        {
            UploadId = uploadId,
            Step = snapshot.CurrentStep,
            StepName = currentStep.Label,
            ApproverId = user.Id,
            ApproverName = user.Name,
            Decision = decision,
            Comments = Sanitize(comments),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        if (string.Equals(decision, "rejected", StringComparison.Ordinal))
        {
            await _repository.UpdateFileUploadStatusAsync(
                uploadId,
                "rejected",
                snapshot.CurrentStep,
                DateTimeOffset.UtcNow,
                cancellationToken);

            return new Dictionary<string, object?>
            {
                ["status"] = "rejected",
                ["message"] = "File upload request was rejected."
            };
        }

        var nextStep = GetNextStep(snapshot.CurrentStep);
        if (nextStep == null)
        {
            await _repository.UpdateFileUploadStatusAsync(
                uploadId,
                "approved",
                snapshot.CurrentStep,
                DateTimeOffset.UtcNow,
                cancellationToken);

            return new Dictionary<string, object?>
            {
                ["status"] = "approved",
                ["message"] = "File upload request fully approved."
            };
        }

        await _repository.UpdateFileUploadStatusAsync(
            uploadId,
            $"pending_{nextStep.Step}",
            nextStep.Step,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return new Dictionary<string, object?>
        {
            ["status"] = $"pending_{nextStep.Step}",
            ["currentStep"] = nextStep.Step,
            ["message"] = $"Approved. Waiting for {nextStep.Label}."
        };
    }

    public async Task<Dictionary<string, object?>> GetFileUploadDetailsAsync(
        AuthUser user,
        int uploadId,
        CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new FileUploadServiceException(401, "Authentication required");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var row = await _repository.GetFileUploadByIdAsync(uploadId, cancellationToken);
        if (row == null)
        {
            throw new FileUploadServiceException(404, "File upload not found");
        }

        var snapshot = MapSnapshot(row);
        if (!CanAccessUpload(user, snapshot.SupplierId))
        {
            throw new FileUploadServiceException(403, "Access denied");
        }

        var approvals = await _repository.GetApprovalHistoryAsync(uploadId, cancellationToken);
        var approvalHistory = approvals.Select(MapApproval).ToList();

        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == snapshot.SupplierId, cancellationToken);

        var payload = ToPayload(snapshot);
        payload["approvalHistory"] = approvalHistory;
        payload["supplier"] = supplier == null ? null : MapSupplier(supplier);
        payload["workflow"] = ToWorkflowPayload();
        return payload;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetPendingFileApprovalsAsync(
        AuthUser user,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new FileUploadServiceException(401, "Authentication required");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var steps = CollectUserApprovalSteps(user);
        if (steps.Count == 0)
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        var allPending = new List<Dictionary<string, object?>>();
        foreach (var step in steps)
        {
            var rows = await _repository.GetPendingFileUploadsAsync(step, limit, offset, cancellationToken);
            foreach (var row in rows)
            {
                var payload = ToPayload(MapSnapshot(row));
                payload["workflow"] = ToWorkflowPayload();
                allPending.Add(payload);
            }
        }

        allPending.Sort((left, right) =>
        {
            var leftDate = GetSortTimestamp(left);
            var rightDate = GetSortTimestamp(right);
            return leftDate.CompareTo(rightDate);
        });

        return allPending.Take(limit).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetApprovedFileApprovalsAsync(
        AuthUser user,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new FileUploadServiceException(401, "Authentication required");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        var rows = await _repository.GetApprovedFileUploadsByApproverAsync(
            user.Id,
            limit,
            offset,
            cancellationToken);

        return rows.Select(row => ToPayload(MapSnapshot(row))).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSupplierFileUploadsAsync(
        AuthUser user,
        int supplierId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (user == null)
        {
            throw new FileUploadServiceException(401, "Authentication required");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        if (!CanAccessUpload(user, supplierId))
        {
            throw new FileUploadServiceException(403, "Access denied");
        }

        var rows = await _repository.GetFileUploadsBySupplierIdAsync(supplierId, limit, offset, cancellationToken);
        return rows.Select(row => ToPayload(MapSnapshot(row))).ToList();
    }

    public async Task<Dictionary<string, object?>> GetExpiringFilesAsync(
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
            throw new FileUploadServiceException(403, "Only purchasers can view expiring files");
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var normalizedDays = daysThreshold < 0 ? 0 : daysThreshold;
        var expiringRows = await _repository.GetExpiringFileUploadsAsync(normalizedDays, cancellationToken);
        var expiredRows = await _repository.GetExpiredFileUploadsAsync(cancellationToken);

        var expiring = expiringRows.Select(MapSnapshot).ToList();
        var expired = expiredRows.Select(MapSnapshot).ToList();

        var supplierIds = expiring.Concat(expired)
            .Select(snapshot => snapshot.SupplierId)
            .Distinct()
            .ToList();

        var suppliers = await _dbContext.Suppliers.AsNoTracking()
            .Where(supplier => supplierIds.Contains(supplier.Id))
            .ToListAsync(cancellationToken);

        var supplierMap = suppliers.ToDictionary(
            supplier => supplier.Id,
            supplier => new Dictionary<string, object?>
            {
                ["id"] = supplier.Id,
                ["companyName"] = supplier.CompanyName,
                ["contactEmail"] = supplier.ContactEmail
            });

        var expiringPayload = expiring.Select(snapshot =>
        {
            var payload = ToPayload(snapshot);
            payload["supplier"] = supplierMap.TryGetValue(snapshot.SupplierId, out var supplier)
                ? supplier
                : null;
            return payload;
        }).ToList();

        var expiredPayload = expired.Select(snapshot =>
        {
            var payload = ToPayload(snapshot);
            payload["supplier"] = supplierMap.TryGetValue(snapshot.SupplierId, out var supplier)
                ? supplier
                : null;
            return payload;
        }).ToList();

        return new Dictionary<string, object?>
        {
            ["expiring"] = expiringPayload,
            ["expired"] = expiredPayload
        };
    }
}
