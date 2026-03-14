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
using SupplierSystem.Api.Services.Audit;
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

public class RfqWorkflowControllerMultiRoundTests
{
    [Fact]
    public async Task ExtendBidDeadline_UpdatesCurrentRoundAndRfqDeadline()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTime.UtcNow;
        var currentDeadline = now.AddDays(2).ToString("o");
        var newDeadline = now.AddDays(4).ToString("o");

        dbContext.Rfqs.Add(new Rfq
        {
            Id = 2001,
            Title = "Round RFQ",
            Description = "Test RFQ",
            Status = "published",
            ValidUntil = currentDeadline,
            CreatedBy = "buyer-1",
            CreatedAt = now.ToString("o"),
            UpdatedAt = now.ToString("o"),
            Currency = "USD",
            IsLineItemMode = true,
        });
        dbContext.RfqBidRounds.Add(new RfqBidRound
        {
            Id = 301,
            RfqId = 2001,
            RoundNumber = 1,
            BidDeadline = currentDeadline,
            Status = "published",
            CreatedBy = "buyer-1",
            CreatedAt = now.ToString("o"),
            UpdatedAt = now.ToString("o"),
        });
        dbContext.SupplierRfqInvitations.Add(new SupplierRfqInvitation
        {
            Id = 1,
            RfqId = 2001,
            BidRoundId = 301,
            SupplierId = 4001,
            Status = "pending",
            InvitedAt = now.ToString("o"),
            UpdatedAt = now.ToString("o"),
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildHttpContext(new AuthUser
            {
                Id = "buyer-1",
                Name = "Buyer 1",
                Role = "purchaser",
                Permissions = [Permissions.PurchaserRfqTarget],
            }),
        };

        using var body = JsonDocument.Parse($$"""
        {
          "newDeadline": "{{newDeadline}}",
          "reason": "Need more supplier responses"
        }
        """);

        var result = await controller.ExtendBidDeadline(2001, body.RootElement.Clone(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var rfq = await dbContext.Rfqs.SingleAsync(item => item.Id == 2001);
        var round = await dbContext.RfqBidRounds.SingleAsync(item => item.Id == 301);

        rfq.ValidUntil.Should().Be(newDeadline);
        round.BidDeadline.Should().Be(newDeadline);
        round.ExtensionReason.Should().Be("Need more supplier responses");
    }

    [Fact]
    public async Task ComparisonPrintService_BuildAsync_AllScopeIncludesHistoricalRounds()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTime.UtcNow;
        SeedMultiRoundPrintData(dbContext, now);
        await dbContext.SaveChangesAsync();

        var service = new RfqComparisonPrintService(
            dbContext,
            new RfqWorkflowStore(dbContext),
            new AuditReadStore(dbContext));

        var data = await service.BuildAsync(3001, "Buyer 1", "all", CancellationToken.None);

        data.Should().NotBeNull();
        data!.Scope.Should().Be("all");
        data.RoundGroups.Should().HaveCount(2);
        data.RoundGroups.Select(group => group.RoundNumber).Should().Equal(2, 1);
        data.RoundGroups[0].QuoteRows.Should().OnlyContain(row => row.RoundNumber == 2);
        data.RoundGroups[1].QuoteRows.Should().OnlyContain(row => row.RoundNumber == 1);
        data.AuditRows.Should().Contain(row => row.RoundNumber == 2);
        data.AuditRows.Should().Contain(row => row.RoundNumber == 1);
    }

    [Fact]
    public async Task ComparisonPrintService_BuildAsync_LatestScopeExcludesHistoricalRounds()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTime.UtcNow;
        SeedMultiRoundPrintData(dbContext, now);
        await dbContext.SaveChangesAsync();

        var service = new RfqComparisonPrintService(
            dbContext,
            new RfqWorkflowStore(dbContext),
            new AuditReadStore(dbContext));

        var data = await service.BuildAsync(3001, "Buyer 1", "latest", CancellationToken.None);

        data.Should().NotBeNull();
        data!.Scope.Should().Be("latest");
        data.RoundGroups.Should().HaveCount(1);
        data.RoundGroups.Single().RoundNumber.Should().Be(2);
        data.QuoteRows.Should().OnlyContain(row => row.RoundNumber == 2);
        data.AuditRows.Should().OnlyContain(row => !row.RoundNumber.HasValue || row.RoundNumber == 2);
    }

    private static SupplierSystemDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SupplierSystemDbContext(options);
    }

    private static DefaultHttpContext BuildHttpContext(AuthUser user)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["AuthUser"] = user;
        return httpContext;
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
        var comparisonPrintService = new RfqComparisonPrintService(
            dbContext,
            new RfqWorkflowStore(dbContext),
            new AuditReadStore(dbContext));

        return new RfqWorkflowController(
            new RfqWorkflowStore(dbContext),
            comparisonPrintService,
            tariffService,
            prExcelService,
            rfqExcelImportService,
            priceAuditService,
            mockAuditService.Object,
            mockEnvironment.Object,
            Mock.Of<ILogger<RfqWorkflowController>>());
    }

    private static void SeedMultiRoundPrintData(SupplierSystemDbContext dbContext, DateTime now)
    {
        dbContext.Rfqs.Add(new Rfq
        {
            Id = 3001,
            Title = "Print RFQ",
            Description = "Print test",
            Status = "closed",
            ValidUntil = now.AddDays(4).ToString("o"),
            CreatedBy = "buyer-1",
            CreatedAt = now.AddDays(-10).ToString("o"),
            UpdatedAt = now.ToString("o"),
            ReviewCompletedAt = now.ToString("o"),
            SelectedQuoteId = 502,
            Currency = "USD",
            IsLineItemMode = true,
        });

        dbContext.RfqBidRounds.AddRange(
            new RfqBidRound
            {
                Id = 401,
                RfqId = 3001,
                RoundNumber = 1,
                BidDeadline = now.AddDays(-6).ToString("o"),
                Status = "closed",
                OpenedAt = now.AddDays(-6).ToString("o"),
                ClosedAt = now.AddDays(-5).ToString("o"),
                CreatedBy = "buyer-1",
                CreatedAt = now.AddDays(-8).ToString("o"),
                UpdatedAt = now.AddDays(-5).ToString("o"),
            },
            new RfqBidRound
            {
                Id = 402,
                RfqId = 3001,
                RoundNumber = 2,
                BidDeadline = now.AddDays(-2).ToString("o"),
                Status = "closed",
                OpenedAt = now.AddDays(-2).ToString("o"),
                ClosedAt = now.AddDays(-1).ToString("o"),
                CreatedBy = "buyer-1",
                CreatedAt = now.AddDays(-4).ToString("o"),
                UpdatedAt = now.AddDays(-1).ToString("o"),
                StartedFromRoundId = 401,
            });

        dbContext.Suppliers.AddRange(
            CreateSupplier(4001, "Supplier A"),
            CreateSupplier(4002, "Supplier B"));

        dbContext.SupplierRfqInvitations.AddRange(
            CreateInvitation(3001, 401, 4001, now.AddDays(-8)),
            CreateInvitation(3001, 401, 4002, now.AddDays(-8)),
            CreateInvitation(3001, 402, 4001, now.AddDays(-4)),
            CreateInvitation(3001, 402, 4002, now.AddDays(-4)));

        dbContext.RfqLineItems.Add(new RfqLineItem
        {
            Id = 601,
            RfqId = 3001,
            LineNumber = 1,
            ItemName = "Bearing",
            Specifications = "SKF",
            Quantity = 10,
            Unit = "pcs",
            MaterialCategory = "M-100",
            CreatedAt = now.AddDays(-10).ToString("o"),
        });

        dbContext.RfqPriceAuditRecords.AddRange(
            CreateAuditRecord(7001, 3001, 401, 1, 501, 4001, "Supplier A", 12.5m, 125m, false, now.AddDays(-6)),
            CreateAuditRecord(7002, 3001, 402, 2, 502, 4002, "Supplier B", 11.0m, 110m, true, now.AddDays(-2)));

        dbContext.AuditLogs.AddRange(
            new AuditLog
            {
                Id = 1,
                ActorId = "supplier-a",
                ActorName = "Supplier A",
                EntityType = "quote",
                EntityId = "501",
                Action = "submit_quote",
                Changes = "{\"roundNumber\":1}",
                Summary = "Submitted round 1 quote",
                CreatedAt = now.AddDays(-6),
            },
            new AuditLog
            {
                Id = 2,
                ActorId = "buyer-1",
                ActorName = "Buyer 1",
                EntityType = "rfq",
                EntityId = "3001",
                Action = "start_next_bid_round",
                Changes = "{\"roundNumber\":2}",
                Summary = "Started round 2",
                CreatedAt = now.AddDays(-4),
            },
            new AuditLog
            {
                Id = 3,
                ActorId = "supplier-b",
                ActorName = "Supplier B",
                EntityType = "quote",
                EntityId = "502",
                Action = "submit_quote",
                Changes = "{\"roundNumber\":2}",
                Summary = "Submitted round 2 quote",
                CreatedAt = now.AddDays(-2),
            });
    }

    private static Supplier CreateSupplier(int id, string companyName)
    {
        return new Supplier
        {
            Id = id,
            CompanyName = companyName,
            CompanyId = $"SUP-{id}",
            Status = "active",
            Stage = "approved",
            CreatedAt = DateTime.UtcNow.ToString("o"),
        };
    }

    private static SupplierRfqInvitation CreateInvitation(int rfqId, long bidRoundId, int supplierId, DateTime invitedAt)
    {
        return new SupplierRfqInvitation
        {
            RfqId = rfqId,
            BidRoundId = bidRoundId,
            SupplierId = supplierId,
            Status = "pending",
            InvitedAt = invitedAt.ToString("o"),
            UpdatedAt = invitedAt.ToString("o"),
        };
    }

    private static RfqPriceAuditRecord CreateAuditRecord(
        long id,
        long rfqId,
        long bidRoundId,
        int roundNumber,
        long quoteId,
        long supplierId,
        string supplierName,
        decimal unitPrice,
        decimal totalPrice,
        bool selected,
        DateTime submittedAt)
    {
        return new RfqPriceAuditRecord
        {
            Id = id,
            RfqId = rfqId,
            BidRoundId = bidRoundId,
            RoundNumber = roundNumber,
            RfqLineItemId = 601,
            LineNumber = 1,
            Quantity = 10,
            QuoteId = quoteId,
            SupplierId = supplierId,
            SupplierName = supplierName,
            QuoteCurrency = "USD",
            QuotedUnitPrice = unitPrice,
            QuotedTotalPrice = totalPrice,
            QuoteSubmittedAt = submittedAt.ToString("o"),
            SelectedQuoteId = selected ? quoteId : 502,
            SelectedSupplierId = selected ? supplierId : 4002,
            SelectedSupplierName = selected ? supplierName : "Supplier B",
            SelectedUnitPrice = selected ? unitPrice : 11m,
            SelectedCurrency = "USD",
            CreatedAt = submittedAt.ToString("o"),
            UpdatedAt = submittedAt.ToString("o"),
        };
    }
}
