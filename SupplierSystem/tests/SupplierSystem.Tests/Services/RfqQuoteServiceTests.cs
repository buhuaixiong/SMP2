using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for RfqQuoteService
/// </summary>
public class RfqQuoteServiceTests
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly Mock<ILogger<QuoteStateMachine>> _mockStateMachineLogger;
    private readonly Mock<ILogger<RfqPriceAuditService>> _mockPriceAuditLogger;

    public RfqQuoteServiceTests()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new SupplierSystemDbContext(options);
        _mockStateMachineLogger = new Mock<ILogger<QuoteStateMachine>>();
        _mockPriceAuditLogger = new Mock<ILogger<RfqPriceAuditService>>();
    }

    private RfqQuoteService CreateRfqQuoteService()
    {
        var stateMachine = new QuoteStateMachine(_dbContext, _mockStateMachineLogger.Object);
        var priceAuditService = new RfqPriceAuditService(_dbContext, _mockPriceAuditLogger.Object);
        return new RfqQuoteService(_dbContext, stateMachine, priceAuditService);
    }

    private static AuthUser CreateSupplierUser(int supplierId = 1)
    {
        return new AuthUser
        {
            Id = $"supplier-user-{supplierId}",
            Name = "Supplier User",
            Role = "supplier",
            SupplierId = supplierId,
            Permissions = new List<string>()
        };
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
                Permissions.RfqViewQuotes,
                Permissions.RfqEditAll,
                Permissions.RfqPublish,
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

    private static Rfq CreateTestRfq(long id = 1, string status = "published", string? validUntil = null)
    {
        return new Rfq
        {
            Id = id,
            Title = $"Test RFQ {id}",
            Description = "Test Description",
            Status = status,
            CreatedBy = "admin-001",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o"),
            IsLineItemMode = true,
            Currency = "CNY",
            ValidUntil = validUntil ?? DateTimeOffset.UtcNow.AddDays(30).ToString("o"),
        };
    }

    private static RfqLineItem CreateTestLineItem(long rfqId = 1, int lineNumber = 1)
    {
        return new RfqLineItem
        {
            Id = lineNumber,
            RfqId = rfqId,
            LineNumber = lineNumber,
            ItemName = $"Test Item {lineNumber}",
            Quantity = 10,
            Unit = "pcs",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            Status = "draft",
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

    private static SupplierRfqInvitation CreateTestInvitation(int rfqId = 1, int supplierId = 1, string status = "sent")
    {
        return new SupplierRfqInvitation
        {
            Id = supplierId,
            RfqId = rfqId,
            SupplierId = supplierId,
            Status = status,
            InvitedAt = DateTimeOffset.UtcNow.ToString("o"),
        };
    }

    #region SubmitQuoteAsync Tests

    [Fact]
    public async Task SubmitQuoteAsync_WithValidRequest_ShouldCreateQuote()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var lineItem = CreateTestLineItem(1, 1);
        await _dbContext.RfqLineItems.AddAsync(lineItem);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 1000m,
            Currency = "CNY",
            DeliveryPeriod = "2025-12-31",
            Remarks = "Test quote",
            Items = new List<QuoteItemRequest>
            {
                new() { LineNumber = 1, UnitPrice = 100m, TotalPrice = 1000m }
            }
        };

        // Act
        var result = await service.SubmitQuoteAsync(1, request, user, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result["rfq_id"].Should().Be(1L);
        result["supplier_id"].Should().Be(1);
        result["total_price"].Should().Be(1000m);
        result["status"].Should().Be("submitted");

        var quoteInDb = await _dbContext.Quotes.FirstOrDefaultAsync();
        quoteInDb.Should().NotBeNull();
        quoteInDb!.Status.Should().Be("submitted");
        quoteInDb.IsLatest.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitQuoteAsync_WithNonSupplierUser_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateRegularUser(); // No SupplierId
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 1000m,
            Currency = "CNY",
            DeliveryPeriod = "2025-12-31"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.SubmitQuoteAsync(1, request, user, null, CancellationToken.None));

        exception.Status.Should().Be(403);
        exception.Message.Should().Contain("Only suppliers can submit quotes");
    }

    [Fact]
    public async Task SubmitQuoteAsync_WithoutInvitation_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 1000m,
            Currency = "CNY",
            DeliveryPeriod = "2025-12-31"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.SubmitQuoteAsync(1, request, user, null, CancellationToken.None));

        exception.Status.Should().Be(403);
        exception.Message.Should().Contain("Supplier not invited to this RFQ");
    }

    [Fact]
    public async Task SubmitQuoteAsync_WithDeclinedInvitation_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "declined");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 1000m,
            Currency = "CNY",
            DeliveryPeriod = "2025-12-31"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.SubmitQuoteAsync(1, request, user, null, CancellationToken.None));

        exception.Status.Should().Be(403);
        exception.Message.Should().Contain("Invitation is no longer active");
    }

    [Fact]
    public async Task SubmitQuoteAsync_WithDraftRfq_ShouldThrow400()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "draft");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 1000m,
            Currency = "CNY",
            DeliveryPeriod = "2025-12-31"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.SubmitQuoteAsync(1, request, user, null, CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("RFQ is not accepting quotes");
    }

    [Fact]
    public async Task SubmitQuoteAsync_WithExpiredDeadline_ShouldThrow400()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published", validUntil: DateTimeOffset.UtcNow.AddDays(-1).ToString("o"));
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 1000m,
            Currency = "CNY",
            DeliveryPeriod = "2025-12-31"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.SubmitQuoteAsync(1, request, user, null, CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("RFQ deadline has passed");
    }

    [Fact]
    public async Task SubmitQuoteAsync_WithMissingRequiredFields_ShouldThrow()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = null, // Missing required field
            Currency = null,
            DeliveryPeriod = null
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationErrorException>(() =>
            service.SubmitQuoteAsync(1, request, user, null, CancellationToken.None));

        exception.Message.Should().Contain("Missing required fields");
    }

    [Fact]
    public async Task SubmitQuoteAsync_WithExistingQuote_ShouldMarkOldAsNotLatest()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        // Existing quote
        var existingQuote = new Quote
        {
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted",
            IsLatest = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("o"),
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("o")
        };
        await _dbContext.Quotes.AddAsync(existingQuote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 1000m,
            Currency = "CNY",
            DeliveryPeriod = "2025-12-31"
        };

        // Act
        var result = await service.SubmitQuoteAsync(1, request, user, null, CancellationToken.None);

        // Assert
        var oldQuote = await _dbContext.Quotes.FindAsync(1L);
        oldQuote!.IsLatest.Should().BeFalse();

        var newQuote = await _dbContext.Quotes.FindAsync(2L);
        newQuote!.IsLatest.Should().BeTrue();
    }

    #endregion

    #region UpdateQuoteAsync Tests

    [Fact]
    public async Task UpdateQuoteAsync_WithDraftQuote_ShouldUpdate()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "draft",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 900m,
            Currency = "USD",
            DeliveryPeriod = "2025-11-30"
        };

        // Act
        var result = await service.UpdateQuoteAsync(1, 1, request, user, CancellationToken.None);

        // Assert
        result["total_price"].Should().Be(900m);
        result["currency"].Should().Be("USD");

        var updatedQuote = await _dbContext.Quotes.FindAsync(1L);
        updatedQuote!.TotalAmount.Should().Be(900m);
        updatedQuote.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task UpdateQuoteAsync_WithWithdrawnQuote_ShouldUpdate()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "withdrawn",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest
        {
            TotalPrice = 950m,
            DeliveryPeriod = "2025-12-15"
        };

        // Act
        var result = await service.UpdateQuoteAsync(1, 1, request, user, CancellationToken.None);

        // Assert
        result["total_price"].Should().Be(950m);
    }

    [Fact]
    public async Task UpdateQuoteAsync_SubmittedQuote_ShouldThrow400()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted",
            SubmittedAt = DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest { TotalPrice = 900m };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.UpdateQuoteAsync(1, 1, request, user, CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("Cannot update submitted quote");
    }

    [Fact]
    public async Task UpdateQuoteAsync_OtherSupplierQuote_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 2, // Different supplier
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "draft",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1); // User is supplier 1
        var request = new SubmitQuoteRequest { TotalPrice = 900m };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.UpdateQuoteAsync(1, 1, request, user, CancellationToken.None));

        exception.Status.Should().Be(403);
        exception.Message.Should().Contain("Cannot update other supplier's quote");
    }

    [Fact]
    public async Task UpdateQuoteAsync_NonExistentQuote_ShouldThrowException()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);
        var request = new SubmitQuoteRequest { TotalPrice = 900m };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.UpdateQuoteAsync(1, 999, request, user, CancellationToken.None));

        exception.Message.Should().Contain("Quote with id 999 not found");
    }

    #endregion

    #region WithdrawQuoteAsync Tests

    [Fact]
    public async Task WithdrawQuoteAsync_WithSubmittedQuote_ShouldWithdraw()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted",
            SubmittedAt = DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);

        // Act
        var result = await service.WithdrawQuoteAsync(1, 1, user, CancellationToken.None);

        // Assert
        result["status"].Should().Be("withdrawn");

        var withdrawnQuote = await _dbContext.Quotes.FindAsync(1L);
        withdrawnQuote!.Status.Should().Be("withdrawn");
    }

    [Fact]
    public async Task WithdrawQuoteAsync_DraftQuote_ShouldThrow400()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "draft",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.WithdrawQuoteAsync(1, 1, user, CancellationToken.None));

        exception.Status.Should().Be(400);
        exception.Message.Should().Contain("Can only withdraw submitted quote");
    }

    [Fact]
    public async Task WithdrawQuoteAsync_OtherSupplierQuote_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var invitation = CreateTestInvitation(1, 1, "sent");
        await _dbContext.SupplierRfqInvitations.AddAsync(invitation);

        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 2, // Different supplier
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted",
            SubmittedAt = DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.WithdrawQuoteAsync(1, 1, user, CancellationToken.None));

        exception.Status.Should().Be(403);
        exception.Message.Should().Contain("Cannot withdraw other supplier's quote");
    }

    #endregion

    #region GetQuoteDetailsAsync Tests

    [Fact]
    public async Task GetQuoteDetailsAsync_WithValidId_ShouldReturnDetails()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted",
            SubmittedAt = DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);

        var lineItem = CreateTestLineItem(1, 1);
        await _dbContext.RfqLineItems.AddAsync(lineItem);

        var quoteLineItem = new QuoteLineItem
        {
            QuoteId = 1L,
            RfqLineItemId = 1L,
            UnitPrice = 80m,
            TotalPrice = 800m
        };
        await _dbContext.QuoteLineItems.AddAsync(quoteLineItem);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateSupplierUser(1);

        // Act
        var result = await service.GetQuoteDetailsAsync(1, user, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result["id"].Should().Be(1L);
        result["total_price"].Should().Be(800m);
        result["supplier_name"].Should().Be("Supplier 1");
        result["rfq_title"].Should().Be("Test RFQ 1");
        result["items"].Should().NotBeNull();
    }

    [Fact]
    public async Task GetQuoteDetailsAsync_WithoutPermission_ShouldThrow403()
    {
        // Arrange
        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted"
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var otherSupplier = CreateSupplierUser(2);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.GetQuoteDetailsAsync(1, otherSupplier, CancellationToken.None));

        exception.Status.Should().Be(403);
        exception.Message.Should().Contain("No permission to view this quote");
    }

    [Fact]
    public async Task GetQuoteDetailsAsync_WithViewQuotesPermission_ShouldAllow()
    {
        // Arrange
        var quote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddAsync(quote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var adminUser = CreateAdminUser();

        // Act
        var result = await service.GetQuoteDetailsAsync(1, adminUser, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result["id"].Should().Be(1L);
    }

    #endregion

    #region ComparePricesAsync Tests

    [Fact]
    public async Task ComparePricesAsync_ShouldReturnPriceAnalysis()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier1 = CreateTestSupplier(1);
        var supplier2 = CreateTestSupplier(2);
        await _dbContext.Suppliers.AddRangeAsync(supplier1, supplier2);

        var quote1 = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted",
            IsLatest = true,
            SubmittedAt = DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };

        var quote2 = new Quote
        {
            Id = 2L,
            RfqId = 1L,
            SupplierId = 2,
            TotalAmount = 1000m,
            Currency = "CNY",
            Status = "submitted",
            IsLatest = true,
            SubmittedAt = DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
        await _dbContext.Quotes.AddRangeAsync(quote1, quote2);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateAdminUser();

        // Act
        var result = await service.ComparePricesAsync(1, user, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result["quoteCount"].Should().Be(2);
        result["lowestPrice"].Should().Be(800m);
        result["highestPrice"].Should().Be(1000m);
        result["averagePrice"].Should().Be(900m);

        var quotes = result["quotes"] as List<Dictionary<string, object?>>;
        quotes.Should().HaveCount(2);
        // Should be sorted by price ascending
        (quotes![0]["total_price"] as decimal?).Should().Be(800m);
        (quotes[1]["total_price"] as decimal?).Should().Be(1000m);
    }

    [Fact]
    public async Task ComparePricesAsync_WithoutViewQuotesPermission_ShouldThrow403()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateRegularUser();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceErrorException>(() =>
            service.ComparePricesAsync(1, user, CancellationToken.None));

        exception.Status.Should().Be(403);
    }

    [Fact]
    public async Task ComparePricesAsync_WithNoQuotes_ShouldReturnEmptyAnalysis()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateAdminUser();

        // Act
        var result = await service.ComparePricesAsync(1, user, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result["quoteCount"].Should().Be(0);
        result["lowestPrice"].Should().BeNull();
        result["highestPrice"].Should().BeNull();
        result["averagePrice"].Should().BeNull();
        result["quotes"].Should().NotBeNull();
    }

    [Fact]
    public async Task ComparePricesAsync_ShouldExcludeNonLatestQuotes()
    {
        // Arrange
        var rfq = CreateTestRfq(1, "published");
        await _dbContext.Rfqs.AddAsync(rfq);

        var supplier = CreateTestSupplier(1);
        await _dbContext.Suppliers.AddAsync(supplier);

        var latestQuote = new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 800m,
            Currency = "CNY",
            Status = "submitted",
            IsLatest = true,
            SubmittedAt = DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o")
        };

        var oldQuote = new Quote
        {
            Id = 2L,
            RfqId = 1L,
            SupplierId = 1,
            TotalAmount = 700m, // Cheaper but old version
            Currency = "CNY",
            Status = "submitted",
            IsLatest = false,
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("o"),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("o")
        };
        await _dbContext.Quotes.AddRangeAsync(latestQuote, oldQuote);
        await _dbContext.SaveChangesAsync();

        var service = CreateRfqQuoteService();
        var user = CreateAdminUser();

        // Act
        var result = await service.ComparePricesAsync(1, user, CancellationToken.None);

        // Assert
        result["quoteCount"].Should().Be(1); // Only latest quote
    }

    #endregion
}
