using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for PurchaseOrderService
/// </summary>
public class PurchaseOrderServiceTests
{
    private static AuthUser CreateTestUser(string userId = "test-user", string role = "purchaser")
    {
        return new AuthUser
        {
            Id = userId,
            Name = "Test User",
            Role = role,
            Permissions = new List<string>
            {
                Permissions.RfqCreate,
                Permissions.PurchaserRfqTarget,
            }
        };
    }

    #region CreatePoAsync Tests

    [Fact]
    public async Task CreatePoAsync_WithValidInput_ShouldCreatePo()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq
        {
            Id = 1,
            Title = "Test RFQ",
            CreatedBy = "test-user",
            Status = "open"
        };
        dbContext.Rfqs.Add(rfq);

        var supplier = new Supplier
        {
            Id = 1,
            CompanyName = "Test Supplier",
            CompanyId = "SUP001"
        };
        dbContext.Suppliers.Add(supplier);

        var quote = new Quote
        {
            Id = 1,
            RfqId = 1,
            SupplierId = 1,
            TotalAmount = 1000m,
            Currency = "CNY",
            Status = "submitted",
            IsLatest = true
        };
        dbContext.Quotes.Add(quote);

        var lineItem = new RfqLineItem
        {
            Id = 1L,
            RfqId = 1L,
            LineNumber = 1,
            Status = "pending_po",
            SelectedQuoteId = 1L,
            Quantity = 10,
            Unit = "EA",
            EstimatedUnitPrice = 100m,
            Currency = "CNY"
        };
        dbContext.RfqLineItems.Add(lineItem);

        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act
        var result = await service.CreatePoAsync(
            rfqId: 1,
            supplierId: 1,
            lineItemIds: new List<int> { 1 },
            description: "Test PO",
            notes: "Test notes",
            user: CreateTestUser(),
            cancellationToken: default);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("id");
        result.Should().ContainKey("po_number");
        result.Should().ContainKey("rfq_id");
        result.Should().ContainKey("supplier_id");
        result["rfq_id"].Should().Be(1);
        result["supplier_id"].Should().Be(1);
        result["item_count"].Should().Be(1);
        result["total_amount"].Should().Be(1000m);

        var poCount = await dbContext.PurchaseOrders.CountAsync();
        poCount.Should().Be(1);
    }

    [Fact]
    public async Task CreatePoAsync_WithNonExistentRfq_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var supplier = new Supplier { Id = 1, CompanyName = "Test Supplier", CompanyId = "SUP001" };
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.CreatePoAsync(
            rfqId: 999,
            supplierId: 1,
            lineItemIds: new List<int> { 1 },
            description: null,
            notes: null,
            user: CreateTestUser(),
            cancellationToken: default);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*RFQ*not found*");
    }

    [Fact]
    public async Task CreatePoAsync_WithNonExistentSupplier_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.CreatePoAsync(
            rfqId: 1,
            supplierId: 999,
            lineItemIds: new List<int> { 1 },
            description: null,
            notes: null,
            user: CreateTestUser(),
            cancellationToken: default);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*Supplier*not found*");
    }

    [Fact]
    public async Task CreatePoAsync_WithEmptyLineItems_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var supplier = new Supplier { Id = 1, CompanyName = "Test Supplier", CompanyId = "SUP001" };
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.CreatePoAsync(
            rfqId: 1,
            supplierId: 1,
            lineItemIds: new List<int>(),
            description: null,
            notes: null,
            user: CreateTestUser(),
            cancellationToken: default);

        await action.Should().ThrowAsync<ValidationErrorException>()
            .WithMessage("*At least one line item*");
    }

    [Fact]
    public async Task CreatePoAsync_WithUnauthorizedUser_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "other-user" };
        dbContext.Rfqs.Add(rfq);

        var supplier = new Supplier { Id = 1, CompanyName = "Test Supplier", CompanyId = "SUP001" };
        dbContext.Suppliers.Add(supplier);

        var quote = new Quote { Id = 1, RfqId = 1, SupplierId = 1, TotalAmount = 100m };
        dbContext.Quotes.Add(quote);

        var lineItem = new RfqLineItem { Id = 1L, RfqId = 1L, Status = "pending_po", SelectedQuoteId = 1L };
        dbContext.RfqLineItems.Add(lineItem);

        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.CreatePoAsync(
            rfqId: 1,
            supplierId: 1,
            lineItemIds: new List<int> { 1 },
            description: null,
            notes: null,
            user: CreateTestUser(userId: "wrong-user"),
            cancellationToken: default);

        await action.Should().ThrowAsync<ServiceErrorException>()
            .WithMessage("*Only RFQ creator*");
    }

    [Fact]
    public async Task CreatePoAsync_WithWrongSupplierQuote_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var supplier = new Supplier { Id = 1, CompanyName = "Supplier 1", CompanyId = "SUP001" };
        var supplier2 = new Supplier { Id = 2, CompanyName = "Supplier 2", CompanyId = "SUP002" };
        dbContext.Suppliers.AddRange(supplier, supplier2);

        var quote = new Quote { Id = 1, RfqId = 1, SupplierId = 2, TotalAmount = 100m }; // Different supplier
        dbContext.Quotes.Add(quote);

        var lineItem = new RfqLineItem { Id = 1L, RfqId = 1L, Status = "pending_po", SelectedQuoteId = 1L };
        dbContext.RfqLineItems.Add(lineItem);

        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.CreatePoAsync(
            rfqId: 1,
            supplierId: 1, // Requesting PO for supplier 1
            lineItemIds: new List<int> { 1 },
            description: null,
            notes: null,
            user: CreateTestUser(),
            cancellationToken: default);

        await action.Should().ThrowAsync<ServiceErrorException>()
            .WithMessage("*different supplier*");
    }

    #endregion

    #region UpdatePoAsync Tests

    [Fact]
    public async Task UpdatePoAsync_WithValidData_ShouldUpdatePo()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var po = new PurchaseOrder
        {
            Id = 1,
            PoNumber = "PO001",
            RfqId = 1,
            SupplierId = 1,
            Status = "draft",
            TotalAmount = 1000m,
            CreatedBy = "test-user"
        };
        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        var updateData = new Dictionary<string, object?>
        {
            ["description"] = "Updated description",
            ["notes"] = "Updated notes"
        };

        // Act
        var result = await service.UpdatePoAsync(1, updateData, CreateTestUser(), default);

        // Assert
        result.Should().NotBeNull();
        result["description"].Should().Be("Updated description");
        result["notes"].Should().Be("Updated notes");

        var updatedPo = await dbContext.PurchaseOrders.FindAsync(1);
        updatedPo!.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdatePoAsync_WithNonDraftPo_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var po = new PurchaseOrder
        {
            Id = 1,
            PoNumber = "PO001",
            RfqId = 1,
            SupplierId = 1,
            Status = "submitted", // Not draft
            TotalAmount = 1000m,
            CreatedBy = "test-user"
        };
        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.UpdatePoAsync(1, new Dictionary<string, object?>(), CreateTestUser(), default);
        await action.Should().ThrowAsync<ServiceErrorException>()
            .WithMessage("*Only draft POs*");
    }

    #endregion

    #region SubmitPoAsync Tests

    [Fact]
    public async Task SubmitPoAsync_WithValidPo_ShouldSubmit()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var po = new PurchaseOrder
        {
            Id = 1,
            PoNumber = "PO001",
            RfqId = 1,
            SupplierId = 1,
            Status = "draft",
            TotalAmount = 1000m,
            PoFilePath = "/path/to/file.pdf",
            CreatedBy = "test-user"
        };
        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act
        var result = await service.SubmitPoAsync(1, CreateTestUser(), default);

        // Assert
        result.Should().NotBeNull();
        result["status"].Should().Be("submitted");

        var submittedPo = await dbContext.PurchaseOrders.FindAsync(1);
        submittedPo!.Status.Should().Be("submitted");
    }

    [Fact]
    public async Task SubmitPoAsync_WithoutPoFile_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var po = new PurchaseOrder
        {
            Id = 1,
            PoNumber = "PO001",
            RfqId = 1,
            SupplierId = 1,
            Status = "draft",
            PoFilePath = null, // No file
            CreatedBy = "test-user"
        };
        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.SubmitPoAsync(1, CreateTestUser(), default);
        await action.Should().ThrowAsync<ValidationErrorException>()
            .WithMessage("*PO file is required*");
    }

    #endregion

    #region GetPoAsync Tests

    [Fact]
    public async Task GetPoAsync_WithValidId_ShouldReturnPo()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var supplier = new Supplier
        {
            Id = 1,
            CompanyName = "Test Supplier",
            CompanyId = "SUP001",
            ContactPerson = "John Doe",
            ContactPhone = "12345678",
            ContactEmail = "john@test.com"
        };
        dbContext.Suppliers.Add(supplier);

        var po = new PurchaseOrder
        {
            Id = 1,
            PoNumber = "PO001",
            RfqId = 1,
            SupplierId = 1,
            Status = "draft",
            TotalAmount = 1000m,
            CreatedBy = "test-user"
        };
        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act
        var result = await service.GetPoAsync(1, default);

        // Assert
        result.Should().NotBeNull();
        result["id"].Should().Be(1);
        result["po_number"].Should().Be("PO001");
        result["supplier_name"].Should().Be("Test Supplier");
        result["supplier_contact"].Should().Be("John Doe");
    }

    [Fact]
    public async Task GetPoAsync_WithNonExistentPo_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);
        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.GetPoAsync(999, default);
        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*not found*");
    }

    #endregion

    #region DeletePoAsync Tests

    [Fact]
    public async Task DeletePoAsync_WithDraftPo_ShouldDelete()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var lineItem = new RfqLineItem { Id = 1L, RfqId = 1L, PoId = 1L };
        dbContext.RfqLineItems.Add(lineItem);

        var po = new PurchaseOrder { Id = 1, RfqId = 1, Status = "draft", CreatedBy = "test-user", PoNumber = "PO001" };
        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act
        await service.DeletePoAsync(1, CreateTestUser(), default);

        // Assert
        var poCount = await dbContext.PurchaseOrders.CountAsync();
        poCount.Should().Be(0);

        var updatedLineItem = await dbContext.RfqLineItems.FindAsync(1L);
        updatedLineItem!.PoId.Should().BeNull();
    }

    [Fact]
    public async Task DeletePoAsync_WithSubmittedPo_ShouldThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var po = new PurchaseOrder { Id = 1, RfqId = 1, Status = "submitted", CreatedBy = "test-user", PoNumber = "PO001" };
        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act & Assert
        var action = () => service.DeletePoAsync(1, CreateTestUser(), default);
        await action.Should().ThrowAsync<ServiceErrorException>()
            .WithMessage("*Only draft POs*");
    }

    #endregion

    #region ListPosForRfqAsync Tests

    [Fact]
    public async Task ListPosForRfqAsync_ShouldReturnPos()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1 };

        var supplier = new Supplier { Id = 1, CompanyName = "Test Supplier", CompanyId = "SUP001" };
        dbContext.Suppliers.Add(supplier);

        var po1 = new PurchaseOrder { Id = 1, RfqId = 1, SupplierId = 1, Status = "draft", CreatedBy = "test-user", PoNumber = "PO001" };
        var po2 = new PurchaseOrder { Id = 2, RfqId = 1, SupplierId = 1, Status = "submitted", CreatedBy = "test-user", PoNumber = "PO002" };
        dbContext.PurchaseOrders.AddRange(po1, po2);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act
        var result = await service.ListPosForRfqAsync(1, default);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetAvailableLineItemsGroupedBySupplierAsync Tests

    [Fact]
    public async Task GetAvailableLineItemsGroupedBySupplierAsync_ShouldGroupItems()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, CreatedBy = "test-user" };
        dbContext.Rfqs.Add(rfq);

        var supplier = new Supplier { Id = 1, CompanyName = "Supplier 1", CompanyId = "SUP001" };
        dbContext.Suppliers.Add(supplier);

        var quote = new Quote { Id = 1, RfqId = 1, SupplierId = 1, TotalAmount = 500m };
        dbContext.Quotes.Add(quote);

        var lineItem = new RfqLineItem
        {
            Id = 1L,
            RfqId = 1L,
            Status = "pending_po",
            SelectedQuoteId = 1L,
            LineNumber = 1
        };
        dbContext.RfqLineItems.Add(lineItem);
        await dbContext.SaveChangesAsync();

        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<PurchaseOrderService>>();
        var service = new PurchaseOrderService(dbContext, mockAuditService.Object, mockLogger.Object);

        // Act
        var result = await service.GetAvailableLineItemsGroupedBySupplierAsync(1, CreateTestUser(), default);

        // Assert
        result.Should().NotBeEmpty();
        result[0].Should().ContainKey("supplierId");
        result[0].Should().ContainKey("lineItems");
        result[0].Should().ContainKey("totalAmount");
    }

    #endregion
}
