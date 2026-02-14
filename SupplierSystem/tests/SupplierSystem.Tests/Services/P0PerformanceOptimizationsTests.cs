using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

public class P0PerformanceOptimizationsTests
{
    [Fact]
    public async Task TokenBlacklistService_ShouldNotReturnDeletedTokenReason_WhenTokenWasNeverQueried()
    {
        var dbName = $"p0-token-blacklist-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var context = new SupplierSystemDbContext(options);
        var future = DateTimeOffset.UtcNow.AddHours(1).ToString("o");
        var token1 = "token-one";
        var token2 = "token-two";

        context.TokenBlacklist.AddRange(
            new TokenBlacklistEntry
            {
                TokenHash = HashToken(token1),
                UserId = "u1",
                BlacklistedAt = DateTimeOffset.UtcNow.ToString("o"),
                ExpiresAt = future,
                Reason = "r1"
            },
            new TokenBlacklistEntry
            {
                TokenHash = HashToken(token2),
                UserId = "u2",
                BlacklistedAt = DateTimeOffset.UtcNow.ToString("o"),
                ExpiresAt = future,
                Reason = "r2"
            });
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddDbContext<SupplierSystemDbContext>(o => o.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var service = new TokenBlacklistService(scopeFactory, NullLogger<TokenBlacklistService>.Instance);

        var reason1 = await service.GetReasonAsync(token1);
        reason1.Should().Be("r1");

        var token2Entry = await context.TokenBlacklist.FirstAsync(x => x.TokenHash == HashToken(token2));
        context.TokenBlacklist.Remove(token2Entry);
        await context.SaveChangesAsync();

        var reason2 = await service.GetReasonAsync(token2);
        reason2.Should().BeNull();
    }

    [Fact]
    public void Program_ShouldRegisterTokenBlacklistService_AsSingleton()
    {
        var program = ReadProgramFile();

        program.Should().Contain("AddSingleton<ITokenBlacklistService, TokenBlacklistService>()");
    }

    [Fact]
    public void Program_ShouldRegisterRateLimitService_AsSingleton()
    {
        var program = ReadProgramFile();

        program.Should().Contain("AddSingleton<IRateLimitService, RateLimitService>()");
    }

    [Fact]
    public void SqlScripts_ShouldContainTokenHashIndexes_ForSessionAndBlacklistTables()
    {
        var sqlDirectory = Path.Combine(GetSupplierSystemRoot(), "sql");
        var sqlFiles = Directory.GetFiles(sqlDirectory, "*.sql", SearchOption.TopDirectoryOnly);
        var allSqlText = string.Join("\n", sqlFiles.Select(File.ReadAllText));

        allSqlText.Should().Contain("IX_token_blacklist_token_hash");
        allSqlText.Should().Contain("IX_active_sessions_token_hash");
    }

    private static string ReadProgramFile()
    {
        var programPath = Path.Combine(GetSupplierSystemRoot(), "src", "SupplierSystem.Api", "Program.cs");
        return File.ReadAllText(programPath);
    }

    private static string GetSupplierSystemRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "SupplierSystem.sln");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate SupplierSystem root directory.");
    }

    private static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

}
