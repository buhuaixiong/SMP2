using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.ChangeRequests;

public sealed partial class ChangeRequestService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly CompatibilityMigrationService _migrationService;
    private readonly ChangeRequestRepository _repository;

    public ChangeRequestService(
        SupplierSystemDbContext dbContext,
        CompatibilityMigrationService migrationService,
        ChangeRequestRepository repository)
    {
        _dbContext = dbContext;
        _migrationService = migrationService;
        _repository = repository;
    }

    public async Task<Dictionary<string, object?>> CreateChangeRequestAsync(
        AuthUser user,
        int supplierId,
        IDictionary<string, object?> changes,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var supplierExists = await _dbContext.Suppliers.AsNoTracking()
            .AnyAsync(supplier => supplier.Id == supplierId, cancellationToken);
        if (!supplierExists)
        {
            throw new ChangeRequestServiceException(400, "Supplier not found");
        }

        var sanitizedChanges = SanitizeChangesPayload(changes);
        if (sanitizedChanges.Count == 0)
        {
            throw new ChangeRequestServiceException(400, "No changes provided");
        }

        var (required, optional) = BuildChangedFieldSummary(sanitizedChanges);
        if (required.Count == 0 && optional.Count == 0)
        {
            throw new ChangeRequestServiceException(400, "No changes provided");
        }

        if (required.Count > 0)
        {
            var riskLevel = CalculateRiskLevel(required);
            var requestId = await _repository.CreateChangeRequestAsync(new ChangeRequestRecord
            {
                SupplierId = supplierId,
                ChangeType = ChangeTypeRequired,
                Payload = JsonSerializer.Serialize(sanitizedChanges),
                SubmittedBy = string.IsNullOrWhiteSpace(user.Id) ? user.Name : user.Id,
                Status = "pending_purchaser",
                CurrentStep = "purchaser",
                RiskLevel = riskLevel,
                RequiresQuality = string.Equals(riskLevel, "high", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                SubmittedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }, cancellationToken);

            var changedFields = new List<Dictionary<string, object?>>();
            changedFields.AddRange(ToFieldPayloads(required, "required"));
            changedFields.AddRange(ToFieldPayloads(optional, "optional"));

            return new Dictionary<string, object?>
            {
                ["isChangeRequest"] = true,
                ["type"] = ChangeTypeRequired,
                ["flow"] = "required",
                ["requestId"] = requestId,
                ["status"] = "pending_purchaser",
                ["currentStep"] = "purchaser",
                ["riskLevel"] = riskLevel,
                ["changedFields"] = changedFields,
                ["message"] = "检测到必填资料修改，已进入完整审批流程。",
            };
        }

        var optionalRequestId = await _repository.CreateChangeRequestAsync(new ChangeRequestRecord
        {
            SupplierId = supplierId,
            ChangeType = ChangeTypeOptional,
            Payload = JsonSerializer.Serialize(sanitizedChanges),
            SubmittedBy = string.IsNullOrWhiteSpace(user.Id) ? user.Name : user.Id,
            Status = "pending_purchaser",
            CurrentStep = "purchaser",
            RiskLevel = "low",
            RequiresQuality = 0,
            SubmittedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);

        return new Dictionary<string, object?>
        {
            ["isChangeRequest"] = true,
            ["type"] = ChangeTypeOptional,
            ["flow"] = "optional",
            ["requestId"] = optionalRequestId,
            ["status"] = "pending_purchaser",
            ["currentStep"] = "purchaser",
            ["riskLevel"] = "low",
            ["changedFields"] = ToFieldPayloads(optional, "optional"),
            ["message"] = "资料已提交，等待采购员确认。",
        };
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetPendingApprovalsAsync(
        AuthUser user,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var applicableSteps = CollectUserApprovalSteps(user);
        if (applicableSteps.Count == 0)
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        var allPending = new List<Dictionary<string, object?>>();
        foreach (var step in applicableSteps)
        {
            var requests = await _repository.GetPendingChangeRequestsAsync(step, limit, offset, cancellationToken);
            foreach (var row in requests)
            {
                var snapshot = MapSnapshot(row);
                var workflow = GetWorkflowForChangeType(snapshot.ChangeType);
                var payload = ToPayload(snapshot);
                payload["flow"] = GetFlowForChangeType(snapshot.ChangeType);
                payload["workflow"] = ToWorkflowPayload(workflow);
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

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetApprovedApprovalsAsync(
        AuthUser user,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        var rows = await _repository.GetApprovedChangeRequestsByApproverAsync(
            user.Id,
            limit,
            offset,
            cancellationToken);

        var results = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var snapshot = MapSnapshot(row);
            var payload = ToPayload(snapshot);
            payload["flow"] = GetFlowForChangeType(snapshot.ChangeType);
            results.Add(payload);
        }

        return results;
    }

    public async Task<Dictionary<string, object?>> GetChangeRequestDetailsAsync(
        AuthUser user,
        int requestId,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var row = await _repository.GetChangeRequestByIdAsync(requestId, cancellationToken);
        if (row == null)
        {
            throw new ChangeRequestServiceException(404, "Change request not found");
        }

        var snapshot = MapSnapshot(row);
        if (!CanAccessRequest(user, snapshot.SupplierId))
        {
            throw new ChangeRequestServiceException(403, "Access denied");
        }

        var workflow = GetWorkflowForChangeType(snapshot.ChangeType);
        var approvalHistory = await _repository.GetApprovalHistoryAsync(requestId, cancellationToken);
        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == snapshot.SupplierId, cancellationToken);

        var sanitizedPayload = SanitizeChangesPayload(snapshot.Payload);
        var (required, optional) = BuildChangedFieldSummary(sanitizedPayload);

        var payload = ToPayload(snapshot);
        payload["approvalHistory"] = approvalHistory.Select(MapApproval).ToList();
        payload["supplier"] = supplier == null ? null : MapSupplier(supplier);
        payload["workflow"] = ToWorkflowPayload(workflow);
        payload["flow"] = GetFlowForChangeType(snapshot.ChangeType);
        payload["changedFields"] = ToFieldPayloads(required, "required")
            .Concat(ToFieldPayloads(optional, "optional"))
            .ToList();

        return payload;
    }

    public async Task<Dictionary<string, object?>> ApproveChangeRequestAsync(
        AuthUser user,
        int requestId,
        string decision,
        string comments,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var row = await _repository.GetChangeRequestByIdAsync(requestId, cancellationToken);
        if (row == null)
        {
            throw new ChangeRequestServiceException(404, "Change request not found");
        }

        var snapshot = MapSnapshot(row);
        if (!snapshot.Status.StartsWith("pending_", StringComparison.OrdinalIgnoreCase))
        {
            throw new ChangeRequestServiceException(400, "Change request is not pending approval");
        }

        var workflow = GetWorkflowForChangeType(snapshot.ChangeType);
        var currentStep = workflow.FirstOrDefault(step =>
            string.Equals(step.Step, snapshot.CurrentStep, StringComparison.OrdinalIgnoreCase));
        if (currentStep == null)
        {
            throw new ChangeRequestServiceException(400, "Invalid workflow step");
        }

        if (!HasApprovalPermission(user, currentStep))
        {
            throw new ChangeRequestServiceException(403, $"You do not have permission to approve at step: {currentStep.Label}");
        }

        await _repository.CreateApprovalRecordAsync(new ChangeRequestApprovalRecord
        {
            RequestId = requestId,
            Step = snapshot.CurrentStep,
            ApproverId = user.Id,
            ApproverName = user.Name,
            Decision = decision,
            Comments = string.IsNullOrWhiteSpace(comments) ? null : Sanitize(comments),
            CreatedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);

        if (string.Equals(decision, "rejected", StringComparison.Ordinal))
        {
            await _repository.UpdateChangeRequestStatusAsync(
                requestId,
                "rejected",
                snapshot.CurrentStep,
                DateTimeOffset.UtcNow,
                cancellationToken);

            return new Dictionary<string, object?>
            {
                ["status"] = "rejected",
                ["message"] = "资料变更申请已被拒绝。",
            };
        }

        var nextStep = GetNextStep(workflow, snapshot.CurrentStep);
        if (nextStep == null)
        {
            await ApplyApprovedChangesAsync(snapshot, cancellationToken);
            await _repository.UpdateChangeRequestStatusAsync(
                requestId,
                "approved",
                snapshot.CurrentStep,
                DateTimeOffset.UtcNow,
                cancellationToken);

            return new Dictionary<string, object?>
            {
                ["status"] = "approved",
                ["message"] = "资料变更申请已完成并同步到供应商档案。",
            };
        }

        await _repository.UpdateChangeRequestStatusAsync(
            requestId,
            $"pending_{nextStep.Step}",
            nextStep.Step,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return new Dictionary<string, object?>
        {
            ["status"] = $"pending_{nextStep.Step}",
            ["currentStep"] = nextStep.Step,
            ["message"] = $"审批通过，等待{nextStep.Label}",
        };
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSupplierChangeRequestsAsync(
        AuthUser user,
        int supplierId,
        string? status,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        if (!CanAccessRequest(user, supplierId))
        {
            throw new ChangeRequestServiceException(403, "Access denied");
        }

        var requests = await _repository.GetChangeRequestsBySupplierIdAsync(
            supplierId,
            status,
            limit,
            offset,
            cancellationToken);

        var results = new List<Dictionary<string, object?>>();
        foreach (var row in requests)
        {
            var snapshot = MapSnapshot(row);
            var payload = ToPayload(snapshot);
            payload["flow"] = GetFlowForChangeType(snapshot.ChangeType);
            results.Add(payload);
        }

        return results;
    }
}
