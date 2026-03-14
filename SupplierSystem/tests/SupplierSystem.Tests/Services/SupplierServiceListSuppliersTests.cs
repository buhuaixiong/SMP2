using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

public class SupplierServiceListSuppliersTests
{
    [Fact]
    public async Task ListSuppliersAsync_PurchaserWithoutForRfq_ShouldRestrictToAssignedSuppliers()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var dbContext = new SupplierSystemDbContext(options);
        dbContext.Suppliers.AddRange(
            new Supplier { Id = 1, CompanyName = "Supplier A", CompanyId = "A001" },
            new Supplier { Id = 2, CompanyName = "Supplier B", CompanyId = "B001" });
        dbContext.BuyerSupplierAssignments.Add(new BuyerSupplierAssignment
        {
            BuyerId = "buyer-1",
            SupplierId = 1,
        });
        await dbContext.SaveChangesAsync();

        var service = new SupplierService(
            dbContext,
            Mock.Of<IAuditService>(),
            NullLogger<SupplierService>.Instance);

        var user = new AuthUser
        {
            Id = "buyer-1",
            Role = "purchaser",
        };

        var result = await service.ListSuppliersAsync(new SupplierListQuery { ForRfq = false }, user, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Suppliers.Should().ContainSingle(s => s.Id == 1);
    }

    [Fact]
    public async Task ListSuppliersAsync_PurchaserWithForRfq_ShouldReturnAllSuppliers()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var dbContext = new SupplierSystemDbContext(options);
        dbContext.Suppliers.AddRange(
            new Supplier { Id = 1, CompanyName = "Supplier A", CompanyId = "A001" },
            new Supplier { Id = 2, CompanyName = "Supplier B", CompanyId = "B001" });
        dbContext.BuyerSupplierAssignments.Add(new BuyerSupplierAssignment
        {
            BuyerId = "buyer-1",
            SupplierId = 1,
        });
        await dbContext.SaveChangesAsync();

        var service = new SupplierService(
            dbContext,
            Mock.Of<IAuditService>(),
            NullLogger<SupplierService>.Instance);

        var user = new AuthUser
        {
            Id = "buyer-1",
            Role = "purchaser",
        };

        var result = await service.ListSuppliersAsync(new SupplierListQuery { ForRfq = true }, user, CancellationToken.None);

        result.Total.Should().Be(2);
        result.Suppliers.Should().HaveCount(2);
        result.Suppliers.Select(s => s.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }
}

