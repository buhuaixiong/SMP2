using FluentAssertions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for DocumentAccess
/// </summary>
public class DocumentAccessTests
{
    #region CanAccessDocuments Tests

    [Fact]
    public void CanAccessDocuments_WithNullUser_ShouldReturnFalse()
    {
        // Act
        var result = DocumentAccess.CanAccessDocuments(null, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccessDocuments_WithStaffPermission_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateUser(permissions: new[] { Permissions.PurchaserSegmentManage });

        // Act
        var result = DocumentAccess.CanAccessDocuments(user, 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccessDocuments_WithSupplierOwnerAndSupplierPermission_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateUser(supplierId: 1, permissions: new[] { Permissions.SupplierContractChecklist });

        // Act
        var result = DocumentAccess.CanAccessDocuments(user, 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccessDocuments_WithSupplierOwnerButNoSupplierPermission_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateUser(supplierId: 1, permissions: new[] { Permissions.RfqCreate });

        // Act
        var result = DocumentAccess.CanAccessDocuments(user, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccessDocuments_WithSupplierNonOwnerAndNoStaffPermission_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateUser(supplierId: 2, permissions: new[] { Permissions.SupplierContractChecklist });

        // Act
        var result = DocumentAccess.CanAccessDocuments(user, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(Permissions.ProcurementManagerRfqReview)]
    [InlineData(Permissions.ProcurementDirectorRfqApprove)]
    [InlineData(Permissions.ProcurementDirectorReportsView)]
    [InlineData(Permissions.FinanceAccountantReconciliation)]
    [InlineData(Permissions.FinanceDirectorRiskMonitor)]
    [InlineData(Permissions.AdminRoleManage)]
    [InlineData(Permissions.AdminSupplierTags)]
    public void CanAccessDocuments_WithVariousStaffPermissions_ShouldReturnTrue(string permission)
    {
        // Arrange
        var user = CreateUser(permissions: new[] { permission });

        // Act
        var result = DocumentAccess.CanAccessDocuments(user, 1);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CanUploadDocuments Tests

    [Fact]
    public void CanUploadDocuments_WithNullUser_ShouldReturnFalse()
    {
        // Act
        var result = DocumentAccess.CanUploadDocuments(null, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanUploadDocuments_WithStaffPermission_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateUser(permissions: new[] { Permissions.PurchaserSegmentManage });

        // Act
        var result = DocumentAccess.CanUploadDocuments(user, 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanUploadDocuments_WithSupplierOwnerAndUploadPermission_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateUser(supplierId: 1, permissions: new[] { Permissions.SupplierContractUpload });

        // Act
        var result = DocumentAccess.CanUploadDocuments(user, 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanUploadDocuments_WithSupplierOwnerButOnlyChecklistPermission_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateUser(supplierId: 1, permissions: new[] { Permissions.SupplierContractChecklist });

        // Act
        var result = DocumentAccess.CanUploadDocuments(user, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanUploadDocuments_WithSupplierNonOwner_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateUser(supplierId: 2, permissions: new[] { Permissions.SupplierContractUpload });

        // Act
        var result = DocumentAccess.CanUploadDocuments(user, 1);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CanAccessDocuments_WithEmptyPermissions_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateUser(permissions: Array.Empty<string>());

        // Act
        var result = DocumentAccess.CanAccessDocuments(user, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccessDocuments_WithNullPermissions_ShouldReturnFalse()
    {
        // Arrange
        var user = new AuthUser { Id = "1", Name = "Test", Permissions = null! };

        // Act
        var result = DocumentAccess.CanAccessDocuments(user, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanUploadDocuments_WithEmptyPermissions_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateUser(permissions: Array.Empty<string>());

        // Act
        var result = DocumentAccess.CanUploadDocuments(user, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccessDocuments_StaffPermissionEnablesAccess_ShouldReturnTrue()
    {
        // Arrange - using a permission that's in StaffPermissions
        var user = new AuthUser
        {
            Id = "1",
            Name = "Staff User",
            Permissions = new List<string> { Permissions.PurchaserSegmentManage }
        };

        // Act
        var result = DocumentAccess.CanAccessDocuments(user, 1);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static AuthUser CreateUser(
        string? userId = "user1",
        string? name = "Test User",
        int? supplierId = null,
        string role = "user",
        string[]? permissions = null)
    {
        return new AuthUser
        {
            Id = userId ?? "user1",
            Name = name ?? "Test User",
            SupplierId = supplierId,
            Role = role,
            Permissions = permissions?.ToList() ?? new List<string>()
        };
    }

    #endregion
}
