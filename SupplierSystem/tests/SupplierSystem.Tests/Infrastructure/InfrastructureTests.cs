using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Data.Configurations;
using Xunit;

namespace SupplierSystem.Tests.Infrastructure;

/// <summary>
/// Unit tests for Infrastructure layer components
/// </summary>
public class InfrastructureTests
{
    #region UserEntityConfiguration Tests

    [Fact]
    public void UserEntityConfiguration_ShouldConfigureUserCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);

        // Act - Add a user to trigger entity configuration
        var user = CreateTestUser();
        context.Users.Add(user);
        context.SaveChanges();

        // Assert
        var savedUser = context.Users.FirstOrDefault(u => u.Id == "test-user-001");
        savedUser.Should().NotBeNull();
        savedUser!.Name.Should().Be("Test User");
        savedUser.Username.Should().Be("testuser");
        savedUser.Email.Should().Be("test@example.com");
        savedUser.Role.Should().Be("user");
        savedUser.Status.Should().Be("active");
    }

    [Fact]
    public void UserEntityConfiguration_ShouldHaveCorrectTableName()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        var configuration = new UserEntityConfiguration();

        // Act
        configuration.Configure(modelBuilder.Entity<User>());

        // Assert
        var entity = modelBuilder.Model.FindEntityType(typeof(User));
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("users");
    }

    [Fact]
    public void UserEntityConfiguration_ShouldHavePrimaryKeyOnId()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        var configuration = new UserEntityConfiguration();

        // Act
        configuration.Configure(modelBuilder.Entity<User>());

        // Assert
        var entity = modelBuilder.Model.FindEntityType(typeof(User));
        entity.Should().NotBeNull();
        var primaryKey = entity!.FindPrimaryKey();
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().Contain(p => p.Name == "Id");
    }

    [Fact]
    public void UserEntityConfiguration_ShouldHaveIndexesOnEmailAndUsername()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        var configuration = new UserEntityConfiguration();

        // Act
        configuration.Configure(modelBuilder.Entity<User>());

        // Assert
        var entity = modelBuilder.Model.FindEntityType(typeof(User));
        entity.Should().NotBeNull();

        var emailIndex = entity!.GetIndexes().FirstOrDefault(i =>
            i.Properties.Any(p => p.Name == "Email"));
        emailIndex.Should().NotBeNull();

        var usernameIndex = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Any(p => p.Name == "Username"));
        usernameIndex.Should().NotBeNull();
    }

    [Fact]
    public void UserEntityConfiguration_ShouldHaveMaxLengthConstraints()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        var configuration = new UserEntityConfiguration();

        // Act
        configuration.Configure(modelBuilder.Entity<User>());

        // Assert
        var entity = modelBuilder.Model.FindEntityType(typeof(User));

        var idProperty = entity!.FindProperty("Id");
        idProperty!.GetMaxLength().Should().Be(64);

        var nameProperty = entity.FindProperty("Name");
        nameProperty!.GetMaxLength().Should().Be(200);

        var usernameProperty = entity.FindProperty("Username");
        usernameProperty!.GetMaxLength().Should().Be(100);

        var roleProperty = entity.FindProperty("Role");
        roleProperty!.GetMaxLength().Should().Be(50);
    }

    [Fact]
    public void UserEntityConfiguration_ShouldHaveRequiredProperties()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        var configuration = new UserEntityConfiguration();

        // Act
        configuration.Configure(modelBuilder.Entity<User>());

        // Assert
        var entity = modelBuilder.Model.FindEntityType(typeof(User));

        var idProperty = entity!.FindProperty("Id");
        idProperty!.IsNullable.Should().BeFalse();

        var nameProperty = entity.FindProperty("Name");
        nameProperty!.IsNullable.Should().BeFalse();

        var usernameProperty = entity.FindProperty("Username");
        usernameProperty!.IsNullable.Should().BeFalse();

        var roleProperty = entity.FindProperty("Role");
        roleProperty!.IsNullable.Should().BeFalse();

        var passwordProperty = entity.FindProperty("Password");
        passwordProperty!.IsNullable.Should().BeFalse();
    }

    #endregion

    #region DbContext Tests

    [Fact]
    public void DbContext_ShouldHaveAllDbSets()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);

        // Act & Assert
        context.Users.Should().NotBeNull();
        context.Suppliers.Should().NotBeNull();
        context.Rfqs.Should().NotBeNull();
        context.Quotes.Should().NotBeNull();
        context.PurchaseOrders.Should().NotBeNull();
        context.AuditLogs.Should().NotBeNull();
        context.Notifications.Should().NotBeNull();
        context.ActiveSessions.Should().NotBeNull();
        context.SystemConfigs.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_ShouldConfigureTriggerMetadataForBuyerSupplierPermissions()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(BuyerSupplierPermission));

        entityType.Should().NotBeNull();
        entityType!.GetDeclaredTriggers().Should().NotBeEmpty();
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveUser()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);
        var user = CreateTestUser();

        // Act
        context.Users.Add(user);
        context.SaveChanges();

        // Assert
        var retrieved = context.Users.Find("test-user-001");
        retrieved.Should().NotBeNull();
        retrieved!.Username.Should().Be("testuser");
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveSupplier()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);
        var supplier = CreateTestSupplier();

        // Act
        context.Suppliers.Add(supplier);
        context.SaveChanges();

        // Assert
        var retrieved = context.Suppliers.Find(1);
        retrieved.Should().NotBeNull();
        retrieved!.CompanyName.Should().Be("Test Supplier");
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveRfq()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);
        var rfq = CreateTestRfq();

        // Act
        context.Rfqs.Add(rfq);
        context.SaveChanges();

        // Assert
        var retrieved = context.Rfqs.Find(1L);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Test RFQ");
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveQuote()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);
        var quote = CreateTestQuote();

        // Act
        context.Quotes.Add(quote);
        context.SaveChanges();

        // Assert
        var retrieved = context.Quotes.Find(1L);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be("submitted");
    }

    [Fact]
    public void DbContext_CanAddAndRetrievePurchaseOrder()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);
        var po = CreateTestPurchaseOrder();

        // Act
        context.PurchaseOrders.Add(po);
        context.SaveChanges();

        // Assert
        var retrieved = context.PurchaseOrders.Find(1);
        retrieved.Should().NotBeNull();
        retrieved!.PoNumber.Should().Be("PO-001");
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveAuditLog()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);
        var auditLog = CreateTestAuditLog();

        // Act
        context.AuditLogs.Add(auditLog);
        context.SaveChanges();

        // Assert
        var retrieved = context.AuditLogs.Find(1);
        retrieved.Should().NotBeNull();
        retrieved!.Action.Should().Be("test_action");
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveActiveSession()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);
        var session = CreateTestActiveSession();

        // Act
        context.ActiveSessions.Add(session);
        context.SaveChanges();

        // Assert
        var retrieved = context.ActiveSessions.Find(1);
        retrieved.Should().NotBeNull();
        retrieved!.UserId.Should().Be("user-001");
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveSystemConfig()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);
        var config = CreateTestSystemConfig();

        // Act
        context.SystemConfigs.Add(config);
        context.SaveChanges();

        // Assert
        var retrieved = context.SystemConfigs.Find("test_config_key");
        retrieved.Should().NotBeNull();
        retrieved!.Value.Should().Be("test_value");
    }

    #endregion

    #region DateTime ValueConverter Tests

    [Fact]
    public void DateTimeParsing_ShouldHandleValidFormat()
    {
        // Arrange
        var dateTimeString = "2024-01-15T10:30:00.0000000Z";

        // Act
        var dateTime = DateTime.Parse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        // Assert
        dateTime.Year.Should().Be(2024);
        dateTime.Month.Should().Be(1);
        dateTime.Day.Should().Be(15);
    }

    [Fact]
    public void DateTimeParsing_ShouldHandleNullAndEmpty()
    {
        // Arrange & Act
        var nullResult = string.IsNullOrWhiteSpace(null);
        var emptyResult = string.IsNullOrWhiteSpace("");

        // Assert
        nullResult.Should().BeTrue();
        emptyResult.Should().BeTrue();
    }

    [Fact]
    public void DbContext_ActiveSessionCreatedAt_ShouldNotUseDateTimeConverter()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(ActiveSession));
        var createdAtProperty = entityType!.FindProperty(nameof(ActiveSession.CreatedAt));

        // Assert
        createdAtProperty.Should().NotBeNull();
        createdAtProperty!.GetValueConverter().Should().BeNull();
    }

    [Fact]
    public void DbContext_SupplierTag_ShouldUseCamelCaseColumnNames()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(SupplierTag));
        var storeObject = StoreObjectIdentifier.Table("supplier_tags", null);
        var supplierIdColumnName = entityType!
            .FindProperty(nameof(SupplierTag.SupplierId))!
            .GetColumnName(storeObject);
        var tagIdColumnName = entityType!
            .FindProperty(nameof(SupplierTag.TagId))!
            .GetColumnName(storeObject);

        // Assert
        supplierIdColumnName.Should().Be("supplierId");
        tagIdColumnName.Should().Be("tagId");
    }

    [Fact]
    public void DbContext_SupplierDocument_ShouldUseCamelCaseSupplierIdColumnName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new SupplierSystemDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(SupplierDocument));
        var storeObject = StoreObjectIdentifier.Table("supplier_documents", null);
        var supplierIdColumnName = entityType!
            .FindProperty(nameof(SupplierDocument.SupplierId))!
            .GetColumnName(storeObject);

        // Assert
        supplierIdColumnName.Should().Be("supplierId");
    }

    #endregion

    #region Helper Methods

    private static User CreateTestUser()
    {
        return new User
        {
            Id = "test-user-001",
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com",
            Role = "user",
            Password = "hashed_password",
            Status = "active",
            AccountType = "regular",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private static Supplier CreateTestSupplier()
    {
        return new Supplier
        {
            Id = 1,
            CompanyName = "Test Supplier",
            CompanyId = "SUP-001",
            Status = "active",
            Stage = "approved",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private static Rfq CreateTestRfq()
    {
        return new Rfq
        {
            Id = 1,
            Title = "Test RFQ",
            Description = "Test Description",
            Status = "draft",
            CreatedBy = "user-001",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private static Quote CreateTestQuote()
    {
        return new Quote
        {
            Id = 1L,
            RfqId = 1L,
            SupplierId = 1,
            Status = "submitted",
            UnitPrice = 100.00m,
            TotalAmount = 1000.00m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private static PurchaseOrder CreateTestPurchaseOrder()
    {
        return new PurchaseOrder
        {
            Id = 1,
            PoNumber = "PO-001",
            RfqId = 1,
            SupplierId = 1,
            TotalAmount = 1000.00m,
            CreatedBy = "user-001",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private static AuditLog CreateTestAuditLog()
    {
        return new AuditLog
        {
            Id = 1,
            ActorId = "user-001",
            ActorName = "Test User",
            EntityType = "test_entity",
            EntityId = "1",
            Action = "test_action",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ActiveSession CreateTestActiveSession()
    {
        return new ActiveSession
        {
            Id = 1,
            UserId = "user-001",
            TokenHash = "hash123",
            IssuedAt = DateTime.UtcNow.ToString("o"),
            ExpiresAt = DateTime.UtcNow.AddHours(8).ToString("o"),
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private static SystemConfig CreateTestSystemConfig()
    {
        return new SystemConfig
        {
            Key = "test_config_key",
            Value = "test_value"
        };
    }

    #endregion
}
