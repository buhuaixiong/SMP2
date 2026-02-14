using FluentAssertions;
using SupplierSystem.Application.Security;
using Xunit;

namespace SupplierSystem.Tests.Security;

public class DepartmentPermissionsTests
{
    [Fact]
    public void GetPermissionsByFunctions_MergesAndDeduplicates()
    {
        var permissions = DepartmentPermissions.GetPermissionsByFunctions(new[] { "finance", "Finance", "general" });

        permissions.Should().Contain("finance.invoice.audit");
        permissions.Should().Contain("supplier.view");
        permissions.Count(p => p == "supplier.view").Should().Be(1);
    }

    [Fact]
    public void GetPermissionsByFunctions_WhenUnknown_ReturnsEmpty()
    {
        DepartmentPermissions.GetPermissionsByFunctions(new[] { "unknown" }).Should().BeEmpty();
    }
}
