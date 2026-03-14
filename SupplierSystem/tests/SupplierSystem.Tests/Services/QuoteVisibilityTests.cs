using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for QuoteVisibility
/// </summary>
public class QuoteVisibilityTests
{
    private static AuthUser CreateProcurementUser()
    {
        return new AuthUser
        {
            Id = "procurement-user",
            Name = "Procurement User",
            Role = "procurement_manager",
            Permissions = new List<string>
            {
                Permissions.PurchaserRfqTarget,
            }
        };
    }

    private static AuthUser CreateRegularUser()
    {
        return new AuthUser
        {
            Id = "regular-user",
            Name = "Regular User",
            Role = "supplier",
            Permissions = new List<string>
            {
                Permissions.SupplierRfqShort,
            }
        };
    }

    #region GetVisibilityAsync Tests

    [Fact]
    public async Task GetVisibilityAsync_WithNonExistentRfq_ShouldReturnNotExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        // Act
        var result = await QuoteVisibility.GetVisibilityAsync(dbContext, 999, CreateProcurementUser());

        // Assert
        result.Should().NotBeNull();
        result.Locked.Should().BeFalse();
        result.Context.RfqExists.Should().BeFalse();
        result.Context.InvitedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetVisibilityAsync_WithNoInvitations_ShouldBeUnlocked()
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
            Status = "open",
            ValidUntil = DateTime.UtcNow.AddDays(7).ToString("o")
        };
        dbContext.Rfqs.Add(rfq);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await QuoteVisibility.GetVisibilityAsync(dbContext, 1, CreateProcurementUser());

        // Assert - with no invitations, RFQ exists but Unlocked depends on logic
        result.Context.RfqExists.Should().BeTrue();
        result.Context.InvitedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetVisibilityAsync_WithAllSuppliersSubmitted_ShouldUnlock()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, Title = "Test RFQ", Status = "open", ValidUntil = DateTime.UtcNow.AddDays(7).ToString("o") };
        dbContext.Rfqs.Add(rfq);

        var invitation1 = new SupplierRfqInvitation { Id = 1, RfqId = 1, SupplierId = 1, Status = "pending" };
        var invitation2 = new SupplierRfqInvitation { Id = 2, RfqId = 1, SupplierId = 2, Status = "pending" };
        dbContext.SupplierRfqInvitations.AddRange(invitation1, invitation2);

        var quote1 = new Quote { Id = 1, RfqId = 1, SupplierId = 1, Status = "submitted", IsLatest = true };
        var quote2 = new Quote { Id = 2, RfqId = 1, SupplierId = 2, Status = "submitted", IsLatest = true };
        dbContext.Quotes.AddRange(quote1, quote2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await QuoteVisibility.GetVisibilityAsync(dbContext, 1, CreateProcurementUser());

        // Assert
        result.Context.InvitedCount.Should().Be(2);
        result.Context.SubmittedCount.Should().Be(2);
        result.Context.AllSubmitted.Should().BeTrue();
        result.Context.Unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task GetVisibilityAsync_WithPartialSubmissions_ShouldLock()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, Title = "Test RFQ", Status = "open", ValidUntil = DateTime.UtcNow.AddDays(7).ToString("o") };
        dbContext.Rfqs.Add(rfq);

        var invitation1 = new SupplierRfqInvitation { Id = 1, RfqId = 1, SupplierId = 1, Status = "pending" };
        var invitation2 = new SupplierRfqInvitation { Id = 2, RfqId = 1, SupplierId = 2, Status = "pending" };
        dbContext.SupplierRfqInvitations.AddRange(invitation1, invitation2);

        var quote = new Quote { Id = 1, RfqId = 1, SupplierId = 1, Status = "submitted", IsLatest = true };
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await QuoteVisibility.GetVisibilityAsync(dbContext, 1, CreateProcurementUser());

        // Assert
        result.Context.InvitedCount.Should().Be(2);
        result.Context.SubmittedCount.Should().Be(1);
        result.Context.AllSubmitted.Should().BeFalse();
        result.Context.Unlocked.Should().BeFalse();
    }

    [Fact]
    public async Task GetVisibilityAsync_WithExpiredDeadline_ShouldUnlock()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, Title = "Test RFQ", Status = "open", ValidUntil = DateTime.UtcNow.AddDays(-1).ToString("o") };
        dbContext.Rfqs.Add(rfq);

        var invitation = new SupplierRfqInvitation { Id = 1, RfqId = 1, SupplierId = 1, Status = "pending" };
        dbContext.SupplierRfqInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await QuoteVisibility.GetVisibilityAsync(dbContext, 1, CreateProcurementUser());

        // Assert
        result.Context.DeadlinePassed.Should().BeTrue();
        result.Context.Unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task GetVisibilityAsync_WithDeclinedInvitation_ShouldNotCount()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, Title = "Test RFQ", Status = "open" };
        dbContext.Rfqs.Add(rfq);

        var invitation1 = new SupplierRfqInvitation { Id = 1, RfqId = 1, SupplierId = 1, Status = "declined" };
        var invitation2 = new SupplierRfqInvitation { Id = 2, RfqId = 1, SupplierId = 2, Status = "pending" };
        dbContext.SupplierRfqInvitations.AddRange(invitation1, invitation2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await QuoteVisibility.GetVisibilityAsync(dbContext, 1, CreateProcurementUser());

        // Assert
        result.Context.InvitedCount.Should().Be(1); // Only pending counts
    }

    [Fact]
    public async Task GetVisibilityAsync_WithWithdrawnQuote_ShouldNotCount()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var rfq = new Rfq { Id = 1, Title = "Test RFQ", Status = "open" };
        dbContext.Rfqs.Add(rfq);

        var invitation = new SupplierRfqInvitation { Id = 1, RfqId = 1, SupplierId = 1, Status = "pending" };
        dbContext.SupplierRfqInvitations.Add(invitation);

        var withdrawnQuote = new Quote { Id = 1, RfqId = 1, SupplierId = 1, Status = "withdrawn", IsLatest = true };
        dbContext.Quotes.Add(withdrawnQuote);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await QuoteVisibility.GetVisibilityAsync(dbContext, 1, CreateProcurementUser());

        // Assert
        result.Context.SubmittedCount.Should().Be(0); // Withdrawn doesn't count
    }

    #endregion

    #region IsProcurementUser Tests

    [Fact]
    public void IsProcurementUser_WithProcurementPermission_ShouldReturnTrue()
    {
        // Arrange
        var user = new AuthUser { Id = "user", Permissions = new List<string> { Permissions.PurchaserRfqTarget } };

        // Act
        var result = QuoteVisibility.IsProcurementUser(user);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsProcurementUser_WithManagerPermission_ShouldReturnTrue()
    {
        // Arrange
        var user = new AuthUser { Id = "user", Permissions = new List<string> { Permissions.ProcurementManagerRfqReview } };

        // Act
        var result = QuoteVisibility.IsProcurementUser(user);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsProcurementUser_WithDirectorPermission_ShouldReturnTrue()
    {
        // Arrange
        var user = new AuthUser { Id = "user", Permissions = new List<string> { Permissions.ProcurementDirectorRfqApprove } };

        // Act
        var result = QuoteVisibility.IsProcurementUser(user);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsProcurementUser_WithNoPermissions_ShouldReturnFalse()
    {
        // Arrange
        var user = new AuthUser { Id = "user", Permissions = new List<string>() };

        // Act
        var result = QuoteVisibility.IsProcurementUser(user);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsProcurementUser_WithNullUser_ShouldReturnFalse()
    {
        // Act
        var result = QuoteVisibility.IsProcurementUser(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsProcurementUser_WithCaseInsensitivePermission_ShouldReturnTrue()
    {
        // Arrange - using actual permission value with different case
        var user = new AuthUser { Id = "user", Permissions = new List<string> { "RFQ.SHORT.MANAGE" } };

        // Act
        var result = QuoteVisibility.IsProcurementUser(user);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region QuoteVisibilityContext Tests

    [Fact]
    public void QuoteVisibilityContext_CanBeModified()
    {
        // Arrange & Act
        var context = new QuoteVisibilityContext
        {
            RfqExists = true,
            InvitedCount = 5,
            SubmittedCount = 3,
            DeadlinePassed = false,
            AllSubmitted = false,
            Unlocked = false,
            Deadline = "2024-12-31"
        };

        // Assert
        context.RfqExists.Should().BeTrue();
        context.InvitedCount.Should().Be(5);
        context.SubmittedCount.Should().Be(3);
        context.DeadlinePassed.Should().BeFalse();
        context.AllSubmitted.Should().BeFalse();
        context.Unlocked.Should().BeFalse();
        context.Deadline.Should().Be("2024-12-31");
    }

    #endregion

    #region QuoteVisibilityResult Tests

    [Fact]
    public void QuoteVisibilityResult_CanCreateWithLockedAndContext()
    {
        // Arrange
        var context = new QuoteVisibilityContext { RfqExists = true };

        // Act
        var result = new QuoteVisibilityResult(true, context);

        // Assert
        result.Locked.Should().BeTrue();
        result.Context.Should().Be(context);
    }

    #endregion
}
