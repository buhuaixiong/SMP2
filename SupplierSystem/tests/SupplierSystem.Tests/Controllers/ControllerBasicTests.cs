using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Api.Controllers;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Controllers;

/// <summary>
/// Basic unit tests for Controllers to verify they can be instantiated
/// </summary>
public class ControllerBasicTests
{
    [Fact]
    public void RfqWorkflowController_CanBeCreated()
    {
        // Arrange - RfqWorkflowController requires TariffCalculationService which is sealed
        // We test that the controller dependencies can be resolved by testing with real services
        var mockAuditService = new Mock<IAuditService>();
        mockAuditService.Setup(x => x.LogAsync(It.IsAny<AuditEntry>())).Returns(Task.CompletedTask);

        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(x => x.ContentRootPath).Returns("/");

        // Create actual services with real dependencies
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new SupplierSystemDbContext(options);

        var exchangeRateService = new SupplierSystem.Api.Services.Rfq.ExchangeRateService(
            mockEnvironment.Object,
            Mock.Of<ILogger<SupplierSystem.Api.Services.Rfq.ExchangeRateService>>());

        var tariffService = new SupplierSystem.Api.Services.Rfq.TariffCalculationService(
            dbContext,
            exchangeRateService,
            Mock.Of<ILogger<SupplierSystem.Api.Services.Rfq.TariffCalculationService>>());

        var mockLogger = new Mock<ILogger<RfqWorkflowController>>();

        var prExcelService = new SupplierSystem.Api.Services.PrExcelService(
            dbContext,
            mockEnvironment.Object,
            Mock.Of<ILogger<SupplierSystem.Api.Services.PrExcelService>>());

        var rfqExcelImportService = new SupplierSystem.Api.Services.Rfq.RfqExcelImportService(
            new SupplierSystem.Api.Services.Excel.ExcelOpenXmlReader());

        var priceAuditService = new SupplierSystem.Api.Services.Rfq.RfqPriceAuditService(
            dbContext,
            Mock.Of<ILogger<SupplierSystem.Api.Services.Rfq.RfqPriceAuditService>>());

        // Act
        var controller = new RfqWorkflowController(
            dbContext,
            tariffService,
            prExcelService,
            rfqExcelImportService,
            priceAuditService,
            mockAuditService.Object,
            mockEnvironment.Object,
            mockLogger.Object);

        // Assert
        controller.Should().NotBeNull();
    }

    [Fact]
    public void AuthController_CanBeCreated()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthService>();
        var mockLockoutService = new Mock<IAccountLockoutService>();
        var mockSessionService = new Mock<ISessionService>();
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new SupplierSystemDbContext(options);

        // Act
        var controller = new AuthController(
            mockAuthService.Object,
            mockLockoutService.Object,
            mockSessionService.Object,
            dbContext);

        // Assert
        controller.Should().NotBeNull();
    }

    [Fact]
    public void SuppliersController_CanBeCreated()
    {
        // Arrange
        var mockSupplierService = new Mock<ISupplierService>();
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(x => x.ContentRootPath).Returns("/");
        var mockLogger = new Mock<ILogger<SuppliersController>>();

        // Act
        var controller = new SuppliersController(
            mockSupplierService.Object,
            mockEnvironment.Object,
            mockLogger.Object);

        // Assert
        controller.Should().NotBeNull();
    }
}
