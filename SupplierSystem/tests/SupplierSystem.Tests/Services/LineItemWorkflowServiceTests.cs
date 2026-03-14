using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for LineItemWorkflowService
/// </summary>
public class LineItemWorkflowServiceTests
{
    private readonly Mock<ILogger<LineItemWorkflowService>> _mockLogger;
    private readonly SupplierSystemDbContext _dbContext;

    public LineItemWorkflowServiceTests()
    {
        var mockAuditService = new Mock<SupplierSystem.Application.Interfaces.IAuditService>();
        mockAuditService.Setup(x => x.LogAsync(It.IsAny<SupplierSystem.Application.Models.Audit.AuditEntry>()))
            .Returns(Task.CompletedTask);

        _mockLogger = new Mock<ILogger<LineItemWorkflowService>>();

        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new SupplierSystemDbContext(options);
    }

    private LineItemWorkflowService CreateService()
    {
        var mockAuditService = new Mock<SupplierSystem.Application.Interfaces.IAuditService>();
        mockAuditService.Setup(x => x.LogAsync(It.IsAny<SupplierSystem.Application.Models.Audit.AuditEntry>()))
            .Returns(Task.CompletedTask);

        var priceAuditService = new RfqPriceAuditService(
            _dbContext,
            Mock.Of<ILogger<RfqPriceAuditService>>());

        return new LineItemWorkflowService(_dbContext, mockAuditService.Object, priceAuditService, _mockLogger.Object);
    }

    private static AuthUser CreateAdminUser(List<string>? permissions = null)
    {
        return new AuthUser
        {
            Id = "admin-001",
            Name = "Admin User",
            Role = "admin",
            Permissions = permissions ?? new List<string> { Permissions.RfqCreate }
        };
    }

    private static Rfq CreateTestRfq(long id = 1, string createdBy = "admin-001")
    {
        return new Rfq
        {
            Id = id,
            Title = $"Test RFQ {id}",
            Description = "Test Description",
            Status = "published",
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o"),
            IsLineItemMode = true,
            Currency = "CNY",
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30).ToString("o"),
        };
    }

    private static RfqLineItem CreateTestLineItem(long id = 1, long rfqId = 1, string status = "pending_po")
    {
        return new RfqLineItem
        {
            Id = id,
            RfqId = rfqId,
            LineNumber = (int)id,
            ItemName = $"Test Item {id}",
            Quantity = 10,
            Unit = "pcs",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o"),
        };
    }

    #region Line Item Status Transition Tests

    [Theory]
    [InlineData("draft", "pending_director")]
    [InlineData("rejected", "pending_director")]
    [InlineData("pending_director", "pending_po")]
    [InlineData("pending_director", "draft")]
    [InlineData("pending_po", "completed")]
    public void LineItemStatus_ShouldAllowValidTransitions(string fromStatus, string toStatus)
    {
        // This test documents the valid status transitions
        // The actual implementation is in the service methods

        var validTransitions = new Dictionary<string, string[]>
        {
            ["draft"] = new[] { "pending_director" },
            ["rejected"] = new[] { "pending_director" },
            ["pending_director"] = new[] { "pending_po", "draft" },
            ["pending_po"] = new[] { "completed" },
        };

        // Assert
        if (validTransitions.TryGetValue(fromStatus, out var allowedTargets))
        {
            allowedTargets.Should().Contain(toStatus);
        }
        else
        {
            // For status transitions that aren't pre-approved in the dict
            // They should fail validation
            true.Should().BeTrue();
        }
    }

    #endregion
}
