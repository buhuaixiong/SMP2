using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Controllers;
using SupplierSystem.Application.DTOs.Registrations;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.Registrations;

public sealed record RegistrationApprovalResult(string Message, string? NextStatus, string? ActivationToken);

public sealed record RegistrationBindResult(string SupplierCode, int SupplierId, IReadOnlyList<RegistrationLoginMethod> LoginMethods);

public sealed record RegistrationLoginMethod(string Type, string Value);

public sealed partial class SupplierRegistrationService
{
    private const int SupplierCodeLength = 7;

    private static readonly IReadOnlyList<RegistrationWorkflowStep> ApprovalWorkflow =
        new List<RegistrationWorkflowStep>
        {
            new()
            {
                Status = RegistrationConstants.RegistrationStatusPendingPurchaser,
                Permission = Permissions.PurchaserRegistrationApprove,
                Role = "purchaser"
            },
            new()
            {
                Status = RegistrationConstants.RegistrationStatusPendingQualityManager,
                Permission = Permissions.QualityManagerRegistrationApprove,
                Role = "quality_manager"
            },
            new()
            {
                Status = RegistrationConstants.RegistrationStatusPendingProcurementManager,
                Permission = Permissions.ProcurementManagerRegistrationApprove,
                Role = "procurement_manager"
            },
            new()
            {
                Status = RegistrationConstants.RegistrationStatusPendingProcurementDirector,
                Permission = Permissions.ProcurementDirectorRegistrationApprove,
                Role = "procurement_director"
            },
            new()
            {
                Status = RegistrationConstants.RegistrationStatusPendingFinanceDirector,
                Permission = Permissions.FinanceDirectorRegistrationApprove,
                Role = "finance_director"
            },
            new()
            {
                Status = RegistrationConstants.RegistrationStatusPendingAccountant,
                Permission = Permissions.FinanceAccountantRegistrationApprove,
                Role = "finance_accountant"
            },
            new()
            {
                Status = RegistrationConstants.RegistrationStatusPendingCashier,
                Permission = Permissions.FinanceCashierRegistrationApprove,
                Role = "finance_cashier"
            },
        };

    private static readonly IReadOnlyDictionary<string, string> NextStatusByStep =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RegistrationConstants.RegistrationStatusPendingPurchaser] = RegistrationConstants.RegistrationStatusPendingQualityManager,
            [RegistrationConstants.RegistrationStatusPendingQualityManager] = RegistrationConstants.RegistrationStatusPendingProcurementManager,
            [RegistrationConstants.RegistrationStatusPendingProcurementManager] = RegistrationConstants.RegistrationStatusPendingProcurementDirector,
            [RegistrationConstants.RegistrationStatusPendingProcurementDirector] = RegistrationConstants.RegistrationStatusPendingFinanceDirector,
            [RegistrationConstants.RegistrationStatusPendingFinanceDirector] = RegistrationConstants.RegistrationStatusPendingAccountant,
            [RegistrationConstants.RegistrationStatusPendingAccountant] = RegistrationConstants.RegistrationStatusPendingCodeBinding,
            [RegistrationConstants.RegistrationStatusPendingCashier] = RegistrationConstants.RegistrationStatusActivated,
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> SupplierCodePrefixes =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DM"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["RMB"] = new[] { "810" },
                ["USD"] = new[] { "610", "613" },
                ["EUR"] = new[] { "610", "613", "247" },
                ["GBP"] = new[] { "610", "613" },
                ["KRW"] = new[] { "610", "613", "617" },
                ["THB"] = new[] { "610", "613", "618" },
                ["JPY"] = new[] { "610", "613", "619" },
            },
            ["IDM"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["RMB"] = new[] { "813" },
                ["USD"] = new[] { "610", "613" },
                ["EUR"] = new[] { "610", "613", "247" },
                ["GBP"] = new[] { "610", "613" },
                ["THB"] = new[] { "610", "613", "614" },
                ["JPY"] = new[] { "610", "613", "615" },
            },
        };

    public async Task<List<SupplierRegistrationApplication>> GetPendingAsync(
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var context = BuildUserContext(user);
        var normalizedRole = ControllerHelpers.NormalizeRole(context.Role);
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return new List<SupplierRegistrationApplication>();
        }

        var step = ApprovalWorkflow.FirstOrDefault(s =>
            string.Equals(ControllerHelpers.NormalizeRole(s.Role), normalizedRole, StringComparison.OrdinalIgnoreCase));

        if (step == null)
        {
            return new List<SupplierRegistrationApplication>();
        }

        if (!HasPermission(context, step.Permission))
        {
            throw new HttpResponseException(403, new { error = "No permission to view registration approvals" });
        }

        if (string.Equals(step.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
        {
            var email = user.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                return new List<SupplierRegistrationApplication>();
            }

            await MigrateLegacyPurchaserAssignmentsAsync(email, cancellationToken);
            return await _dbContext.SupplierRegistrationApplications.AsNoTracking()
                .Where(app => app.Status == step.Status &&
                              app.ProcurementEmail != null &&
                              app.ProcurementEmail.ToLower() == email)
                .OrderBy(app => app.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        // 璐㈠姟浼氳鍙互鐪嬪埌寰呭鎵瑰拰寰呯粦瀹欳ODE鐨勭敵璇?
        if (string.Equals(step.Role, "finance_accountant", StringComparison.OrdinalIgnoreCase))
        {
            return await _dbContext.SupplierRegistrationApplications.AsNoTracking()
                .Where(app => app.Status == step.Status ||
                              app.Status == RegistrationConstants.RegistrationStatusPendingCodeBinding)
                .OrderBy(app => app.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        // 璐㈠姟鍑虹撼鍙互鐪嬪埌寰呭鎵圭殑鐢宠
        if (string.Equals(step.Role, "finance_cashier", StringComparison.OrdinalIgnoreCase))
        {
            return await _dbContext.SupplierRegistrationApplications.AsNoTracking()
                .Where(app => app.Status == step.Status)
                .OrderBy(app => app.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        return await _dbContext.SupplierRegistrationApplications.AsNoTracking()
            .Where(app => app.Status == step.Status)
            .OrderBy(app => app.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var context = BuildUserContext(user);
        var normalizedRole = ControllerHelpers.NormalizeRole(context.Role);
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return 0;
        }

        var step = ApprovalWorkflow.FirstOrDefault(s =>
            string.Equals(ControllerHelpers.NormalizeRole(s.Role), normalizedRole, StringComparison.OrdinalIgnoreCase));

        if (step == null)
        {
            return 0;
        }

        if (!HasPermission(context, step.Permission))
        {
            return 0;
        }

        var query = _dbContext.SupplierRegistrationApplications.AsNoTracking();

        if (string.Equals(step.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
        {
            var email = user.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                return 0;
            }

            await MigrateLegacyPurchaserAssignmentsAsync(email, cancellationToken);
            return await query
                .Where(app => app.Status == step.Status &&
                              app.ProcurementEmail != null &&
                              app.ProcurementEmail.ToLower() == email)
                .CountAsync(cancellationToken);
        }

        if (string.Equals(step.Role, "finance_accountant", StringComparison.OrdinalIgnoreCase))
        {
            return await query
                .Where(app => app.Status == step.Status ||
                              app.Status == RegistrationConstants.RegistrationStatusPendingCodeBinding)
                .CountAsync(cancellationToken);
        }

        if (string.Equals(step.Role, "finance_cashier", StringComparison.OrdinalIgnoreCase))
        {
            return await query
                .Where(app => app.Status == step.Status)
                .CountAsync(cancellationToken);
        }

        return await query
            .Where(app => app.Status == step.Status)
            .CountAsync(cancellationToken);
    }

    public async Task<List<SupplierRegistrationApprovalListItem>> GetApprovedAsync(
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var context = BuildUserContext(user);
        var normalizedRole = ControllerHelpers.NormalizeRole(context.Role);
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return new List<SupplierRegistrationApprovalListItem>();
        }

        var step = ApprovalWorkflow.FirstOrDefault(s =>
            string.Equals(ControllerHelpers.NormalizeRole(s.Role), normalizedRole, StringComparison.OrdinalIgnoreCase));

        if (step == null)
        {
            return new List<SupplierRegistrationApprovalListItem>();
        }

        if (!HasPermission(context, step.Permission))
        {
            throw new HttpResponseException(403, new { error = "No permission to view registration approvals" });
        }

        var approverId = user.Id?.Trim();
        if (string.IsNullOrWhiteSpace(approverId))
        {
            return new List<SupplierRegistrationApprovalListItem>();
        }

        approverId = approverId.ToLowerInvariant();

        IQueryable<SupplierRegistrationApplication> query = _dbContext.SupplierRegistrationApplications.AsNoTracking();

        switch (step.Status)
        {
            case RegistrationConstants.RegistrationStatusPendingPurchaser:
                return await query
                    .Where(app =>
                        app.PurchaserId != null &&
                        app.PurchaserId.ToLower() == approverId &&
                        (app.PurchaserApprovalStatus == "approved" || app.PurchaserApprovalStatus == "rejected"))
                    .OrderByDescending(app => app.PurchaserApprovalTime)
                    .Select(app => new SupplierRegistrationApprovalListItem
                    {
                        Id = app.Id,
                        CompanyName = app.CompanyName,
                        SupplierCode = app.SupplierCode,
                        Status = app.PurchaserApprovalStatus ?? string.Empty,
                        CreatedAt = app.CreatedAt,
                        UpdatedAt = app.UpdatedAt,
                        ContactEmail = app.ContactEmail,
                        ProcurementEmail = app.ProcurementEmail,
                    })
                    .ToListAsync(cancellationToken);
            case RegistrationConstants.RegistrationStatusPendingQualityManager:
                return await query
                    .Where(app =>
                        app.QualityManagerId != null &&
                        app.QualityManagerId.ToLower() == approverId &&
                        (app.QualityApprovalStatus == "approved" || app.QualityApprovalStatus == "rejected"))
                    .OrderByDescending(app => app.QualityApprovalTime)
                    .Select(app => new SupplierRegistrationApprovalListItem
                    {
                        Id = app.Id,
                        CompanyName = app.CompanyName,
                        SupplierCode = app.SupplierCode,
                        Status = app.QualityApprovalStatus ?? string.Empty,
                        CreatedAt = app.CreatedAt,
                        UpdatedAt = app.UpdatedAt,
                        ContactEmail = app.ContactEmail,
                        ProcurementEmail = app.ProcurementEmail,
                    })
                    .ToListAsync(cancellationToken);
            case RegistrationConstants.RegistrationStatusPendingProcurementManager:
                return await query
                    .Where(app =>
                        app.ProcurementManagerId != null &&
                        app.ProcurementManagerId.ToLower() == approverId &&
                        (app.ProcurementManagerApprovalStatus == "approved" || app.ProcurementManagerApprovalStatus == "rejected"))
                    .OrderByDescending(app => app.ProcurementManagerApprovalTime)
                    .Select(app => new SupplierRegistrationApprovalListItem
                    {
                        Id = app.Id,
                        CompanyName = app.CompanyName,
                        SupplierCode = app.SupplierCode,
                        Status = app.ProcurementManagerApprovalStatus ?? string.Empty,
                        CreatedAt = app.CreatedAt,
                        UpdatedAt = app.UpdatedAt,
                        ContactEmail = app.ContactEmail,
                        ProcurementEmail = app.ProcurementEmail,
                    })
                    .ToListAsync(cancellationToken);
            case RegistrationConstants.RegistrationStatusPendingProcurementDirector:
                return await query
                    .Where(app =>
                        app.ProcurementDirectorId != null &&
                        app.ProcurementDirectorId.ToLower() == approverId &&
                        (app.ProcurementDirectorApprovalStatus == "approved" || app.ProcurementDirectorApprovalStatus == "rejected"))
                    .OrderByDescending(app => app.ProcurementDirectorApprovalTime)
                    .Select(app => new SupplierRegistrationApprovalListItem
                    {
                        Id = app.Id,
                        CompanyName = app.CompanyName,
                        SupplierCode = app.SupplierCode,
                        Status = app.ProcurementDirectorApprovalStatus ?? string.Empty,
                        CreatedAt = app.CreatedAt,
                        UpdatedAt = app.UpdatedAt,
                        ContactEmail = app.ContactEmail,
                        ProcurementEmail = app.ProcurementEmail,
                    })
                    .ToListAsync(cancellationToken);
            case RegistrationConstants.RegistrationStatusPendingFinanceDirector:
                return await query
                    .Where(app =>
                        app.FinanceDirectorId != null &&
                        app.FinanceDirectorId.ToLower() == approverId &&
                        (app.FinanceDirectorApprovalStatus == "approved" || app.FinanceDirectorApprovalStatus == "rejected"))
                    .OrderByDescending(app => app.FinanceDirectorApprovalTime)
                    .Select(app => new SupplierRegistrationApprovalListItem
                    {
                        Id = app.Id,
                        CompanyName = app.CompanyName,
                        SupplierCode = app.SupplierCode,
                        Status = app.FinanceDirectorApprovalStatus ?? string.Empty,
                        CreatedAt = app.CreatedAt,
                        UpdatedAt = app.UpdatedAt,
                        ContactEmail = app.ContactEmail,
                        ProcurementEmail = app.ProcurementEmail,
                    })
                    .ToListAsync(cancellationToken);
            case RegistrationConstants.RegistrationStatusPendingAccountant:
                return await query
                    .Where(app =>
                        app.FinanceAccountantId != null &&
                        app.FinanceAccountantId.ToLower() == approverId &&
                        (app.FinanceAccountantApprovalStatus == "approved" || app.FinanceAccountantApprovalStatus == "rejected"))
                    .OrderByDescending(app => app.FinanceAccountantApprovalTime)
                    .Select(app => new SupplierRegistrationApprovalListItem
                    {
                        Id = app.Id,
                        CompanyName = app.CompanyName,
                        SupplierCode = app.SupplierCode,
                        Status = app.FinanceAccountantApprovalStatus ?? string.Empty,
                        CreatedAt = app.CreatedAt,
                        UpdatedAt = app.UpdatedAt,
                        ContactEmail = app.ContactEmail,
                        ProcurementEmail = app.ProcurementEmail,
                    })
                    .ToListAsync(cancellationToken);
            case RegistrationConstants.RegistrationStatusPendingCashier:
                return await query
                    .Where(app =>
                        app.FinanceCashierId != null &&
                        app.FinanceCashierId.ToLower() == approverId &&
                        (app.FinanceCashierApprovalStatus == "approved" || app.FinanceCashierApprovalStatus == "rejected"))
                    .OrderByDescending(app => app.FinanceCashierApprovalTime)
                    .Select(app => new SupplierRegistrationApprovalListItem
                    {
                        Id = app.Id,
                        CompanyName = app.CompanyName,
                        SupplierCode = app.SupplierCode,
                        Status = app.FinanceCashierApprovalStatus ?? string.Empty,
                        CreatedAt = app.CreatedAt,
                        UpdatedAt = app.UpdatedAt,
                        ContactEmail = app.ContactEmail,
                        ProcurementEmail = app.ProcurementEmail,
                    })
                    .ToListAsync(cancellationToken);
        }

        return new List<SupplierRegistrationApprovalListItem>();
    }

    public async Task<SupplierRegistrationApplication> GetByIdAsync(
        int applicationId,
        CancellationToken cancellationToken)
    {
        var application = await _dbContext.SupplierRegistrationApplications.AsNoTracking()
            .FirstOrDefaultAsync(app => app.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        return application;
    }

    public async Task<SupplierRegistrationStatusResponse> GetStatusAsync(
        int applicationId,
        CancellationToken cancellationToken)
    {
        var application = await _dbContext.SupplierRegistrationApplications.AsNoTracking()
            .FirstOrDefaultAsync(app => app.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        return await BuildStatusResponseAsync(application, cancellationToken);
    }

    public async Task<SupplierRegistrationStatusResponse> GetStatusBySupplierAsync(
        int supplierId,
        CancellationToken cancellationToken)
    {
        var application = await _dbContext.SupplierRegistrationApplications.AsNoTracking()
            .Where(app => app.SupplierId == supplierId)
            .OrderByDescending(app => app.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        return await BuildStatusResponseAsync(application, cancellationToken);
    }

    public async Task<SupplierRegistrationStatusResponse> GetStatusForCurrentUserAsync(
        AuthUser user,
        CancellationToken cancellationToken)
    {
        if (user.SupplierId.HasValue)
        {
            return await GetStatusBySupplierAsync(user.SupplierId.Value, cancellationToken);
        }

        if (user.RelatedApplicationId.HasValue)
        {
            return await GetStatusAsync(user.RelatedApplicationId.Value, cancellationToken);
        }

        var isTrackingAccount = string.Equals(user.Role, "tracking", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(user.AccountType, "tracking", StringComparison.OrdinalIgnoreCase);
        if (!isTrackingAccount)
        {
            throw new HttpResponseException(400, new { error = "supplierId is required." });
        }

        var trackingId = user.Id?.Trim().ToLowerInvariant();
        var email = user.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trackingId) && string.IsNullOrWhiteSpace(email))
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        IQueryable<SupplierRegistrationApplication> query = _dbContext.SupplierRegistrationApplications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(trackingId) && !string.IsNullOrWhiteSpace(email) &&
            !string.Equals(trackingId, email, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(app =>
                (app.TrackingAccountId != null && app.TrackingAccountId.ToLower() == trackingId) ||
                (app.ContactEmail != null && app.ContactEmail.ToLower() == email));
        }
        else
        {
            var lookup = !string.IsNullOrWhiteSpace(trackingId) ? trackingId : email;
            query = query.Where(app =>
                (app.TrackingAccountId != null && app.TrackingAccountId.ToLower() == lookup) ||
                (app.ContactEmail != null && app.ContactEmail.ToLower() == lookup));
        }

        var application = await query
            .OrderByDescending(app => app.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        return await BuildStatusResponseAsync(application, cancellationToken);
    }

    public async Task<RegistrationApprovalResult> ApproveAsync(
        int applicationId,
        AuthUser user,
        string? comment,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var application = await _dbContext.SupplierRegistrationApplications
            .FirstOrDefaultAsync(app => app.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        if (string.Equals(application.Status, RegistrationConstants.RegistrationStatusActivated, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpResponseException(400, new { error = "Application already activated" });
        }

        if (string.Equals(application.Status, RegistrationConstants.RegistrationStatusRejected, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpResponseException(400, new { error = "Application already rejected" });
        }

        var step = FindStepByStatus(application.Status);
        if (step == null)
        {
            throw new HttpResponseException(400, new { error = "Invalid application status" });
        }

        var context = BuildUserContext(user);
        EnsureApprovalPermission(context, step, allowRoleMismatch: false);

        var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        ApplyApprovalUpdate(application, step, user.Id, nowIso, comment, "approved");

        // 鍑虹撼瀹℃壒閫氳繃鍚庣洿鎺ユ縺娲?
        string? nextStatus;
        if (string.Equals(step.Status, RegistrationConstants.RegistrationStatusPendingCashier, StringComparison.OrdinalIgnoreCase))
        {
            nextStatus = RegistrationConstants.RegistrationStatusActivated;
            application.ActivatedAt = nowIso;
        }
        else
        {
            nextStatus = NextStatusByStep[step.Status];
        }

        application.Status = nextStatus;
        application.UpdatedAt = nowIso;

        _dbContext.SupplierRegistrationApplications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = user.Id,
            ActorName = user.Name,
            EntityType = "supplier_registration",
            EntityId = applicationId.ToString(CultureInfo.InvariantCulture),
            Action = "approve_step",
            Changes = JsonSerializer.Serialize(new
            {
                step = step.Status,
                nextStatus,
                comment,
            }),
            IpAddress = ipAddress,
        });

        await TryNotifyNextApproverAsync(application, step, nextStatus, cancellationToken);

        if (string.Equals(nextStatus, RegistrationConstants.RegistrationStatusActivated, StringComparison.OrdinalIgnoreCase))
        {
            await TryNotifyApplicantActivatedAsync(application, cancellationToken);
        }

        return new RegistrationApprovalResult(
            "Application approved and moved to next step",
            nextStatus,
            null);
    }

    public async Task<RegistrationApprovalResult> RejectAsync(
        int applicationId,
        AuthUser user,
        string reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new HttpResponseException(400, new { error = "Rejection reason is required" });
        }

        var application = await _dbContext.SupplierRegistrationApplications
            .FirstOrDefaultAsync(app => app.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        if (string.Equals(application.Status, RegistrationConstants.RegistrationStatusActivated, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpResponseException(400, new { error = "Cannot reject activated application" });
        }

        if (string.Equals(application.Status, RegistrationConstants.RegistrationStatusRejected, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpResponseException(400, new { error = "Application already rejected" });
        }

        var step = FindStepByStatus(application.Status);
        if (step == null)
        {
            throw new HttpResponseException(400, new { error = "Invalid application status" });
        }

        var context = BuildUserContext(user);
        EnsureApprovalPermission(context, step, allowRoleMismatch: true);

        var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        ApplyApprovalUpdate(application, step, user.Id, nowIso, reason, "rejected");
        application.Status = RegistrationConstants.RegistrationStatusRejected;
        application.RejectedBy = user.Id;
        application.RejectedAt = nowIso;
        application.RejectionReason = reason;
        application.UpdatedAt = nowIso;

        _dbContext.SupplierRegistrationApplications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = user.Id,
            ActorName = user.Name,
            EntityType = "supplier_registration",
            EntityId = applicationId.ToString(CultureInfo.InvariantCulture),
            Action = "reject",
            Changes = JsonSerializer.Serialize(new
            {
                step = step.Status,
                reason,
            }),
            IpAddress = ipAddress,
        });

        await TryNotifyApplicantRejectedAsync(application, step, reason, cancellationToken);

        return new RegistrationApprovalResult("Application rejected", null, null);
    }

    public async Task<RegistrationApprovalResult> RequestInfoAsync(
        int applicationId,
        AuthUser user,
        string message,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new HttpResponseException(400, new { error = "Message is required" });
        }

        var application = await _dbContext.SupplierRegistrationApplications
            .FirstOrDefaultAsync(app => app.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        var step = FindStepByStatus(application.Status);
        if (step == null)
        {
            throw new HttpResponseException(400, new { error = "Invalid application status" });
        }

        var context = BuildUserContext(user);
        EnsureApprovalPermission(context, step, allowRoleMismatch: true);

        var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        ApplyApprovalUpdate(application, step, user.Id, nowIso, message, "pending_info");
        application.UpdatedAt = nowIso;

        _dbContext.SupplierRegistrationApplications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = user.Id,
            ActorName = user.Name,
            EntityType = "supplier_registration",
            EntityId = applicationId.ToString(CultureInfo.InvariantCulture),
            Action = "request_info",
            Changes = JsonSerializer.Serialize(new
            {
                step = step.Status,
                message,
            }),
            IpAddress = ipAddress,
        });

        return new RegistrationApprovalResult("Information request sent to supplier", null, null);
    }

    public async Task<RegistrationBindResult> BindSupplierCodeAsync(
        int applicationId,
        AuthUser user,
        string? requestedCode,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(user.Role, "finance_accountant", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpResponseException(403, new { error = "Only finance accountants can bind supplier codes" });
        }

        var application = await _dbContext.SupplierRegistrationApplications
            .FirstOrDefaultAsync(app => app.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new HttpResponseException(404, new { error = "Registration application not found" });
        }

        if (!string.Equals(application.Status, RegistrationConstants.RegistrationStatusPendingCodeBinding, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpResponseException(400, new
            {
                error = "Current status does not allow code binding",
                currentStatus = application.Status
            });
        }

        var prefixes = ResolveSupplierCodePrefixes(application.SupplierClassification, application.OperatingCurrency);
        if (prefixes.Count == 0)
        {
            throw new HttpResponseException(400, new { error = "Unable to resolve supplier code prefix for classification/currency" });
        }

        var finalCode = await ResolveFinalSupplierCodeAsync(prefixes, requestedCode, cancellationToken);
        var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var supplier = new Supplier
        {
            CompanyName = application.CompanyName,
            CompanyId = application.SupplierCode ?? finalCode,
            ContactPerson = application.ContactName,
            ContactPhone = application.ContactPhone,
            ContactEmail = application.ContactEmail,
            Category = application.BusinessNature ?? application.SupplierClassification,
            Address = application.BusinessAddress,
            Status = "approved",
            Stage = "temporary",
            SupplierCode = finalCode,
            CreatedBy = "registration_system",
            CreatedAt = nowIso,
            UpdatedAt = nowIso,
            Notes = application.Notes,
            BankAccount = application.BankAccountNumber,
            PaymentTerms = application.PaymentTermsDays,
            ServiceCategory = application.ProductTypes,
            Region = application.DeliveryLocation,
            FinancialContact = application.FinanceContactName,
            PaymentCurrency = application.OperatingCurrency,
            FaxNumber = application.BusinessFax,
            BusinessRegistrationNumber = application.BusinessRegistrationNumber,
        };

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var trackingEmail = application.ContactEmail?.Trim().ToLowerInvariant();
        User? trackingUser = null;
        if (!string.IsNullOrWhiteSpace(trackingEmail))
        {
            trackingUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == trackingEmail && u.AccountType == "tracking", cancellationToken);

            if (trackingUser != null)
            {
                trackingUser.Name = application.CompanyName;
                trackingUser.Username = finalCode;
                trackingUser.Role = "temp_supplier";
                trackingUser.AccountType = "formal";
                trackingUser.SupplierId = supplier.Id;
                trackingUser.MustChangePassword = true;
                trackingUser.UpdatedAt = nowIso;
                _dbContext.Users.Update(trackingUser);
            }
        }

        if (trackingUser == null)
        {
            throw new InvalidOperationException("Tracking account not found");
        }

        application.SupplierCode = finalCode;
        application.SupplierId = supplier.Id;
        application.Status = RegistrationConstants.RegistrationStatusPendingCashier;
        application.UpdatedAt = nowIso;

        _dbContext.SupplierRegistrationApplications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = user.Id,
            ActorName = user.Name,
            EntityType = "supplier_registration",
            EntityId = applicationId.ToString(CultureInfo.InvariantCulture),
            Action = "bind_supplier_code",
            Changes = JsonSerializer.Serialize(new
            {
                supplierCode = finalCode,
                supplierId = supplier.Id,
                trackingEmail,
                requestedCode = string.IsNullOrWhiteSpace(requestedCode) ? "auto-generated" : requestedCode,
            }),
            IpAddress = ipAddress,
        });

        await tx.CommitAsync(cancellationToken);

        return new RegistrationBindResult(
            finalCode,
            supplier.Id,
            new[]
            {
                new RegistrationLoginMethod("email", application.ContactEmail ?? string.Empty),
                new RegistrationLoginMethod("supplierCode", finalCode),
            });
    }

    private static SupplierRegistrationUserContext BuildUserContext(AuthUser user)
    {
        return new SupplierRegistrationUserContext
        {
            Role = user.Role,
            Permissions = user.Permissions ?? new List<string>(),
        };
    }

    private static RegistrationWorkflowStep? FindStepByStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return ApprovalWorkflow.FirstOrDefault(step =>
            string.Equals(step.Status, status, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ResolveSupplierCodePrefixes(string? classification, string? currency)
    {
        if (string.IsNullOrWhiteSpace(classification) || string.IsNullOrWhiteSpace(currency))
        {
            return Array.Empty<string>();
        }

        if (!SupplierCodePrefixes.TryGetValue(classification.Trim(), out var map))
        {
            return Array.Empty<string>();
        }

        return map.TryGetValue(currency.Trim(), out var prefixes) ? prefixes : Array.Empty<string>();
    }

    private async Task<string> ResolveFinalSupplierCodeAsync(
        IReadOnlyList<string> prefixes,
        string? requestedCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedCode))
        {
            var trimmedCode = requestedCode.Trim();
            if (!prefixes.Any(prefix => trimmedCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                var allowed = string.Join(" or ", prefixes);
                throw new HttpResponseException(400, new { error = $"Supplier code must start with {allowed}" });
            }

            if (trimmedCode.Length != SupplierCodeLength)
            {
                throw new HttpResponseException(400, new { error = "Supplier code must be 6 characters long" });
            }

            var exists = await _dbContext.Suppliers.AsNoTracking()
                .AnyAsync(s => s.SupplierCode == trimmedCode, cancellationToken);

            if (exists)
            {
                throw new HttpResponseException(409, new { error = "Supplier code already exists" });
            }

            return trimmedCode;
        }

        var defaultPrefix = prefixes.First();
        return await BuildNextSupplierCodeAsync(defaultPrefix, cancellationToken);
    }

    private async Task<string> BuildNextSupplierCodeAsync(string prefix, CancellationToken cancellationToken)
    {
        if (prefix.Length >= SupplierCodeLength)
        {
            throw new InvalidOperationException("Invalid supplier code prefix configuration");
        }

        var prefixPattern = $"{prefix}%";

        var latestCode = await _dbContext.SupplierRegistrationApplications.AsNoTracking()
            .Where(app => app.SupplierCode != null && EF.Functions.Like(app.SupplierCode, prefixPattern))
            .Select(app => app.SupplierCode!)
            .Concat(_dbContext.Suppliers.AsNoTracking()
                .Where(s => EF.Functions.Like(s.CompanyId, prefixPattern))
                .Select(s => s.CompanyId))
            .Concat(_dbContext.Suppliers.AsNoTracking()
                .Where(s => s.SupplierCode != null && EF.Functions.Like(s.SupplierCode, prefixPattern))
                .Select(s => s.SupplierCode!))
            .OrderByDescending(code => code)
            .FirstOrDefaultAsync(cancellationToken);

        var suffixLength = SupplierCodeLength - prefix.Length;
        var nextNumber = 1;
        if (!string.IsNullOrWhiteSpace(latestCode) && latestCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = latestCode.Substring(prefix.Length);
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                nextNumber = parsed + 1;
            }
        }

        if (nextNumber >= Math.Pow(10, suffixLength))
        {
            throw new InvalidOperationException($"Supplier code sequence exhausted for prefix {prefix}");
        }

        return $"{prefix}{nextNumber.ToString().PadLeft(suffixLength, '0')}";
    }
}
