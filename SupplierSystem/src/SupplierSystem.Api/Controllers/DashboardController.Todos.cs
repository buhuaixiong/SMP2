using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Controllers;

public sealed partial class DashboardController
{
    private async Task<IReadOnlyList<TodoItem>> ResolveTodosAsync(AuthUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Role))
        {
            return Array.Empty<TodoItem>();
        }

        return user.Role switch
        {
            "temp_supplier" or "formal_supplier" => user.SupplierId.HasValue
                ? await GetSupplierTodosAsync(user.SupplierId.Value, cancellationToken)
                : Array.Empty<TodoItem>(),
            "purchaser" => await GetPurchaserTodosAsync(user, cancellationToken),
            "procurement_manager" => await GetProcurementManagerTodosAsync(user, cancellationToken),
            "quality_manager" => await GetQualityManagerTodosAsync(user, cancellationToken),
            "procurement_director" => await GetProcurementDirectorTodosAsync(user, cancellationToken),
            "finance_accountant" => await GetFinanceAccountantTodosAsync(user, cancellationToken),
            "finance_director" => await GetFinanceDirectorTodosAsync(user, cancellationToken),
            "admin" => Array.Empty<TodoItem>(),
            _ => Array.Empty<TodoItem>()
        };
    }

    private async Task<List<TodoItem>> GetSupplierTodosAsync(int supplierId, CancellationToken cancellationToken)
    {
        var todos = new List<TodoItem>();
        var summary = await BuildSupplierSummaryContextAsync(supplierId, cancellationToken);
        if (summary == null)
        {
            return todos;
        }

        if (summary.Summary.MissingProfileFields.Count > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "incomplete_profile",
                Count = summary.Summary.MissingProfileFields.Count,
                Title = "Supplier profile incomplete",
                Route = "/supplier/profile",
                Priority = "high",
                Icon = "User"
            });
        }

        if (summary.Summary.MissingDocumentTypes.Count > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "missing_documents",
                Count = summary.Summary.MissingDocumentTypes.Count,
                Title = "Missing required documents",
                Route = "/supplier/file-uploads",
                Priority = "high",
                Icon = "FolderOpened"
            });
        }

        var pendingRfqs = await GetPendingRfqCountAsync(supplierId, cancellationToken);
        if (pendingRfqs > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_rfqs",
                Count = pendingRfqs,
                Title = "RFQs waiting for quote",
                Route = "/rfq",
                Priority = "warning",
                Icon = "Memo"
            });
        }

        var expiringDocumentCount = CountExpiringDates(summary.Documents.Select(d => d.ExpiresAt), DateTimeOffset.UtcNow);
        if (expiringDocumentCount > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "expiring_files",
                Count = expiringDocumentCount,
                Title = "Documents expiring soon",
                Route = "/supplier/file-uploads",
                Priority = "warning",
                Icon = "Clock"
            });
        }

        return todos;
    }

    private async Task<List<TodoItem>> GetPurchaserTodosAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var todos = new List<TodoItem>();

        var pendingUploads = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierFileUploadsByStatusAsync("pending_purchaser", cancellationToken));
        if (pendingUploads > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_file_approvals",
                Count = pendingUploads,
                Title = "Pending file approvals",
                Route = "/approval/file-uploads",
                Priority = "high",
                Icon = "Document"
            });
        }

        var expiringUploads = await GetExpiringUploadsCountAsync(cancellationToken);
        if (expiringUploads > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "expiring_supplier_files",
                Count = expiringUploads,
                Title = "Supplier documents expiring soon",
                Route = "/approval/file-uploads",
                Priority = "warning",
                Icon = "Clock"
            });
        }


        var pendingUpgrades = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierUpgradeApplicationsByStatusesAsync(new[] { "pending_procurement_review" }, cancellationToken));
        if (pendingUpgrades > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_upgrade_approvals",
                Count = pendingUpgrades,
                Title = "Pending upgrade approvals",
                Route = "/approval/upgrades",
                Priority = "high",
                Icon = "CircleCheck"
            });
        }

        var pendingChanges = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierChangeRequestsByStatusAsync("pending_purchaser", cancellationToken));
        if (pendingChanges > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_change_approvals",
                Count = pendingChanges,
                Title = "Pending change requests",
                Route = "/approval/supplier-changes",
                Priority = "warning",
                Icon = "Edit"
            });
        }

        var pendingRequisitions = await SafeCountAsync(
            () => _dashboardDataService.CountMaterialRequisitionsPendingAsync(cancellationToken));
        if (pendingRequisitions > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_requisitions",
                Count = pendingRequisitions,
                Title = "Pending purchase requisitions",
                Route = "/requisitions",
                Priority = "info",
                Icon = "Document"
            });
        }

        await AppendPendingRegistrationTodoAsync(user, todos, cancellationToken);
        return todos;
    }

    private async Task<List<TodoItem>> GetProcurementManagerTodosAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var todos = new List<TodoItem>();

        var pendingUpgrades = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierUpgradeApplicationsByStatusesAsync(
                new[] { "pending_procurement_manager_review", "pending_procurement_manager" },
                cancellationToken));
        var pendingChanges = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierChangeRequestsByStatusAsync("pending_procurement_manager", cancellationToken));
        var pendingFiles = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierFileUploadsByStatusAsync("pending_procurement_manager", cancellationToken));

        var totalPending = pendingUpgrades + pendingChanges + pendingFiles;
        if (totalPending > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_approvals",
                Count = totalPending,
                Title = "Pending approvals",
                Route = "/approvals",
                Priority = "high",
                Icon = "CircleCheck"
            });
        }

        var ongoingRfqs = await SafeCountAsync(
            () => _dashboardDataService.CountRfqsByStatusesAsync(new[] { "draft", "published", "in_progress" }, cancellationToken));
        if (ongoingRfqs > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "ongoing_rfqs",
                Count = ongoingRfqs,
                Title = "In-progress RFQs",
                Route = "/rfq",
                Priority = "info",
                Icon = "Memo"
            });
        }

        var incompleteSuppliers = await GetIncompleteSupplierCountAsync(cancellationToken);
        if (incompleteSuppliers > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "incomplete_suppliers",
                Count = incompleteSuppliers,
                Title = "Suppliers missing documents",
                Route = "/suppliers",
                Priority = "warning",
                Icon = "FolderOpened"
            });
        }

        await AppendPendingRegistrationTodoAsync(user, todos, cancellationToken);
        return todos;
    }

    private async Task<List<TodoItem>> GetQualityManagerTodosAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var todos = new List<TodoItem>();

        var pendingUpgrades = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierUpgradeApplicationsByStatusesAsync(
                new[] { "pending_quality_review", "pending_quality_manager" },
                cancellationToken));
        var pendingChanges = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierChangeRequestsByStatusAsync("pending_quality_manager", cancellationToken));
        var pendingFiles = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierFileUploadsByStatusAsync("pending_quality_manager", cancellationToken));

        var totalPending = pendingUpgrades + pendingChanges + pendingFiles;
        if (totalPending > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_quality_approvals",
                Count = totalPending,
                Title = "Pending quality approvals",
                Route = "/approval/upgrades",
                Priority = "high",
                Icon = "CircleCheck"
            });
        }

        await AppendPendingRegistrationTodoAsync(user, todos, cancellationToken);
        return todos;
    }

    private async Task<List<TodoItem>> GetProcurementDirectorTodosAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var todos = new List<TodoItem>();

        var pendingUpgrades = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierUpgradeApplicationsByStatusesAsync(
                new[] { "pending_procurement_director_review", "pending_procurement_director" },
                cancellationToken));
        var pendingChanges = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierChangeRequestsByStatusAsync("pending_procurement_director", cancellationToken));
        var pendingFiles = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierFileUploadsByStatusAsync("pending_procurement_director", cancellationToken));

        var totalPending = pendingUpgrades + pendingChanges + pendingFiles;
        if (totalPending > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_director_approvals",
                Count = totalPending,
                Title = "Pending director approvals",
                Route = "/approval/upgrades",
                Priority = "high",
                Icon = "CircleCheck"
            });
        }

        var ongoingRfqs = await SafeCountAsync(
            () => _dashboardDataService.CountRfqsByStatusesAsync(new[] { "draft", "published", "in_progress" }, cancellationToken));
        if (ongoingRfqs > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "ongoing_rfqs",
                Count = ongoingRfqs,
                Title = "In-progress RFQs",
                Route = "/rfq",
                Priority = "info",
                Icon = "Memo"
            });
        }

        await AppendPendingRegistrationTodoAsync(user, todos, cancellationToken);
        return todos;
    }

    private async Task<List<TodoItem>> GetFinanceAccountantTodosAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var todos = new List<TodoItem>();

        var pendingInvoices = await SafeCountAsync(
            () => _dashboardDataService.CountInvoicesByStatusAsync("pending", cancellationToken));
        if (pendingInvoices > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_invoices",
                Count = pendingInvoices,
                Title = "Pending invoices",
                Route = "/invoices",
                Priority = "high",
                Icon = "Tickets"
            });
        }

        await AppendPendingRegistrationTodoAsync(user, todos, cancellationToken);
        return todos;
    }

    private async Task<List<TodoItem>> GetFinanceDirectorTodosAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var todos = new List<TodoItem>();

        var pendingUpgrades = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierUpgradeApplicationsByStatusesAsync(
                new[] { "pending_finance_director_review", "pending_finance_director" },
                cancellationToken));
        var pendingChanges = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierChangeRequestsByStatusAsync("pending_finance_director", cancellationToken));
        var pendingFiles = await SafeCountAsync(
            () => _dashboardDataService.CountSupplierFileUploadsByStatusAsync("pending_finance_director", cancellationToken));
        var pendingInvoices = await SafeCountAsync(
            () => _dashboardDataService.CountInvoicesByStatusAsync("pending_director_approval", cancellationToken));

        var totalPending = pendingUpgrades + pendingChanges + pendingFiles + pendingInvoices;
        if (totalPending > 0)
        {
            todos.Add(new TodoItem
            {
                Type = "pending_finance_approvals",
                Count = totalPending,
                Title = "Pending finance approvals",
                Route = "/approval/upgrades",
                Priority = "high",
                Icon = "CircleCheck"
            });
        }

        await AppendPendingRegistrationTodoAsync(user, todos, cancellationToken);
        return todos;
    }

    private async Task AppendPendingRegistrationTodoAsync(
        AuthUser user,
        List<TodoItem> todos,
        CancellationToken cancellationToken)
    {
        var pendingCount = await GetPendingRegistrationCountAsync(user, cancellationToken);
        if (pendingCount <= 0)
        {
            return;
        }

        todos.Add(new TodoItem
        {
            Type = "pending_registrations",
            Count = pendingCount,
            Title = "Pending supplier registrations",
            Route = "/approvals",
            Priority = "high",
            Icon = "User"
        });
    }

    private async Task<int> GetPendingRegistrationCountAsync(AuthUser user, CancellationToken cancellationToken)
    {
        try
        {
            return await _registrationService.GetPendingCountAsync(user, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load pending registration approvals for dashboard");
            return 0;
        }
    }

    private async Task<int> SafeCountAsync(Func<Task<int>> countAction)
    {
        try
        {
            return await countAction();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard count query failed");
            return 0;
        }
    }
}


