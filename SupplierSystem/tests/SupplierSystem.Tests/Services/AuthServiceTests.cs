using BCrypt.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Repositories;
using SupplierSystem.Infrastructure.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for AuthService
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<ITokenBlacklistService> _mockTokenBlacklistService;
    private readonly Mock<ILoginLockService> _mockLoginLockService;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IAccountLockoutService> _mockAccountLockoutService;
    private readonly Mock<IAuthPayloadService> _mockAuthPayloadService;
    private readonly Mock<IRateLimitService> _mockRateLimitService;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly IConfiguration _mockConfiguration;
    private readonly SupplierSystemDbContext _dbContext;

    public AuthServiceTests()
    {
        _mockAuditService = CreateMockAuditService();
        _mockTokenBlacklistService = CreateMockTokenBlacklistService();
        _mockLoginLockService = CreateMockLoginLockService();
        _mockSessionService = CreateMockSessionService();
        _mockAccountLockoutService = CreateMockAccountLockoutService();
        _mockAuthPayloadService = CreateMockAuthPayloadService();
        _mockRateLimitService = CreateMockRateLimitService();
        _mockHostEnvironment = CreateMockHostEnvironment();

        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new SupplierSystemDbContext(options);

        _mockConfiguration = CreateMockConfiguration();
    }

    private static Mock<IAuditService> CreateMockAuditService()
    {
        var mock = new Mock<IAuditService>();
        mock.Setup(x => x.LogAsync(It.IsAny<AuditEntry>())).Returns(Task.CompletedTask);
        return mock;
    }

    private static Mock<ITokenBlacklistService> CreateMockTokenBlacklistService()
    {
        var mock = new Mock<ITokenBlacklistService>();
        mock.Setup(x => x.AddAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mock.Setup(x => x.IsBlacklistedAsync(It.IsAny<string>())).ReturnsAsync(false);
        return mock;
    }

    private static Mock<ILoginLockService> CreateMockLoginLockService()
    {
        var mock = new Mock<ILoginLockService>();
        mock.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(true);
        mock.Setup(x => x.Release(It.IsAny<string>())).Verifiable();
        return mock;
    }

    private static Mock<ISessionService> CreateMockSessionService()
    {
        var mock = new Mock<ISessionService>();
        mock.Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.RemoveAsync(It.IsAny<string>())).ReturnsAsync(true);
        mock.Setup(x => x.InvalidateUserSessionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new SessionInvalidationResult { Invalidated = 0, Total = 1 });
        mock.Setup(x => x.CountActiveAsync(It.IsAny<string>())).ReturnsAsync(1);
        return mock;
    }

    private static Mock<IAccountLockoutService> CreateMockAccountLockoutService()
    {
        var mock = new Mock<IAccountLockoutService>();
        mock.Setup(x => x.Check(It.IsAny<string>())).Returns(new AccountLockoutStatus { Locked = false });
        mock.Setup(x => x.RecordFailedAttempt(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new FailedAttemptResult { Locked = false, RemainingAttempts = 4 });
        return mock;
    }

    private static Mock<IAuthPayloadService> CreateMockAuthPayloadService()
    {
        var mock = new Mock<IAuthPayloadService>();
        mock.Setup(x => x.BuildAsync(It.IsAny<string>()))
            .ReturnsAsync((string userId) =>
            {
                if (string.IsNullOrEmpty(userId))
                    return null!;
                return new AuthUser
                {
                    Id = userId,
                    Name = "Test User",
                    Role = "admin"
                };
            });
        return mock;
    }

    private static Mock<IRateLimitService> CreateMockRateLimitService()
    {
        var mock = new Mock<IRateLimitService>();
        mock.Setup(x => x.ShouldBlock(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        mock.Setup(x => x.RecordRequest(It.IsAny<string>(), It.IsAny<string>()));
        return mock;
    }

    private static Mock<IHostEnvironment> CreateMockHostEnvironment()
    {
        var mock = new Mock<IHostEnvironment>();
        mock.Setup(x => x.EnvironmentName).Returns(Environments.Development);
        return mock;
    }

    private static IConfiguration CreateMockConfiguration()
    {
        var config = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", "ThisIsAVeryLongSecretKeyForTestingPurposes12345" },
            { "JwtSettings:Issuer", "TestIssuer" },
            { "JwtSettings:Audience", "TestAudience" },
            { "JwtSettings:ExpiresIn", "8h" }
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(config!)
            .Build();
    }

    private static User CreateTestUser(string id, string username, string password, string role, string status = "active")
    {
        return new User
        {
            Id = id,
            Username = username,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            Status = status,
            Name = "Test User",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o")
        };
    }

    private AuthService CreateAuthService()
    {
        var logger = new Mock<ILogger<AuthService>>().Object;
        return new AuthService(
            _dbContext,
            new AuthRepository(_dbContext),
            _mockAuthPayloadService.Object,
            _mockTokenBlacklistService.Object,
            _mockAccountLockoutService.Object,
            _mockLoginLockService.Object,
            _mockSessionService.Object,
            _mockAuditService.Object,
            _mockRateLimitService.Object,
            _mockConfiguration,
            _mockHostEnvironment.Object,
            logger);
    }

    #region Login Tests

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-001",
            username: "testuser",
            password: "password123",
            role: "admin");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var service = CreateAuthService();
        var request = new LoginRequest { Username = "testuser", Password = "password123" };

        // Act
        var result = await service.LoginAsync(request, "127.0.0.1", "Test-Agent");

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Id.Should().Be("user-001");
    }

    [Fact]
    public async Task LoginAsync_WithEmptyUsername_ShouldThrowException()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new LoginRequest { Username = "", Password = "password123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.LoginAsync(request, "127.0.0.1", "Test-Agent"));

        exception.Status.Should().Be(400);
    }

    [Fact]
    public async Task LoginAsync_WithEmptyPassword_ShouldThrowException()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new LoginRequest { Username = "testuser", Password = "" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.LoginAsync(request, "127.0.0.1", "Test-Agent"));

        exception.Status.Should().Be(400);
    }

    [Fact]
    public async Task LoginAsync_WithNullUsername_ShouldThrowException()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new LoginRequest { Username = null!, Password = "password123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.LoginAsync(request, "127.0.0.1", "Test-Agent"));

        exception.Status.Should().Be(400);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ShouldReturn401()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new LoginRequest { Username = "nonexistent", Password = "password123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.LoginAsync(request, "127.0.0.1", "Test-Agent"));

        exception.Status.Should().Be(401);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ShouldReturn401()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-002",
            username: "testuser",
            password: "password123",
            role: "admin");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var service = CreateAuthService();
        var request = new LoginRequest { Username = "testuser", Password = "wrongpassword" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.LoginAsync(request, "127.0.0.1", "Test-Agent"));

        exception.Status.Should().Be(401);
    }

    [Fact]
    public async Task LoginAsync_WithLockedAccount_ShouldReturn423()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-003",
            username: "lockeduser",
            password: "password123",
            role: "admin");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Configure mock to return locked status
        _mockAccountLockoutService
            .Setup(x => x.Check(It.IsAny<string>()))
            .Returns(new AccountLockoutStatus
            {
                Locked = true,
                RemainingTimeMinutes = 30,
                Attempts = 5
            });

        var service = CreateAuthService();
        var request = new LoginRequest { Username = "lockeduser", Password = "password123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.LoginAsync(request, "127.0.0.1", "Test-Agent"));

        exception.Status.Should().Be(423);
    }

    [Fact]
    public async Task LoginAsync_WithFrozenAccount_ShouldReturn403()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-004",
            username: "frozenuser",
            password: "password123",
            role: "admin",
            status: "frozen");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var service = CreateAuthService();
        var request = new LoginRequest { Username = "frozenuser", Password = "password123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.LoginAsync(request, "127.0.0.1", "Test-Agent"));

        exception.Status.Should().Be(403);
    }

    [Fact]
    public async Task LoginAsync_WithDeletedAccount_ShouldReturn403()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-005",
            username: "deleteduser",
            password: "password123",
            role: "admin",
            status: "deleted");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var service = CreateAuthService();
        var request = new LoginRequest { Username = "deleteduser", Password = "password123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.LoginAsync(request, "127.0.0.1", "Test-Agent"));

        exception.Status.Should().Be(403);
    }

    [Theory]
    [InlineData("testuser")]
    [InlineData("TESTUSER")]
    [InlineData("TestUser")]
    public async Task LoginAsync_WithCaseInsensitiveUsername_ShouldSucceed(string username)
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-009",
            username: "testuser",
            password: "password123",
            role: "admin");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var service = CreateAuthService();
        var request = new LoginRequest { Username = username, Password = "password123" };

        // Act
        var result = await service.LoginAsync(request, "127.0.0.1", "Test-Agent");

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ChangePassword Tests

    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_ShouldSucceed()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-010",
            username: "pwduser",
            password: "oldpassword",
            role: "admin");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var service = CreateAuthService();
        var request = new ChangePasswordRequest { CurrentPassword = "oldpassword", NewPassword = "newpassword123" };

        // Act
        await service.ChangePasswordAsync("user-010", request);

        // Assert
        var updatedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == "user-010");
        updatedUser.Should().NotBeNull();
        updatedUser!.Password.StartsWith("$argon2id$", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidCurrentPassword_ShouldThrow401()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-011",
            username: "pwduser2",
            password: "oldpassword",
            role: "admin");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var service = CreateAuthService();
        var request = new ChangePasswordRequest { CurrentPassword = "wrongpassword", NewPassword = "newpassword123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.ChangePasswordAsync("user-011", request));

        exception.Status.Should().Be(401);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithShortNewPassword_ShouldThrow400()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-012",
            username: "pwduser3",
            password: "oldpassword",
            role: "admin");

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var service = CreateAuthService();
        var request = new ChangePasswordRequest { CurrentPassword = "oldpassword", NewPassword = "short" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.ChangePasswordAsync("user-012", request));

        exception.Status.Should().Be(400);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithNonExistentUser_ShouldThrow404()
    {
        // Arrange
        var service = CreateAuthService();
        var request = new ChangePasswordRequest { CurrentPassword = "oldpassword", NewPassword = "newpassword123" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            service.ChangePasswordAsync("non-existent-user", request));

        exception.Status.Should().Be(404);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task LogoutAsync_WithValidToken_ShouldBlacklistToken()
    {
        // Arrange
        var service = CreateAuthService();
        var authUser = new AuthUser { Id = "user-013", Name = "Test User", Role = "admin" };
        var token = "valid-token";

        // Act
        await service.LogoutAsync(token, authUser);

        // Assert
        _mockTokenBlacklistService.Verify(
            x => x.AddAsync(token, "user-013", "logout"),
            Times.Once);
        _mockSessionService.Verify(
            x => x.RemoveAsync(token),
            Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_WithEmptyToken_ShouldNotBlacklist()
    {
        // Arrange
        var service = CreateAuthService();
        var authUser = new AuthUser { Id = "user-014", Name = "Test User", Role = "admin" };

        // Act
        await service.LogoutAsync("", authUser);

        // Assert
        _mockTokenBlacklistService.Verify(
            x => x.AddAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region GetCurrentUser Tests

    [Fact]
    public async Task GetCurrentUserAsync_WithValidUserId_ShouldReturnUser()
    {
        // Arrange
        var user = CreateTestUser(
            id: "user-016",
            username: "currentuser",
            password: "password123",
            role: "admin");

        _mockAuthPayloadService
            .Setup(x => x.BuildAsync("user-016"))
            .ReturnsAsync(new AuthUser { Id = "user-016", Name = "Test User", Role = "admin" });

        var service = CreateAuthService();

        // Act
        var result = await service.GetCurrentUserAsync("user-016");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("user-016");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithNullUserId_ShouldReturnNull()
    {
        // Arrange
        var service = CreateAuthService();

        // Act
        var result = await service.GetCurrentUserAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
