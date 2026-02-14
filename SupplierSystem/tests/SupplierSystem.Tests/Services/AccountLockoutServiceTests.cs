using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for AccountLockoutService
/// </summary>
public class AccountLockoutServiceTests
{
    private readonly AccountLockoutService _service;

    public AccountLockoutServiceTests()
    {
        var mockAudit = new Mock<IAuditService>();
        mockAudit.Setup(x => x.LogAsync(It.IsAny<AuditEntry>())).Returns(Task.CompletedTask);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<AccountLockoutService>>().Object;
        _service = new AccountLockoutService(cache, mockAudit.Object, logger);
    }

    [Fact]
    public void Check_WithNonExistentUser_ShouldReturnNotLocked()
    {
        // Act
        var result = _service.Check("nonexistent");

        // Assert
        result.Locked.Should().BeFalse();
        result.RemainingTimeMinutes.Should().Be(0);
        result.Attempts.Should().Be(0);
    }

    [Fact]
    public void Check_WithNullUsername_ShouldReturnNotLocked()
    {
        // Act
        var result = _service.Check(null!);

        // Assert
        result.Locked.Should().BeFalse();
        result.RemainingTimeMinutes.Should().Be(0);
    }

    [Fact]
    public void RecordFailedAttempt_FirstAttempt_ShouldNotLock()
    {
        // Act
        var result = _service.RecordFailedAttempt("testuser", "127.0.0.1");

        // Assert
        result.Locked.Should().BeFalse();
        result.RemainingAttempts.Should().Be(4);
    }

    [Fact]
    public void RecordFailedAttempt_After5Attempts_ShouldLock()
    {
        // Arrange
        var username = "lockeduser";

        // Act
        FailedAttemptResult? result = null;
        for (int i = 0; i < 5; i++)
        {
            result = _service.RecordFailedAttempt(username, "127.0.0.1");
        }

        // Assert
        result!.Locked.Should().BeTrue();
        result.RemainingAttempts.Should().Be(0);
        result.LockedUntil.Should().NotBeNull();
    }

    [Fact]
    public void RecordFailedAttempt_CaseInsensitive_ShouldTrackSameUser()
    {
        // Act - First attempt with different casing
        var result1 = _service.RecordFailedAttempt("testuser", "127.0.0.1");
        var result2 = _service.RecordFailedAttempt("TESTUSER", "127.0.0.1");
        var result3 = _service.RecordFailedAttempt("TestUser", "127.0.0.1");

        // Assert - Should be cumulative
        result1.RemainingAttempts.Should().Be(4);
        result2.RemainingAttempts.Should().Be(3);
        result3.RemainingAttempts.Should().Be(2);
    }

    [Fact]
    public void Reset_ShouldClearFailedAttempts()
    {
        // Arrange
        var username = "resetuser";
        for (int i = 0; i < 3; i++)
        {
            _service.RecordFailedAttempt(username, "127.0.0.1");
        }

        // Act
        _service.Reset(username);

        // Assert
        var checkResult = _service.Check(username);
        checkResult.Attempts.Should().Be(0);
        checkResult.Locked.Should().BeFalse();
    }

    [Fact]
    public void Unlock_WithLockedAccount_ShouldReturnTrue()
    {
        // Arrange
        var username = "tounlock";
        for (int i = 0; i < 5; i++)
        {
            _service.RecordFailedAttempt(username, "127.0.0.1");
        }

        // Act
        var result = _service.Unlock(username, "admin-user");

        // Assert
        result.Should().BeTrue();
        var checkResult = _service.Check(username);
        checkResult.Locked.Should().BeFalse();
    }

    [Fact]
    public void GetStats_ShouldReturnCorrectStatistics()
    {
        // Arrange
        _service.RecordFailedAttempt("user1", "127.0.0.1");
        _service.RecordFailedAttempt("user1", "127.0.0.1");
        _service.RecordFailedAttempt("user2", "127.0.0.1");
        for (int i = 0; i < 5; i++)
        {
            _service.RecordFailedAttempt("lockeduser", "127.0.0.1");
        }

        // Act
        var stats = _service.GetStats();

        // Assert
        stats.TotalAccounts.Should().Be(3);
        stats.CurrentlyLocked.Should().Be(1);
        stats.AccountsWithFailures.Should().Be(3);
        stats.MaxFailedAttempts.Should().Be(5);
        stats.LockoutDurationMinutes.Should().Be(30);
    }
}

/// <summary>
/// Unit tests for SessionService
/// </summary>
public class SessionServiceTests
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly Mock<ITokenBlacklistService> _mockTokenBlacklistService;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new SupplierSystemDbContext(options);

        _mockTokenBlacklistService = new Mock<ITokenBlacklistService>();
        _mockTokenBlacklistService.Setup(x => x.AddAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockTokenBlacklistService.Setup(x => x.AddTokenHashAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockAuditService = new Mock<IAuditService>();
        _mockAuditService.Setup(x => x.LogAsync(It.IsAny<AuditEntry>())).Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<SessionService>>().Object;
        _service = new SessionService(_dbContext, _mockTokenBlacklistService.Object, _mockAuditService.Object, logger);
    }

    [Fact]
    public async Task RegisterAsync_WithValidToken_ShouldCreateSession()
    {
        // Arrange
        var token = CreateValidTestToken();

        // Act
        await _service.RegisterAsync(token, "user-001", "127.0.0.1", "Test-Agent");

        // Assert
        var sessions = await _dbContext.ActiveSessions.ToListAsync();
        sessions.Should().HaveCount(1);
        sessions[0].UserId.Should().Be("user-001");
    }

    [Fact]
    public async Task RegisterAsync_WithEmptyToken_ShouldNotCreateSession()
    {
        // Act
        await _service.RegisterAsync("", "user-001", "127.0.0.1", "Test-Agent");

        // Assert
        var sessions = await _dbContext.ActiveSessions.ToListAsync();
        sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task CountActiveAsync_WithActiveSession_ShouldReturnCount()
    {
        // Arrange
        var token = CreateValidTestToken();
        await _service.RegisterAsync(token, "user-001", "127.0.0.1", "Test-Agent");

        // Act
        var count = await _service.CountActiveAsync("user-001");

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_WithExistingSession_ShouldReturnTrue()
    {
        // Arrange
        var token = CreateValidTestToken();
        await _service.RegisterAsync(token, "user-001", "127.0.0.1", "Test-Agent");

        // Act
        var result = await _service.RemoveAsync(token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HashToken_ShouldReturnConsistentHash()
    {
        // Arrange
        var token = "test-token";

        // Act
        var hash1 = _service.HashToken(token);
        var hash2 = _service.HashToken(token);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
    }

    [Fact]
    public async Task InvalidateUserSessionsAsync_WithDifferentIpAndUa_ShouldInvalidate()
    {
        // Arrange
        var token = CreateValidTestToken();
        await _service.RegisterAsync(token, "user-001", "192.168.1.1", "Old-Agent");

        // Act
        var result = await _service.InvalidateUserSessionsAsync(
            "user-001", "test", "192.168.1.2", "New-Agent");

        // Assert
        result.Invalidated.Should().Be(1);
    }

    private static string CreateValidTestToken()
    {
        return "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJleHAiOjk5OTk5OTk5OTl9.test-signature";
    }
}

/// <summary>
/// Unit tests for LoginLockService
/// </summary>
public class LoginLockServiceTests
{
    private readonly LoginLockService _service;

    public LoginLockServiceTests()
    {
        var logger = new Mock<ILogger<LoginLockService>>().Object;
        _service = new LoginLockService(logger);
    }

    [Fact]
    public void TryAcquire_WithNullUserId_ShouldReturnTrue()
    {
        // Act - Null user ID should always return true (no lock needed)
        var result = _service.TryAcquire(null!);

        // Assert
        result.Should().BeTrue();
    }
}
