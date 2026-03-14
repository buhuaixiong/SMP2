using FluentAssertions;
using SupplierSystem.Api.Services;
using SupplierSystem.Domain.Entities;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for NotificationService
/// </summary>
public class NotificationServiceTests
{
    #region NotificationType Constants Tests

    [Fact]
    public void NotificationType_ShouldHaveCorrectValues()
    {
        // Assert
        NotificationType.DocumentExpiring.Should().Be("document_expiring");
        NotificationType.DocumentExpired.Should().Be("document_expired");
        NotificationType.DocumentRenewalReminder.Should().Be("document_renewal_reminder");
        NotificationType.ProfileIncomplete.Should().Be("profile_incomplete");
        NotificationType.RfqPendingProcessing.Should().Be("rfq_pending_processing");
    }

    #endregion

    #region NotificationPriority Constants Tests

    [Fact]
    public void NotificationPriority_ShouldHaveCorrectValues()
    {
        // Assert
        NotificationPriority.Low.Should().Be("low");
        NotificationPriority.Normal.Should().Be("normal");
        NotificationPriority.High.Should().Be("high");
        NotificationPriority.Urgent.Should().Be("urgent");
    }

    #endregion

    #region Notification Record Tests

    [Fact]
    public void Notification_CanBeCreated()
    {
        // Arrange & Act
        var notification = new Notification
        {
            Id = 1,
            SupplierId = 100,
            UserId = null,
            Type = NotificationType.DocumentExpiring,
            Title = "Document Expiring",
            Message = "Your document is about to expire",
            Priority = NotificationPriority.High,
            Status = "unread",
            RelatedEntityType = "document",
            RelatedEntityId = 123,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            ExpiresAt = null,
            Metadata = null
        };

        // Assert
        notification.Id.Should().Be(1);
        notification.SupplierId.Should().Be(100);
        notification.Type.Should().Be("document_expiring");
        notification.Priority.Should().Be("high");
        notification.Status.Should().Be("unread");
    }

    [Fact]
    public void Notification_WithUserId_ShouldWork()
    {
        // Arrange & Act
        var notification = new Notification
        {
            Id = 2,
            SupplierId = null,
            UserId = "user-123",
            Type = NotificationType.ProfileIncomplete,
            Title = "Profile Incomplete",
            Message = "Please complete your profile",
            Priority = NotificationPriority.Normal,
            Status = "unread"
        };

        // Assert
        notification.SupplierId.Should().BeNull();
        notification.UserId.Should().Be("user-123");
    }

    [Fact]
    public void Notification_WithMetadata_ShouldSerialize()
    {
        // Arrange & Act
        var notification = new Notification
        {
            Id = 3,
            Type = NotificationType.RfqPendingProcessing,
            Title = "RFQ Pending",
            Message = "You have a pending RFQ",
            Priority = NotificationPriority.Normal,
            Status = "unread",
            Metadata = "{\"rfqId\": 123}"
        };

        // Assert
        notification.Metadata.Should().NotBeNull();
        notification.Metadata.Should().Contain("rfqId");
    }

    [Fact]
    public void Notification_DefaultValues_ShouldBeNullOrEmpty()
    {
        // Arrange & Act
        var notification = new Notification();

        // Assert
        notification.Id.Should().Be(0);
        notification.SupplierId.Should().BeNull();
        notification.UserId.Should().BeNull();
        notification.Type.Should().BeNull();
        notification.Title.Should().BeNull();
        notification.Message.Should().BeNull();
        notification.Priority.Should().BeNull();
        notification.Status.Should().BeNull();
        notification.RelatedEntityType.Should().BeNull();
        notification.RelatedEntityId.Should().BeNull();
        notification.CreatedAt.Should().BeNull();
        notification.ExpiresAt.Should().BeNull();
        notification.Metadata.Should().BeNull();
    }

    #endregion

    #region Notification Property Tests

    [Fact]
    public void Notifications_WithSameId_ShouldHaveSameId()
    {
        // Arrange
        var notification1 = new Notification { Id = 1, Type = "test", Title = "Test" };
        var notification2 = new Notification { Id = 1, Type = "test", Title = "Test" };

        // Assert
        notification1.Id.Should().Be(notification2.Id);
    }

    [Fact]
    public void Notifications_WithDifferentIds_ShouldHaveDifferentIds()
    {
        // Arrange
        var notification1 = new Notification { Id = 1, Type = "test", Title = "Test" };
        var notification2 = new Notification { Id = 2, Type = "test", Title = "Test" };

        // Assert
        notification1.Id.Should().NotBe(notification2.Id);
    }

    #endregion
}
