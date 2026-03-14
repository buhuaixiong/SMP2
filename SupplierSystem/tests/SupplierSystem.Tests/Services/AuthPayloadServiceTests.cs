using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

public class AuthPayloadServiceTests
{
    [Fact]
    public async Task BuildAsync_IncludesMustChangePasswordFlag()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new SupplierSystemDbContext(options);
        dbContext.Users.Add(new User
        {
            Id = "supplier-1",
            Name = "Supplier User",
            Username = "supplier-1",
            Password = "hashed",
            Role = "supplier",
            Status = "active",
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o")
        });
        await dbContext.SaveChangesAsync();

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = new LoggerFactory().CreateLogger<AuthPayloadService>();
        var service = new AuthPayloadService(dbContext, memoryCache, logger);

        var payload = await service.BuildAsync("supplier-1");

        payload.Should().NotBeNull();
        payload!.MustChangePassword.Should().BeTrue();
    }
}
