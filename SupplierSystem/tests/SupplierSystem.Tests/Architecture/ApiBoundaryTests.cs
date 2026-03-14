using FluentAssertions;
using Xunit;

namespace SupplierSystem.Tests.Architecture;

public sealed class ApiBoundaryTests
{
    [Fact]
    public void SupplierFocusedControllers_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "SuppliersController.cs",
            "SupplierDocumentsController.cs",
            "SupplierDraftsController.cs",
            "SupplierImportController.cs",
            "SupplierWorkflowController.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
        }
    }

    [Fact]
    public void DashboardAndEmailControllers_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "DashboardController.cs",
            "DashboardController.Data.cs",
            "DashboardController.Todos.cs",
            "DashboardController.Stats.cs",
            "EmailSettingsController.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
        }
    }

    [Fact]
    public void UsersAndExchangeRatesControllers_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "UsersController.cs",
            "ExchangeRatesController.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
        }
    }

    [Fact]
    public void PurchasingGroupsController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "PurchasingGroupsController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
    }

    [Fact]
    public void OrganizationalUnitsControllers_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "OrganizationalUnitsController.cs",
            "OrganizationalUnitsController.Crud.cs",
            "OrganizationalUnitsController.Members.cs",
            "OrganizationalUnitsController.Suppliers.cs",
            "OrganizationalUnitsController.Helpers.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
        }
    }

    [Fact]
    public void CountryFreightRatesController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "CountryFreightRatesController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
    }

    [Fact]
    public void PermissionsControllers_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "PermissionsController.cs",
            "PermissionsController.Users.cs",
            "PermissionsController.Buyers.cs",
            "PermissionsController.Roles.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
        }
    }

    [Fact]
    public void BuyerAssignmentsController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "BuyerAssignmentsController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
    }

    [Fact]
    public void FilesController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "FilesController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
        content.Should().NotContain("new TempSupplierUpgradeRepository(");
        content.Should().NotContain("new SupplierFileRepository(");
        content.Should().NotContain("new TempSupplierUpgradeService(");
    }

    [Fact]
    public void TemplatesController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "TemplatesController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
        content.Should().Contain("TemplateDocumentStore");
    }

    [Fact]
    public void InvoicesController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "InvoicesController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
        content.Should().Contain("InvoiceStore");
    }

    [Fact]
    public void AuthController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "AuthController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
        content.Should().Contain("AuthReadStore");
    }

    [Fact]
    public void RequisitionsControllers_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "RequisitionsController.cs",
            "RequisitionsController.Crud.cs",
            "RequisitionsController.Workflow.cs",
            "RequisitionsController.Attachments.cs",
            "RequisitionsController.Helpers.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
        }
    }

    [Fact]
    public void AuditController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "AuditController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
        content.Should().Contain("AuditReadStore");
    }

    [Fact]
    public void SettlementsController_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "SettlementsController.cs",
            "SettlementsController.List.cs",
            "SettlementsController.MonthlyStatements.cs",
            "SettlementsController.PrePayment.cs",
            "SettlementsController.Disputes.cs",
            "SettlementsController.Helpers.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
        }

        var baseContent = File.ReadAllText(Path.Combine(apiRoot, "SettlementsController.cs"));
        baseContent.Should().Contain("SettlementStore");
    }

    [Fact]
    public void RfqControllers_ShouldNotDependOnDbContextDirectly()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "RfqController.cs",
            "RfqQuotesController.cs",
            "RfqReviewController.cs",
            "RfqExportController.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
        }
    }

    [Fact]
    public void RfqExportController_ShouldDependOnRfqControllerDataServiceInsteadOfDbContext()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "RfqExportController.cs"));

        content.Should().Contain("RfqControllerDataService");
        content.Should().NotContain("_dbContext");
    }

    [Fact]
    public void RfqQuotesAndReviewControllers_ShouldDependOnScenarioServices()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var quotesContent = File.ReadAllText(Path.Combine(apiRoot, "RfqQuotesController.cs"));
        var reviewContent = File.ReadAllText(Path.Combine(apiRoot, "RfqReviewController.cs"));

        quotesContent.Should().Contain("RfqQuoteService");
        quotesContent.Should().NotContain("_dbContext");

        reviewContent.Should().Contain("RfqService");
        reviewContent.Should().Contain("RfqQuoteService");
        reviewContent.Should().NotContain("_dbContext");
    }

    [Fact]
    public void RfqWorkflowController_ExtractedSlices_ShouldUseStoreInsteadOfDbContext()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "RfqWorkflowController.List.cs",
            "RfqWorkflowController.RfqEndpoints.cs",
            "RfqWorkflowController.RfqMutations.cs",
            "RfqWorkflowController.QuoteEndpoints.cs",
            "RfqWorkflowController.QuoteHelpers.cs",
            "RfqWorkflowController.ApprovalEndpoints.cs",
            "RfqWorkflowController.ReviewEndpoints.cs",
            "RfqWorkflowController.Notifications.cs",
            "RfqWorkflowController.PrEndpoints.cs",
            "RfqWorkflowController.PrExcelEndpoints.cs",
            "RfqWorkflowController.RfqHelpers.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("_dbContext", because: file);
            content.Should().Contain("_rfqWorkflowStore", because: file);
        }

        var baseContent = File.ReadAllText(Path.Combine(apiRoot, "RfqWorkflowController.cs"));
        baseContent.Should().NotContain("SupplierSystemDbContext");
        baseContent.Should().NotContain("_dbContext");
        baseContent.Should().Contain("RfqWorkflowStore");
    }

    [Fact]
    public void ReconciliationController_QueriesAndHelpers_ShouldUseStoreInsteadOfDbContext()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "ReconciliationController.Queries.cs",
            "ReconciliationController.Helpers.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
            content.Should().NotContain("_dbContext", because: file);
        }

        var baseContent = File.ReadAllText(Path.Combine(apiRoot, "ReconciliationController.cs"));
        baseContent.Should().NotContain("SupplierSystemDbContext");
        baseContent.Should().NotContain("_dbContext");
        baseContent.Should().Contain("ReconciliationStore");
    }

    [Fact]
    public void WorkflowsController_ShouldUseWorkflowStoreInsteadOfDbContext()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "WorkflowsController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
        content.Should().NotContain("_dbContext");
        content.Should().Contain("WorkflowStore");
    }

    [Fact]
    public void WhitelistBlacklistControllers_ShouldUseStoreInsteadOfDbContext()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "WhitelistBlacklistController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
        content.Should().NotContain("_dbContext");
        content.Should().Contain("WhitelistBlacklistStore");
    }

    [Fact]
    public void NotificationsController_ShouldUseNotificationStoreInsteadOfDbContext()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var content = File.ReadAllText(Path.Combine(apiRoot, "NotificationsController.cs"));

        content.Should().NotContain("SupplierSystemDbContext");
        content.Should().NotContain("using SupplierSystem.Infrastructure.Data;");
        content.Should().NotContain("_dbContext");
        content.Should().Contain("NotificationStore");
    }

    [Fact]
    public void ContractsController_Files_ShouldUseContractStoreInsteadOfDbContext()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "ContractsController.cs",
            "ContractsController.Contracts.cs",
            "ContractsController.Versions.cs",
            "ContractsController.Reminders.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
            content.Should().NotContain("_dbContext", because: file);
        }

        var baseContent = File.ReadAllText(Path.Combine(apiRoot, "ContractsController.cs"));
        baseContent.Should().Contain("ContractStore");
    }

    [Fact]
    public void WarehouseReceiptsController_Files_ShouldUseStoreInsteadOfDbContext()
    {
        var apiRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Controllers");
        var files = new[]
        {
            "WarehouseReceiptsController.cs",
            "WarehouseReceiptsController.Queries.cs",
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(apiRoot, file));
            content.Should().NotContain("SupplierSystemDbContext", because: file);
            content.Should().NotContain("using SupplierSystem.Infrastructure.Data;", because: file);
            content.Should().NotContain("_dbContext", because: file);
        }

        var baseContent = File.ReadAllText(Path.Combine(apiRoot, "WarehouseReceiptsController.cs"));
        baseContent.Should().Contain("WarehouseReceiptStore");
    }

    [Fact]
    public void SupplierDbContextMappings_ShouldLiveInDedicatedConfigurationFiles()
    {
        var infraRoot = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Infrastructure", "Data", "Configurations");
        var expectedFiles = new[]
        {
            "SupplierEntityConfiguration.cs",
            "SupplierTagEntityConfiguration.cs",
            "SupplierDocumentEntityConfiguration.cs",
            "SupplierDraftEntityConfiguration.cs",
            "SupplierRegistrationApplicationEntityConfiguration.cs",
            "SupplierRegistrationDraftEntityConfiguration.cs",
            "SupplierRegistrationBlacklistEntityConfiguration.cs",
            "SupplierDocumentWhitelistEntityConfiguration.cs",
            "SupplierRfqInvitationEntityConfiguration.cs",
        };

        foreach (var file in expectedFiles)
        {
            File.Exists(Path.Combine(infraRoot, file)).Should().BeTrue(because: file);
        }
    }

    private static string GetSupplierSystemRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "SupplierSystem.sln");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate SupplierSystem root directory.");
    }
}
