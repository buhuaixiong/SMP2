using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SupplierSystem.Api.Controllers;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Excel;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using Xunit;

namespace SupplierSystem.Tests.Controllers;

public class RfqWorkflowControllerListRfqsTests
{
    [Fact]
    public async Task ListRfqs_PurchaserCanViewAllRfqs()
    {
        var dbContext = CreateDbContext();
        await dbContext.Rfqs.AddRangeAsync(
            CreateRfq(1001, "buyer-1"),
            CreateRfq(1002, "buyer-2"));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var purchaser = new AuthUser
        {
            Id = "buyer-1",
            Name = "Buyer 1",
            Role = "purchaser",
            Permissions = new List<string> { Permissions.PurchaserRfqTarget }
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Items["AuthUser"] = purchaser;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.ListRfqs(CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(okResult.Value));
        var rfqIds = document.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64())
            .ToList();

        rfqIds.Should().BeEquivalentTo(new long[] { 1001, 1002 });
    }

    private static RfqWorkflowController CreateController(SupplierSystemDbContext dbContext)
    {
        var mockAuditService = new Mock<IAuditService>();
        mockAuditService
            .Setup(service => service.LogAsync(It.IsAny<AuditEntry>()))
            .Returns(Task.CompletedTask);

        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(environment => environment.ContentRootPath).Returns("/");

        var exchangeRateService = new ExchangeRateService(
            mockEnvironment.Object,
            Mock.Of<ILogger<ExchangeRateService>>());
        var tariffService = new TariffCalculationService(
            dbContext,
            exchangeRateService,
            Mock.Of<ILogger<TariffCalculationService>>());
        var prExcelService = new PrExcelService(
            dbContext,
            mockEnvironment.Object,
            Mock.Of<ILogger<PrExcelService>>());
        var rfqExcelImportService = new RfqExcelImportService(new ExcelOpenXmlReader());
        var priceAuditService = new RfqPriceAuditService(
            dbContext,
            Mock.Of<ILogger<RfqPriceAuditService>>());

        return new RfqWorkflowController(
            dbContext,
            tariffService,
            prExcelService,
            rfqExcelImportService,
            priceAuditService,
            mockAuditService.Object,
            mockEnvironment.Object,
            Mock.Of<ILogger<RfqWorkflowController>>());
    }

    private static SupplierSystemDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SupplierSystemDbContext(options);
    }

    private static Rfq CreateRfq(long id, string createdBy)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        return new Rfq
        {
            Id = id,
            Title = $"RFQ-{id}",
            Description = "Test RFQ",
            Status = "draft",
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
            IsLineItemMode = true,
            Currency = "CNY"
        };
    }
}
