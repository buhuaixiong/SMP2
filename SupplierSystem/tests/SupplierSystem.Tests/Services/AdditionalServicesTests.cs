using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Services;

/// <summary>
/// Unit tests for additional services
/// </summary>
public class AdditionalServicesTests
{
    #region ExchangeRateService Tests

    [Fact]
    public void GetExchangeRate_WithValidCurrency_ShouldReturnRate()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var rate = service.GetExchangeRate("USD");

        // Assert
        rate.Should().NotBeNull();
        rate.Should().Be(1.0m);
    }

    [Fact]
    public void GetExchangeRate_WithRmb_ShouldReturnCorrectRate()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var rate = service.GetExchangeRate("RMB");

        // Assert
        rate.Should().NotBeNull();
        rate.Should().BeApproximately(0.1388889m, 0.0001m);
    }

    [Fact]
    public void GetExchangeRate_WithNullCurrency_ShouldReturnNull()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var rate = service.GetExchangeRate(null);

        // Assert
        rate.Should().BeNull();
    }

    [Fact]
    public void GetExchangeRate_WithEmptyCurrency_ShouldReturnNull()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var rate = service.GetExchangeRate("");

        // Assert
        rate.Should().BeNull();
    }

    [Fact]
    public void GetExchangeRate_WithInvalidCurrency_ShouldReturnNull()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var rate = service.GetExchangeRate("INVALID");

        // Assert
        rate.Should().BeNull();
    }

    [Fact]
    public void GetExchangeRate_WithSpecificYear_ShouldReturnRate()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var rate = service.GetExchangeRate("USD", 2025);

        // Assert
        rate.Should().NotBeNull();
        rate.Should().Be(1.0m);
    }

    [Fact]
    public void ConvertToUsd_WithValidAmount_ShouldConvert()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var usd = service.ConvertToUsd(1000m, "USD");

        // Assert
        usd.Should().NotBeNull();
        usd.Should().Be(1000m);
    }

    [Fact]
    public void ConvertToUsd_WithRmb_ShouldConvert()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var usd = service.ConvertToUsd(7200m, "RMB");

        // Assert
        usd.Should().NotBeNull();
        usd.Should().BeApproximately(1000m, 1m); // 7200 * 0.1388889 ≈ 1000
    }

    [Fact]
    public void ConvertToUsd_WithNullCurrency_ShouldReturnNull()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var usd = service.ConvertToUsd(1000m, null);

        // Assert
        usd.Should().BeNull();
    }

    [Fact]
    public void GetAvailableCurrencies_ShouldReturnCurrencies()
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var currencies = service.GetAvailableCurrencies();

        // Assert
        currencies.Should().NotBeEmpty();
        currencies.Should().Contain("USD");
        currencies.Should().Contain("CNY");
        currencies.Should().Contain("EUR");
    }

    [Theory]
    [InlineData("CNY", "CNY")]
    [InlineData("RMB", "CNY")]
    [InlineData("CN¥", "CNY")]
    [InlineData("USDT", "USD")]
    [InlineData("usd", "USD")]
    [InlineData("eur", "EUR")]
    public void GetExchangeRate_ShouldNormalizeCurrencyCodes(string input, string expected)
    {
        // Arrange
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var mockLogger = new Mock<ILogger<ExchangeRateService>>();
        var service = new ExchangeRateService(mockEnv.Object, mockLogger.Object);

        // Act
        var rate = service.GetExchangeRate(input);

        // Assert
        rate.Should().NotBeNull();
        // Verify that the rate matches the expected currency's rate
        var expectedRate = service.GetExchangeRate(expected);
        rate.Should().Be(expectedRate);
    }

    #endregion

    #region TariffCalculationService Tests

    private static SupplierSystemDbContext CreateSqlServerDbContext()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseSqlServer(ResolveSqlServerConnectionString())
            .Options;
        return new SupplierSystemDbContext(options);
    }

    private static string ResolveSqlServerConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("SUPPLIER_SYSTEM_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__SupplierSystem");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            connectionString = TryReadConnectionString(Path.Combine(
                current.FullName, "tests", "SupplierSystem.Tests", "appsettings.Test.json"));
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            var appSettingsPath = Path.Combine(
                current.FullName, "src", "SupplierSystem.Api", "appsettings.json");
            connectionString = TryReadConnectionString(appSettingsPath);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("SQL Server connection string not configured for tests.");
    }

    private static string? TryReadConnectionString(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var config = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false)
            .Build();
        return config.GetConnectionString("SupplierSystem");
    }

    [Fact]
    public async Task CalculateStandardCostAsync_WithInvalidPrice_ShouldReturnError()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act
        var result = await service.CalculateStandardCostAsync(
            0m, // Invalid price
            "CN",
            "OTHERS",
            "US",
            "HZ",
            "FOB",
            "USD");

        // Assert
        result.Should().NotBeNull();
        result.Error.Should().Be("Invalid original price");
        result.OriginalPrice.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateStandardCostAsync_WithNegativePrice_ShouldReturnError()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act
        var result = await service.CalculateStandardCostAsync(
            -100m,
            "CN",
            "OTHERS",
            "US",
            "HZ",
            "FOB",
            "USD");

        // Assert
        result.Error.Should().Be("Invalid original price");
    }

    [Fact]
    public async Task CalculateStandardCostAsync_WithValidInput_ShouldCalculate()
    {
        // Arrange
        using var dbContext = CreateSqlServerDbContext();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act
        var result = await service.CalculateStandardCostAsync(
            1000m,
            "CN",
            "OTHERS",
            "US",
            "HZ",
            "FOB",
            "USD");

        // Assert
        result.Should().NotBeNull();
        result.OriginalPrice.Should().Be(1000m);
        result.OriginalCurrency.Should().Be("USD");
        result.StandardCostLocal.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task CalculateStandardCostAsync_WithDdpTerms_ShouldIncludeSpecialTariff()
    {
        // Arrange
        using var dbContext = CreateSqlServerDbContext();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act
        var result = await service.CalculateStandardCostAsync(
            1000m,
            "CN",
            "OTHERS",
            "US", // US origin triggers special tariff
            "HZ",
            "DDP", // DDP terms
            "USD");

        // Assert
        result.Should().NotBeNull();
        result.IsDdp.Should().BeTrue();
    }

    [Fact]
    public async Task GetSpecialTariffRateAsync_WithUsOrigin_ShouldReturn10Percent()
    {
        // Arrange
        using var dbContext = CreateSqlServerDbContext();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act
        var rate = await service.GetSpecialTariffRateAsync("US", "OTHERS", "CN");

        // Assert
        rate.Should().Be(0.10m); // Special tariff for US is 10%
    }

    [Fact]
    public async Task GetSpecialTariffRateAsync_WithNullOrigin_ShouldReturnZero()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act
        var rate = await service.GetSpecialTariffRateAsync(null, "OTHERS", "CN");

        // Assert
        rate.Should().Be(0m);
    }

    [Fact]
    public async Task GetFreightRateAsync_WithChina_ShouldReturnRate()
    {
        // Arrange
        using var dbContext = CreateSqlServerDbContext();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act
        var rate = await service.GetFreightRateAsync("CN", "HZ");

        // Assert
        rate.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public async Task GetFreightRateAsync_WithHongKong_ShouldReturnRate()
    {
        // Arrange
        using var dbContext = CreateSqlServerDbContext();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act
        var rate = await service.GetFreightRateAsync("HK", "HZ");

        // Assert
        rate.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public async Task GetTariffRateAsync_WithNullInputs_ShouldReturnNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SupplierSystemDbContext(options);

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns("/fake/path");

        var exchangeRateLogger = new Mock<ILogger<ExchangeRateService>>();
        var exchangeRateService = new ExchangeRateService(mockEnv.Object, exchangeRateLogger.Object);

        var tariffLogger = new Mock<ILogger<TariffCalculationService>>();
        var service = new TariffCalculationService(dbContext, exchangeRateService, tariffLogger.Object);

        // Act & Assert
        var rate1 = await service.GetTariffRateAsync(null, "OTHERS");
        rate1.Should().BeNull();

        var rate2 = await service.GetTariffRateAsync("CN", null);
        rate2.Should().BeNull();

        var rate3 = await service.GetTariffRateAsync("", "OTHERS");
        rate3.Should().BeNull();
    }

    #endregion

    #region Permission Tests

    [Theory]
    [InlineData("purchaser", Permissions.PurchaserRegistrationApprove, true)]
    [InlineData("purchaser", Permissions.RfqCreate, true)]
    [InlineData("quality_manager", Permissions.QualityManagerRegistrationApprove, true)]
    [InlineData("admin", Permissions.RfqCreate, true)]
    public void AuthUser_ShouldHavePermissions(string role, string permission, bool shouldHave)
    {
        // Arrange
        var user = new AuthUser
        {
            Id = "test-user",
            Name = "Test User",
            Role = role,
            Permissions = new List<string>
            {
                Permissions.PurchaserRegistrationApprove,
                Permissions.RfqCreate,
                Permissions.QualityManagerRegistrationApprove
            }
        };

        // Act
        var hasPermission = user.Permissions.Contains(permission);

        // Assert
        hasPermission.Should().Be(shouldHave);
    }

    [Fact]
    public void AuthUser_ShouldBeAbleToCheckMultiplePermissions()
    {
        // Arrange
        var user = new AuthUser
        {
            Id = "test-user",
            Name = "Test User",
            Role = "admin",
            Permissions = new List<string>
            {
                Permissions.PurchaserRegistrationApprove,
                Permissions.QualityManagerRegistrationApprove,
                Permissions.FinanceAccountantRegistrationApprove
            }
        };

        // Act & Assert
        user.Permissions.Should().Contain(Permissions.PurchaserRegistrationApprove);
        user.Permissions.Should().Contain(Permissions.QualityManagerRegistrationApprove);
        user.Permissions.Should().Contain(Permissions.FinanceAccountantRegistrationApprove);
        user.Permissions.Count.Should().Be(3);
    }

    #endregion

    #region TariffCalculationResult Tests

    [Fact]
    public void TariffCalculationResult_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var result = new TariffCalculationResult
        {
            OriginalPrice = 1000m,
            OriginalCurrency = "USD",
            ExchangeRate = 1.0m,
            OriginalPriceUsd = 1000m,
            TariffRate = 0.05m,
            TariffAmount = 50m,
            StandardCostLocal = 1050m,
            StandardCostUsd = 1050m,
            StandardCost = 1050m,
            ShippingCountry = "CN",
            ProductGroup = "OTHERS",
            ProductOrigin = "US",
            HasSpecialTariff = false
        };

        // Assert
        result.OriginalPrice.Should().Be(1000m);
        result.OriginalCurrency.Should().Be("USD");
        result.ExchangeRate.Should().Be(1.0m);
        result.TariffRate.Should().Be(0.05m);
        result.TariffAmount.Should().Be(50m);
        result.StandardCostLocal.Should().Be(1050m);
        result.HasSpecialTariff.Should().BeFalse();
    }

    [Fact]
    public void TariffCalculationResult_CanHaveWarnings()
    {
        // Arrange & Act
        var result = new TariffCalculationResult
        {
            OriginalPrice = 1000m,
            Warnings = new List<TariffWarning>
            {
                new TariffWarning
                {
                    Code = "MISSING_TARIFF_RATE",
                    Message = "No tariff rate found",
                    Severity = "warning"
                }
            }
        };

        // Assert
        result.Warnings.Should().NotBeNull();
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Code.Should().Be("MISSING_TARIFF_RATE");
    }

    #endregion

    #region ExchangeRateConfig Tests

    [Fact]
    public void ExchangeRateConfig_ShouldStoreRates()
    {
        // Arrange & Act
        var config = new ExchangeRateConfig
        {
            UpdatedAt = "2025-01-01",
            DefaultYear = 2025,
            Rates = new Dictionary<string, Dictionary<string, decimal>>
            {
                ["2025"] = new Dictionary<string, decimal>
                {
                    ["USD"] = 1.0m,
                    ["RMB"] = 0.1388889m
                }
            }
        };

        // Assert
        config.UpdatedAt.Should().Be("2025-01-01");
        config.DefaultYear.Should().Be(2025);
        config.Rates.Should().ContainKey("2025");
        config.Rates["2025"].Should().ContainKey("USD");
    }

    #endregion
}
