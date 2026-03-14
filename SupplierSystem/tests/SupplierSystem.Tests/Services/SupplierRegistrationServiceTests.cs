using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Api.Services.Registrations;
using SupplierSystem.Application.DTOs.Registrations;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for SupplierRegistrationService
/// </summary>
public class SupplierRegistrationServiceTests
{
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<ILogger<SupplierRegistrationService>> _mockLogger;
    private readonly SupplierSystemDbContext _dbContext;

    public SupplierRegistrationServiceTests()
    {
        _mockAuditService = new Mock<IAuditService>();
        _mockAuditService.Setup(x => x.LogAsync(It.IsAny<AuditEntry>())).Returns(Task.CompletedTask);

        _mockLogger = new Mock<ILogger<SupplierRegistrationService>>();

        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new SupplierSystemDbContext(options);
    }

    private SupplierRegistrationService CreateService()
    {
        return new SupplierRegistrationService(_dbContext, _mockAuditService.Object, null!, _mockLogger.Object);
    }

    private static AuthUser CreateAdminUser(string role = "admin", List<string>? permissions = null)
    {
        return new AuthUser
        {
            Id = "admin-001",
            Name = "Admin User",
            Role = role,
            Permissions = permissions ?? new List<string> { Permissions.PurchaserRegistrationApprove }
        };
    }

    private static AuthUser CreatePurchaserUser()
    {
        return new AuthUser
        {
            Id = "purchaser-001",
            Name = "Purchaser",
            Role = "purchaser",
            Email = "purchaser@test.com",
            Permissions = new List<string> { Permissions.PurchaserRegistrationApprove }
        };
    }

    private static AuthUser CreateQualityManagerUser()
    {
        return new AuthUser
        {
            Id = "quality-001",
            Name = "Quality Manager",
            Role = "quality_manager",
            Permissions = new List<string> { Permissions.QualityManagerRegistrationApprove }
        };
    }

    private static AuthUser CreateFinanceAccountantUser()
    {
        return new AuthUser
        {
            Id = "accountant-001",
            Name = "Accountant",
            Role = "finance_accountant",
            Permissions = new List<string> { Permissions.FinanceAccountantRegistrationApprove }
        };
    }

    private static SupplierRegistrationApplication CreateFullApplication(int id, string status)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        return new SupplierRegistrationApplication
        {
            Id = id,
            CompanyName = $"Test Company {id}",
            CompanyType = "Corporation",
            RegisteredOffice = "Test City",
            BusinessRegistrationNumber = $"REG-{id}",
            BusinessAddress = "123 Test Street",
            ContactName = "John Doe",
            ContactEmail = $"contact{id}@test.com",
            ContactPhone = "1234567890",
            OperatingCurrency = "USD",
            DeliveryLocation = "Shanghai",
            ShipCode = "FOB",
            BankName = "Test Bank",
            BankAddress = "456 Bank Ave",
            BankAccountNumber = $"123456789{id}",
            SupplierClassification = "DM",
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    #region GetPendingAsync Tests

    [Fact]
    public async Task GetPendingAsync_WithPurchaserRole_ShouldReturnPendingPurchaserApplications()
    {
        // Arrange
        var application1 = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        application1.ProcurementEmail = "purchaser@test.com";
        var application2 = CreateFullApplication(2, RegistrationConstants.RegistrationStatusPendingPurchaser);
        application2.ProcurementEmail = "purchaser@test.com";

        await _dbContext.SupplierRegistrationApplications.AddRangeAsync(application1, application2);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act
        var result = await service.GetPendingAsync(user, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPendingAsync_WithWrongRole_ShouldReturnEmptyList()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreateAdminUser("user"); // No approval permissions

        // Act
        var result = await service.GetPendingAsync(user, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingAsync_WithQualityManagerRole_ShouldReturnPendingQualityManagerApps()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingQualityManager);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreateQualityManagerUser();

        // Act
        var result = await service.GetPendingAsync(user, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnApplication()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.GetByIdAsync(1, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.CompanyName.Should().Be("Test Company 1");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldThrow404()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.GetByIdAsync(999, CancellationToken.None));

        exception.Status.Should().Be(404);
    }

    #endregion

    #region ApproveAsync Tests

    [Fact]
    public async Task ApproveAsync_WithValidApplication_ShouldApproveAndMoveToNextStep()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act
        var result = await service.ApproveAsync(1, user, "Looks good", "127.0.0.1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.NextStatus.Should().Be(RegistrationConstants.RegistrationStatusPendingQualityManager);

        var updatedApp = await _dbContext.SupplierRegistrationApplications.FindAsync(1);
        updatedApp!.Status.Should().Be(RegistrationConstants.RegistrationStatusPendingQualityManager);
        updatedApp.PurchaserId.Should().Be("purchaser-001");
        updatedApp.PurchaserApprovalStatus.Should().Be("approved");
    }

    [Fact]
    public async Task ApproveAsync_WithNonExistentApplication_ShouldThrow404()
    {
        // Arrange
        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.ApproveAsync(999, user, null, "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(404);
    }

    [Fact]
    public async Task ApproveAsync_WithActivatedApplication_ShouldThrow400()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusActivated);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.ApproveAsync(1, user, null, "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("already activated");
    }

    [Fact]
    public async Task ApproveAsync_WithRejectedApplication_ShouldThrow400()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusRejected);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.ApproveAsync(1, user, null, "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("already rejected");
    }

    [Fact]
    public async Task ApproveAsync_WithWrongRole_ShouldThrow403()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var qualityManager = CreateQualityManagerUser(); // Wrong role for this step

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.ApproveAsync(1, qualityManager, null, "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(403);
    }

    #endregion

    #region RejectAsync Tests

    [Fact]
    public async Task RejectAsync_WithValidRequest_ShouldRejectApplication()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act
        var result = await service.RejectAsync(1, user, "Incomplete documentation", "127.0.0.1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        var updatedApp = await _dbContext.SupplierRegistrationApplications.FindAsync(1);
        updatedApp!.Status.Should().Be(RegistrationConstants.RegistrationStatusRejected);
        updatedApp.RejectedBy.Should().Be("purchaser-001");
        updatedApp.RejectionReason.Should().Be("Incomplete documentation");
    }

    [Fact]
    public async Task RejectAsync_WithEmptyReason_ShouldThrow400()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.RejectAsync(1, user, "", "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(400);
    }

    [Fact]
    public async Task RejectAsync_WithActivatedApplication_ShouldThrow400()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusActivated);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.RejectAsync(1, user, "Some reason", "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("Cannot reject activated application");
    }

    #endregion

    #region RequestInfoAsync Tests

    [Fact]
    public async Task RequestInfoAsync_WithValidRequest_ShouldSetPendingInfo()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act
        var result = await service.RequestInfoAsync(1, user, "Please provide bank statement", "127.0.0.1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        var updatedApp = await _dbContext.SupplierRegistrationApplications.FindAsync(1);
        updatedApp!.PurchaserApprovalStatus.Should().Be("pending_info");
    }

    [Fact]
    public async Task RequestInfoAsync_WithEmptyMessage_ShouldThrow400()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreatePurchaserUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.RequestInfoAsync(1, user, "", "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(400);
    }

    #endregion

    #region BindSupplierCodeAsync Tests

    [Fact]
    public async Task BindSupplierCodeAsync_WithValidRequest_ShouldCreateSupplierAndBindCode()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingCodeBinding);
        application.SupplierClassification = "DM";
        application.OperatingCurrency = "USD";

        // Create tracking user with password
        var trackingUser = new User
        {
            Id = "contact1@test.com",
            Username = "contact1@test.com",
            Name = "Test User",
            Role = "tracking",
            Password = "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/X4.Bj1YJxyrR1C1Pe", // Dummy hash
            Email = "contact1@test.com",
            AccountType = "tracking",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };

        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.Users.AddAsync(trackingUser);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreateFinanceAccountantUser();

        // Act
        var result = await service.BindSupplierCodeAsync(1, user, null, "127.0.0.1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SupplierCode.Should().StartWith("610"); // DM + USD prefix
        result.SupplierId.Should().BeGreaterThan(0);

        var supplier = await _dbContext.Suppliers.FindAsync(result.SupplierId);
        supplier.Should().NotBeNull();
        supplier!.Status.Should().Be("approved");
        supplier.Stage.Should().Be("temporary");
    }

    [Fact]
    public async Task BindSupplierCodeAsync_WithNonFinanceAccountantRole_ShouldThrow403()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingCodeBinding);
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var purchaser = CreatePurchaserUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.BindSupplierCodeAsync(1, purchaser, null, "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(403);
        exception.Message.Should().Contain("Only finance accountants");
    }

    [Fact]
    public async Task BindSupplierCodeAsync_WithWrongStatus_ShouldThrow400()
    {
        // Arrange
        var application = CreateFullApplication(1, RegistrationConstants.RegistrationStatusPendingPurchaser); // Wrong status
        await _dbContext.SupplierRegistrationApplications.AddAsync(application);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var user = CreateFinanceAccountantUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.BindSupplierCodeAsync(1, user, null, "127.0.0.1", CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("Current status does not allow code binding");
    }

    #endregion

    #region SupplierCode Prefix Resolution Tests

    [Theory]
    [InlineData("DM", "RMB", "810")]
    [InlineData("DM", "USD", "610")]
    [InlineData("DM", "KRW", "617")]
    [InlineData("DM", "THB", "618")]
    [InlineData("DM", "JPY", "619")]
    [InlineData("IDM", "RMB", "813")]
    [InlineData("IDM", "USD", "613")]
    [InlineData("IDM", "THB", "614")]
    [InlineData("IDM", "JPY", "615")]
    public void ResolveSupplierCodePrefix_WithValidInputs_ShouldReturnCorrectPrefix(string classification, string currency, string expectedPrefix)
    {
        // Arrange
        var service = CreateService();

        // Act
        var prefixes = ResolvePrefixes(service, classification, currency);

        // Assert
        prefixes.Should().Contain(expectedPrefix);
    }

    [Theory]
    [InlineData("INVALID", "USD")]
    [InlineData("DM", "INVALID")]
    public void ResolveSupplierCodePrefix_WithInvalidInputs_ShouldReturnNull(string classification, string currency)
    {
        // Arrange
        var service = CreateService();

        // Act
        var prefixes = ResolvePrefixes(service, classification, currency);

        // Assert
        prefixes.Should().BeEmpty();
    }

    private static IReadOnlyList<string> ResolvePrefixes(SupplierRegistrationService service, string classification, string currency)
    {
        var method = service.GetType()
            .GetMethod("ResolveSupplierCodePrefixes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IReadOnlyList<string>?)method?.Invoke(null, new object[] { classification, currency })
               ?? Array.Empty<string>();
    }

    #endregion
}
