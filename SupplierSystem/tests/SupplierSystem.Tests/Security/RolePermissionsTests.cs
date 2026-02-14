using FluentAssertions;
using SupplierSystem.Application.Security;
using Xunit;

namespace SupplierSystem.Tests.Security;

public class RolePermissionsTests
{
    [Fact]
    public void GetRoleKey_ReturnsNullForEmpty()
    {
        RolePermissions.GetRoleKey(null).Should().BeNull();
        RolePermissions.GetRoleKey("  ").Should().BeNull();
    }

    [Theory]
    [InlineData("financeDirector", "finance_director")]
    [InlineData("finance-director", "finance_director")]
    [InlineData("supplier", "formal_supplier")]
    public void GetRoleKey_ResolvesAliases(string role, string expected)
    {
        RolePermissions.GetRoleKey(role).Should().Be(expected);
    }

    [Fact]
    public void GetPermissionsForRole_ReturnsPermissionsForKnownRole()
    {
        var permissions = RolePermissions.GetPermissionsForRole("admin");

        permissions.Should().Contain(Permissions.AdminRoleManage);
    }

    [Fact]
    public void GetPermissionsForRole_ReturnsEmptyForUnknownRole()
    {
        RolePermissions.GetPermissionsForRole("unknown").Should().BeEmpty();
    }
}

