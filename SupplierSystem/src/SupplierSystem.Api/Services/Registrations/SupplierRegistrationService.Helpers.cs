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
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.Registrations
{
    public sealed partial class SupplierRegistrationService
    {
        private static string? NormalizeRole(string? role)
        {
            return ControllerHelpers.NormalizeRole(role);
        }

        private static bool HasPermission(SupplierRegistrationUserContext context, string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
            {
                return false;
            }

            return context.Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureApprovalPermission(
            SupplierRegistrationUserContext context,
            RegistrationWorkflowStep step,
            bool allowRoleMismatch)
        {
            if (!HasPermission(context, step.Permission))
            {
                throw new SupplierSystem.Application.Exceptions.HttpResponseException(
                    403,
                    new { error = "No permission to approve this step" });
            }

            if (!allowRoleMismatch)
            {
                var role = NormalizeRole(context.Role);
                if (!string.Equals(step.Role, role, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SupplierSystem.Application.Exceptions.HttpResponseException(
                        403,
                        new { error = "Your role cannot approve this step" });
                }
            }
        }

        private static void ApplyApprovalUpdate(
            SupplierRegistrationApplication application,
            RegistrationWorkflowStep step,
            string userId,
            string nowIso,
            string? comment,
            string statusValue)
        {
            switch (step.Status)
            {
                case RegistrationConstants.RegistrationStatusPendingPurchaser:
                    application.PurchaserId = userId;
                    application.PurchaserApprovalTime = nowIso;
                    application.PurchaserApprovalComment = comment ?? string.Empty;
                    application.PurchaserApprovalStatus = statusValue;
                    break;
                case "pending_quality_manager":
                    application.QualityManagerId = userId;
                    application.QualityApprovalTime = nowIso;
                    application.QualityApprovalComment = comment ?? string.Empty;
                    application.QualityApprovalStatus = statusValue;
                    break;
                case "pending_procurement_manager":
                    application.ProcurementManagerId = userId;
                    application.ProcurementManagerApprovalTime = nowIso;
                    application.ProcurementManagerApprovalComment = comment ?? string.Empty;
                    application.ProcurementManagerApprovalStatus = statusValue;
                    break;
                case "pending_procurement_director":
                    application.ProcurementDirectorId = userId;
                    application.ProcurementDirectorApprovalTime = nowIso;
                    application.ProcurementDirectorApprovalComment = comment ?? string.Empty;
                    application.ProcurementDirectorApprovalStatus = statusValue;
                    break;
                case "pending_finance_director":
                    application.FinanceDirectorId = userId;
                    application.FinanceDirectorApprovalTime = nowIso;
                    application.FinanceDirectorApprovalComment = comment ?? string.Empty;
                    application.FinanceDirectorApprovalStatus = statusValue;
                    break;
                case "pending_accountant":
                    application.FinanceAccountantId = userId;
                    application.FinanceAccountantApprovalTime = nowIso;
                    application.FinanceAccountantApprovalComment = comment ?? string.Empty;
                    application.FinanceAccountantApprovalStatus = statusValue;
                    break;
                case RegistrationConstants.RegistrationStatusPendingCashier:
                    application.FinanceCashierId = userId;
                    application.FinanceCashierApprovalTime = nowIso;
                    application.FinanceCashierApprovalComment = comment ?? string.Empty;
                    application.FinanceCashierApprovalStatus = statusValue;
                    break;
            }
        }

        private async Task MigrateLegacyPurchaserAssignmentsAsync(string email, CancellationToken cancellationToken)
        {
            var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            var applications = await _dbContext.SupplierRegistrationApplications
                .Where(app =>
                    app.Status == "pending_quality_manager" &&
                    app.ProcurementEmail != null &&
                    app.ProcurementEmail.ToLower() == email &&
                    app.PurchaserApprovalStatus == null)
                .ToListAsync(cancellationToken);

            if (applications.Count == 0)
            {
                return;
            }

            foreach (var app in applications)
            {
                app.Status = RegistrationConstants.RegistrationStatusPendingPurchaser;
                app.QualityManagerId = null;
                app.QualityApprovalTime = null;
                app.QualityApprovalComment = null;
                app.QualityApprovalStatus = null;
                app.UpdatedAt = nowIso;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<SupplierRegistrationStatusResponse> BuildStatusResponseAsync(
            SupplierRegistrationApplication application,
            CancellationToken cancellationToken)
        {
            Supplier? supplier = null;
            if (application.SupplierId.HasValue)
            {
                supplier = await _dbContext.Suppliers.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == application.SupplierId.Value, cancellationToken);
            }

            var history = await BuildHistoryAsync(application.Id, application.CreatedAt, cancellationToken);
            var tempAccount = await BuildTempAccountInfoAsync(application, supplier, cancellationToken);
            var currentApprover = await ResolveCurrentApproverAsync(application, cancellationToken);

            var updatedAt = application.UpdatedAt;
            if (string.IsNullOrWhiteSpace(updatedAt) && !string.IsNullOrWhiteSpace(supplier?.UpdatedAt))
            {
                updatedAt = supplier?.UpdatedAt;
            }

            return new SupplierRegistrationStatusResponse
            {
                ApplicationId = application.Id,
                SupplierId = application.SupplierId,
                SupplierCode = application.SupplierCode,
                Status = application.Status ?? string.Empty,
                SupplierStatus = supplier?.Status,
                SupplierStage = supplier?.Stage,
                CurrentApprover = currentApprover,
                SubmittedAt = application.CreatedAt,
                UpdatedAt = updatedAt,
                TempAccount = tempAccount,
                History = history,
            };
        }

        private async Task<List<SupplierRegistrationStatusHistoryEntry>> BuildHistoryAsync(
            int applicationId,
            string? fallbackCreatedAt,
            CancellationToken cancellationToken)
        {
            var entries = await _dbContext.AuditLogs.AsNoTracking()
                .Where(log => log.EntityType == "supplier_registration" &&
                              log.EntityId == applicationId.ToString(CultureInfo.InvariantCulture))
                .OrderBy(log => log.CreatedAt)
                .ToListAsync(cancellationToken);

            if (entries.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(fallbackCreatedAt))
                {
                    return new List<SupplierRegistrationStatusHistoryEntry>();
                }

                return new List<SupplierRegistrationStatusHistoryEntry>
                {
                    new()
                    {
                        Type = "registration",
                        Step = "submitted",
                        Status = "submitted",
                        OccurredAt = fallbackCreatedAt,
                    }
                };
            }

            var results = new List<SupplierRegistrationStatusHistoryEntry>();
            foreach (var entry in entries)
            {
                var changes = ParseChanges(entry.Changes);
                var action = entry.Action ?? string.Empty;
                var occurredAt = entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture);

                if (string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SupplierRegistrationStatusHistoryEntry
                    {
                        Type = "registration",
                        Step = "submitted",
                        Status = "submitted",
                        Approver = entry.ActorName,
                        OccurredAt = occurredAt,
                        Comments = GetChangeString(changes, "message"),
                    });
                    continue;
                }

                if (string.Equals(action, "approve_step", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(action, "approve_final_step", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SupplierRegistrationStatusHistoryEntry
                    {
                        Type = "approval",
                        Step = GetChangeString(changes, "step"),
                        Status = "approved",
                        Result = GetChangeString(changes, "nextStatus") ?? "approved",
                        Approver = entry.ActorName,
                        OccurredAt = occurredAt,
                        Comments = GetChangeString(changes, "comment") ??
                                   GetChangeString(changes, "approvalComment"),
                    });
                    continue;
                }

                if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SupplierRegistrationStatusHistoryEntry
                    {
                        Type = "approval",
                        Step = GetChangeString(changes, "step"),
                        Status = "rejected",
                        Approver = entry.ActorName,
                        OccurredAt = occurredAt,
                        Comments = GetChangeString(changes, "reason"),
                    });
                    continue;
                }

                if (string.Equals(action, "request_info", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SupplierRegistrationStatusHistoryEntry
                    {
                        Type = "approval",
                        Step = GetChangeString(changes, "step"),
                        Status = "pending_info",
                        Approver = entry.ActorName,
                        OccurredAt = occurredAt,
                        Comments = GetChangeString(changes, "message"),
                    });
                    continue;
                }

                if (string.Equals(action, "bind_supplier_code", StringComparison.OrdinalIgnoreCase))
                {
                    var code = GetChangeString(changes, "supplierCode");
                    results.Add(new SupplierRegistrationStatusHistoryEntry
                    {
                        Type = "approval",
                        Step = "code_binding",
                        Status = "completed",
                        Approver = entry.ActorName,
                        OccurredAt = occurredAt,
                        Comments = string.IsNullOrWhiteSpace(code) ? null : $"Supplier code: {code}",
                    });
                    continue;
                }

                results.Add(new SupplierRegistrationStatusHistoryEntry
                {
                    Type = "approval",
                    Step = GetChangeString(changes, "step"),
                    Status = action,
                    Approver = entry.ActorName,
                    OccurredAt = occurredAt,
                });
            }

            return results;
        }

        private async Task<SupplierRegistrationTempAccountInfo?> BuildTempAccountInfoAsync(
            SupplierRegistrationApplication application,
            Supplier? supplier,
            CancellationToken cancellationToken)
        {
            var trackingId = application.TrackingAccountId ??
                             application.ContactEmail?.Trim().ToLowerInvariant();
            User? account = null;

            if (!string.IsNullOrWhiteSpace(trackingId))
            {
                account = await _dbContext.Users.AsNoTracking()
                    .FirstOrDefaultAsync(user =>
                        user.Id == trackingId ||
                        (user.Email != null && user.Email.ToLower() == trackingId),
                        cancellationToken);
            }

            if (account == null)
            {
                account = await _dbContext.Users.AsNoTracking()
                    .FirstOrDefaultAsync(user => user.RelatedApplicationId == application.Id, cancellationToken);
            }

            if (account == null)
            {
                return null;
            }

            return new SupplierRegistrationTempAccountInfo
            {
                Id = account.Id,
                Username = string.IsNullOrWhiteSpace(account.Username) ? account.Id : account.Username,
                Status = account.Status,
                ExpiresAt = supplier?.TempAccountExpiresAt,
                LastLoginAt = account.LastLoginAt,
                CreatedAt = account.CreatedAt,
            };
        }

        private async Task<string?> ResolveCurrentApproverAsync(
            SupplierRegistrationApplication application,
            CancellationToken cancellationToken)
        {
            var status = application.Status ?? string.Empty;
            string? identifier = null;
            string? label = null;

            switch (status)
            {
                case RegistrationConstants.RegistrationStatusPendingPurchaser:
                    identifier = application.PurchaserId;
                    label = "Purchaser";
                    if (string.IsNullOrWhiteSpace(identifier))
                    {
                        identifier = application.AssignedPurchaserEmail;
                    }
                    break;
                case "pending_quality_manager":
                    identifier = application.QualityManagerId;
                    label = "Quality Manager";
                    break;
                case "pending_procurement_manager":
                    identifier = application.ProcurementManagerId;
                    label = "Procurement Manager";
                    break;
                case "pending_procurement_director":
                    identifier = application.ProcurementDirectorId;
                    label = "Procurement Director";
                    break;
                case "pending_finance_director":
                    identifier = application.FinanceDirectorId;
                    label = "Finance Director";
                    break;
                case RegistrationConstants.RegistrationStatusPendingCashier:
                    identifier = application.FinanceCashierId;
                    label = "Finance Cashier";
                    break;
                case "pending_accountant":
                case "pending_code_binding":
                    identifier = application.FinanceAccountantId;
                    label = "Finance Accountant";
                    break;
                case "pending_activation":
                    identifier = application.ContactEmail;
                    label = "Supplier Activation";
                    break;
            }

            return await ResolveUserDisplayAsync(identifier, label, cancellationToken);
        }

        private async Task<string?> ResolveUserDisplayAsync(
            string? identifier,
            string? fallbackLabel,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return fallbackLabel;
            }

            var trimmed = identifier.Trim();

            var lowered = trimmed.ToLowerInvariant();
            var user = await _dbContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.Id == trimmed ||
                    (u.Email != null && u.Email.ToLower() == lowered),
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(user?.Name))
            {
                return user.Name;
            }

            if (!string.IsNullOrWhiteSpace(user?.Email))
            {
                return user.Email;
            }

            return trimmed;
        }

        private static JsonElement? ParseChanges(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? GetChangeString(JsonElement? element, string name)
        {
            if (element == null || element.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in element.Value.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }

                if (property.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return property.Value.ToString();
                }
            }

            return null;
        }
    }
}
