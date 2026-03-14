using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for RfqService
/// </summary>
public class RfqServiceTests
{
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<ILogger<RfqService>> _mockLogger;
    private readonly Mock<ILogger<RfqStateMachine>> _mockStateMachineLogger;
    private readonly SupplierSystemDbContext _dbContext;

    public RfqServiceTests()
    {
        _mockAuditService = new Mock<IAuditService>();
        _mockAuditService.Setup(x => x.LogAsync(It.IsAny<Application.Models.Audit.AuditEntry>()))
            .Returns(Task.CompletedTask);

        _mockLogger = new Mock<ILogger<RfqService>>();
        _mockStateMachineLogger = new Mock<ILogger<RfqStateMachine>>();

        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new SupplierSystemDbContext(options);
    }

    private RfqService CreateRfqService()
    {
        var stateMachine = new RfqStateMachine(_dbContext, _mockStateMachineLogger.Object);
        return new RfqService(_dbContext, _mockAuditService.Object, _mockLogger.Object, stateMachine);
    }

    private static AuthUser CreateAdminUser()
    {
        return new AuthUser
        {
            Id = "admin-001",
            Name = "Admin User",
            Role = "admin",
            Permissions = new List<string>
            {
                Permissions.RfqCreate,
                Permissions.RfqViewAll,
                Permissions.RfqEditAll,
                Permissions.RfqPublish,
                Permissions.RfqClose,
                Permissions.RfqDelete,
                Permissions.RfqInviteSuppliers,
                Permissions.RfqViewQuotes,
            }
        };
    }

    private static AuthUser CreateRegularUser()
    {
        return new AuthUser
        {
            Id = "user-001",
            Name = "Regular User",
            Role = "user",
            Permissions = new List<string>()
        };
    }

    private static Rfq CreateTestRfq(long id = 1, string status = "draft")
    {
        return new Rfq
        {
            Id = id,
            Title = $"Test RFQ {id}",
            Description = "Test Description",
            Status = status,
            CreatedBy = "user-001",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o"),
            IsLineItemMode = true,
            Currency = "CNY",
        };
    }

    private static Supplier CreateTestSupplier(int id = 1)
    {
        return new Supplier
        {
            Id = id,
            CompanyName = $"Supplier {id}",
            CompanyId = $"COMP-{id}",
            ContactEmail = $"contact{id}@supplier.com",
            SupplierCode = $"SUP-{id}",
        };
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreateRfq()
    {
        // Arrange
        var service = CreateRfqService();
        var user = CreateAdminUser();
        var request = new CreateRfqRequest
        {
            Title = "New RFQ",
            Description = "Test Description",
            RfqType = "standard",
            DeliveryPeriod = "2026-12-31",
            BudgetAmount = 10000m,
            Currency = "CNY",
            ValidUntil = "2026-12-31",
            Items = new List<CreateRfqItem>
            {
                new() { LineNumber = 1, Description = "Item 1", Quantity = 10, Unit = "pcs", TargetPrice = 100m }
            }
        };

        // Act
        var result = await service.CreateAsync(request, user, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result["title"].Should().Be("New RFQ");
        result["status"].Should().Be("draft");

        var rfqInDb = await _dbContext.Rfqs.FirstOrDefaultAsync();
        rfqInDb.Should().NotBeNull();
        rfqInDb!.Title.Should().Be("New RFQ");
        rfqInDb.Status.Should().Be("draft");
    }

    [Fact]
    public async Task CreateAsync_WithPastDeadline_ShouldThrowValidationError()
    {
        // Arrange
        var service = CreateRfqService();
        var user = CreateAdminUser();
        var request = new CreateRfqRequest
        {
            Title = "Invalid RFQ",
            ValidUntil = "2020-01-01" // Past date
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationErrorException>(() =>
            service.CreateAsync(request, user, CancellationToken.None));

        exception.Message.Should().Contain("Deadline cannot be in the past");
    }

    [Fact]
    public async Task CreateAsync_WithoutCreatePermission_ShouldThrow403()
    {
        // Arrange
        var service = CreateRfqService();
        var user = CreateRegularUser();
        var request = new CreateRfqRequest
        {
            Title = "Unauthorized RFQ"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.CreateAsync(request, user, CancellationToken.None));

        exception.Status.Should().Be(403);
    }

    [Fact]
    public async Task CreateAsync_WithLineItems_ShouldCreateLineItems()
    {
        // Arrange
        var service = CreateRfqService();
        var user = CreateAdminUser();
        var request = new CreateRfqRequest
        {
            Title = "RFQ with Items",
            Items = new List<CreateRfqItem>
            {
                new() { LineNumber = 1, Description = "Item 1", Quantity = 10, Unit = "pcs" },
                new() { LineNumber = 2, Description = "Item 2", Quantity = 5, Unit = "kg" }
            }
        };

        // Act
        var result = await service.CreateAsync(request, user, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var rfqId = Convert.ToInt64(result["id"]);
        var items = await _dbContext.RfqLineItems.Where(li => li.RfqId == rfqId).ToListAsync();
        items.Should().HaveCount(2);
    }

    #endregion

    #region ListAsync Tests

    [Fact]
    public async Task ListAsync_WithNoFilters_ShouldReturnAllAccessibleRfq()
    {
        // Arrange
        var service = CreateRfqService();

        await _dbContext.Rfqs.AddRangeAsync(
            CreateTestRfq(1, "draft"),
            CreateTestRfq(2, "published"),
            CreateTestRfq(3, "closed")
        );
        await _dbContext.SaveChangesAsync();

        var user = CreateAdminUser();

        // Act
        var (data, total) = await service.ListAsync(null, null, null, 1, 10, user, CancellationToken.None);

        // Assert
        total.Should().Be(3);
        data.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListAsync_WithStatusFilter_ShouldFilterByStatus()
    {
        // Arrange
        var service = CreateRfqService();

        await _dbContext.Rfqs.AddRangeAsync(
            CreateTestRfq(1, "draft"),
            CreateTestRfq(2, "published"),
            CreateTestRfq(3, "published")
        );
        await _dbContext.SaveChangesAsync();

        var user = CreateAdminUser();

        // Act
        var (data, total) = await service.ListAsync("published", null, null, 1, 10, user, CancellationToken.None);

        // Assert
        total.Should().Be(2);
        data.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_WithKeywordFilter_ShouldSearchTitleAndDescription()
    {
        // Arrange
        var service = CreateRfqService();

        await _dbContext.Rfqs.AddRangeAsync(
            new Rfq { Title = "Electronics RFQ", Status = "draft", CreatedBy = "admin-001" },
            new Rfq { Title = "Office Supplies", Status = "draft", CreatedBy = "admin-001" },
            new Rfq { Title = "Electronic Devices", Status = "draft", CreatedBy = "admin-001" }
        );
        await _dbContext.SaveChangesAsync();

        var user = CreateAdminUser();

        // Act
        var (data, total) = await service.ListAsync("draft", null, "Electronic", 1, 10, user, CancellationToken.None);

        // Assert
        total.Should().Be(2);
    }

    [Fact]
    public async Task ListAsync_WithoutViewAllPermission_ShouldOnlyReturnOwnRfq()
    {
        // Arrange
        var service = CreateRfqService();

        await _dbContext.Rfqs.AddRangeAsync(
            new Rfq { Title = "User 1 RFQ", Status = "draft", CreatedBy = "user-001" },
            new Rfq { Title = "Other User RFQ", Status = "draft", CreatedBy = "user-002" }
        );
        await _dbContext.SaveChangesAsync();

        var user = CreateRegularUser();

        // Act
        var (data, total) = await service.ListAsync(null, null, null, 1, 10, user, CancellationToken.None);

        // Assert
        total.Should().Be(1);
        data[0]["title"].Should().Be("User 1 RFQ");
    }

    #endregion

    #region GetDetailsAsync Tests

    [Fact]
    public async Task GetDetailsAsync_WithValidId_ShouldReturnRfqWithDetails()
    {
        // Arrange
        var service = CreateRfqService();
        var rfq = CreateTestRfq(1, "draft");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var user = CreateAdminUser();

        // Act
        var result = await service.GetDetailsAsync(1, user, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result["id"].Should().Be(1L);
        result["title"].Should().Be("Test RFQ 1");
    }

    [Fact]
    public async Task GetDetailsAsync_WithNonExistentId_ShouldThrowException()
    {
        // Arrange
        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.GetDetailsAsync(999, user, CancellationToken.None));

        exception.Message.Should().Contain("RFQ with id 999 not found");
    }

    [Fact]
    public async Task GetDetailsAsync_WithoutPermission_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        rfq.CreatedBy = "other-user";
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateRegularUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.GetDetailsAsync(1, user, CancellationToken.None));

        exception.Status.Should().Be(403);
        exception.Message.Should().Contain("No permission");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidRequest_ShouldUpdateRfq()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();
        var request = new CreateRfqRequest { Title = "Updated Title" };

        // Act
        var result = await service.UpdateAsync(1, request, user, CancellationToken.None);

        // Assert
        result["title"].Should().Be("Updated Title");

        var updatedRfq = await _dbContext.Rfqs.FindAsync(1L);
        updatedRfq!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateAsync_PublishedRfq_ShouldThrowError()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateRegularUser(); // No RfqEditAll permission
        var request = new CreateRfqRequest { Title = "Updated Title" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.UpdateAsync(1, request, user, CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("Cannot edit published RFQ");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentRfq_ShouldThrowException()
    {
        // Arrange
        var service = CreateRfqService();
        var user = CreateAdminUser();
        var request = new CreateRfqRequest { Title = "Updated Title" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.UpdateAsync(999, request, user, CancellationToken.None));

        exception.Message.Should().Contain("RFQ with id 999 not found");
    }

    #endregion

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_WithValidDraftRfq_ShouldPublish()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        rfq.ValidUntil = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        await _dbContext.Rfqs.AddAsync(rfq);

        var lineItem = new RfqLineItem
        {
            RfqId = 1,
            LineNumber = 1,
            ItemName = "Test Item",
            Quantity = 10,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.RfqLineItems.AddAsync(lineItem);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act
        var result = await service.PublishAsync(1, user, CancellationToken.None);

        // Assert
        result["status"].Should().Be("published");

        var publishedRfq = await _dbContext.Rfqs.FindAsync(1L);
        publishedRfq!.Status.Should().Be("published");
    }

    [Fact]
    public async Task PublishAsync_WithoutPublishPermission_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        rfq.ValidUntil = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateRegularUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.PublishAsync(1, user, CancellationToken.None));

        exception.Status.Should().Be(403);
    }

    [Fact]
    public async Task PublishAsync_AlreadyPublishedRfq_ShouldThrowError()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        rfq.ValidUntil = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        await _dbContext.Rfqs.AddAsync(rfq);

        var lineItem = new RfqLineItem
        {
            RfqId = 1,
            LineNumber = 1,
            ItemName = "Test Item",
            Quantity = 10,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.RfqLineItems.AddAsync(lineItem);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishAsync(1, user, CancellationToken.None));

        exception.Message.Should().Contain("Invalid state transition");
    }

    [Fact]
    public async Task PublishAsync_WithoutDeadline_ShouldThrowError()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        rfq.ValidUntil = null;
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishAsync(1, user, CancellationToken.None));

        exception.Message.Should().Contain("Cannot publish RFQ without deadline");
    }

    [Fact]
    public async Task PublishAsync_WithoutLineItems_ShouldThrowError()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        rfq.ValidUntil = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishAsync(1, user, CancellationToken.None));

        exception.Message.Should().Contain("Cannot publish RFQ without line items");
    }

    #endregion

    #region CloseAsync Tests

    [Fact]
    public async Task CloseAsync_WithPublishedRfq_ShouldClose()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act
        var result = await service.CloseAsync(1, user, CancellationToken.None);

        // Assert
        result["status"].Should().Be("closed");

        var closedRfq = await _dbContext.Rfqs.FindAsync(1L);
        closedRfq!.Status.Should().Be("closed");
    }

    [Fact]
    public async Task CloseAsync_WithoutClosePermission_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateRegularUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.CloseAsync(1, user, CancellationToken.None));

        exception.Status.Should().Be(403);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithDraftRfq_ShouldDelete()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act
        var result = await service.DeleteAsync(1, user, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        var deletedRfq = await _dbContext.Rfqs.FindAsync(1L);
        deletedRfq.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_PublishedRfq_ShouldThrow400()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.DeleteAsync(1, user, CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("Only draft RFQ can be deleted");
    }

    [Fact]
    public async Task DeleteAsync_WithoutDeletePermission_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateRegularUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.DeleteAsync(1, user, CancellationToken.None));

        exception.Status.Should().Be(403);
    }

    #endregion

    #region SendInvitationsAsync Tests

    [Fact]
    public async Task SendInvitationsAsync_WithValidRequest_ShouldCreateInvitations()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        rfq.ValidUntil = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act
        var result = await service.SendInvitationsAsync(1, new List<int> { 1 }, user, CancellationToken.None);

        // Assert
        result["success"].Should().Be(true);
        result["count"].Should().Be(1);

        var invitation = await _dbContext.SupplierRfqInvitations.FirstOrDefaultAsync();
        invitation.Should().NotBeNull();
        invitation!.RfqId.Should().Be(1);
        invitation.SupplierId.Should().Be(1);
        invitation.Status.Should().Be("sent");
    }

    [Fact]
    public async Task SendInvitationsAsync_DraftRfq_ShouldThrow400()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.SendInvitationsAsync(1, new List<int> { 1 }, user, CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("Can only invite suppliers to published RFQ");
    }

    [Fact]
    public async Task SendInvitationsAsync_EmptySupplierList_ShouldThrow400()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationErrorException>(() =>
            service.SendInvitationsAsync(1, new List<int>(), user, CancellationToken.None));

        exception.Message.Should().Contain("At least one supplier ID is required");
    }

    #endregion

    #region Status Transition Tests

    [Theory]
    [InlineData("draft", "published")]
    [InlineData("draft", "cancelled")]
    [InlineData("published", "in_progress")]
    [InlineData("published", "closed")]
    [InlineData("published", "cancelled")]
    [InlineData("in_progress", "confirmed")]
    [InlineData("in_progress", "closed")]
    [InlineData("in_progress", "cancelled")]
    [InlineData("confirmed", "closed")]
    public async Task ValidateRfqTransition_WithValidTransition_ShouldSucceed(string fromStatus, string toStatus)
    {
        // Arrange
        var rfq = CreateTestRfq(1, fromStatus);
        rfq.ValidUntil = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        await _dbContext.Rfqs.AddAsync(rfq);

        // Add line item if in line item mode
        if (rfq.IsLineItemMode)
        {
            var lineItem = new RfqLineItem
            {
                RfqId = 1,
                LineNumber = 1,
                ItemName = "Test Item",
                Quantity = 10,
                CreatedAt = DateTimeOffset.UtcNow.ToString("o")
            };
            await _dbContext.RfqLineItems.AddAsync(lineItem);
        }
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert - Only testing transitions that can be triggered
        if (toStatus == "published")
        {
            var result = await service.PublishAsync(1, user, CancellationToken.None);
            result["status"].Should().Be("published");
        }
        else if (toStatus == "closed")
        {
            var result = await service.CloseAsync(1, user, CancellationToken.None);
            result["status"].Should().Be("closed");
        }
    }

    [Theory]
    [InlineData("published", "published")]
    [InlineData("closed", "draft")]
    [InlineData("cancelled", "published")]
    public async Task ValidateRfqTransition_WithInvalidTransition_ShouldThrow(string fromStatus, string toStatus)
    {
        // Arrange
        var rfq = CreateTestRfq(1, fromStatus);
        rfq.ValidUntil = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        await _dbContext.Rfqs.AddAsync(rfq);

        // Add line item if in line item mode
        if (rfq.IsLineItemMode)
        {
            var lineItem = new RfqLineItem
            {
                RfqId = 1,
                LineNumber = 1,
                ItemName = "Test Item",
                Quantity = 10,
                CreatedAt = DateTimeOffset.UtcNow.ToString("o")
            };
            await _dbContext.RfqLineItems.AddAsync(lineItem);
        }
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqService();
        var user = CreateAdminUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            toStatus == "published"
                ? service.PublishAsync(1, user, CancellationToken.None)
                : service.CloseAsync(1, user, CancellationToken.None));

        exception.Message.Should().Contain("Invalid state transition");
    }

    #endregion
}
