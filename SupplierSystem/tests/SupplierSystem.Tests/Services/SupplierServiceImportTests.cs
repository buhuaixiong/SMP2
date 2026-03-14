using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OfficeOpenXml;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Services;
using Xunit;

namespace SupplierSystem.Tests.Services;

public class SupplierServiceImportTests
{
    [Fact]
    public async Task ImportSuppliersFromExcelAsync_WithSapHeaders_ShouldImportSupplier()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var dbContext = new SupplierSystemDbContext(options);
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(x => x.LogAsync(It.IsAny<SupplierSystem.Application.Models.Audit.AuditEntry>()))
            .Returns(Task.CompletedTask);

        var service = new SupplierService(
            dbContext,
            auditService.Object,
            NullLogger<SupplierService>.Instance);

        var excelBytes = CreateSapVendorWorkbook();

        var result = await service.ImportSuppliersFromExcelAsync(
            excelBytes,
            "Vendor List 20260130.xlsx",
            "admin",
            CancellationToken.None);

        result.Summary.ImportedRows.Should().Be(
            1,
            "errors: {0}",
            string.Join(" | ", result.Summary.Errors.Select(error => $"{error.Row}:{error.Message}")));
        result.Summary.Errors.Should().BeEmpty();

        var supplier = await dbContext.Suppliers.SingleAsync();
        supplier.CompanyId.Should().Be("V0001");
        supplier.CompanyName.Should().Be("Acme Metals Ltd");
        supplier.ContactPerson.Should().Be("Alice Chen");
        supplier.ContactPhone.Should().Be("13800001111");
        supplier.FaxNumber.Should().Be("021-60000000");
        supplier.PaymentTerms.Should().Be("NET30");
        supplier.PaymentCurrency.Should().Be("USD");
        supplier.BusinessRegistrationNumber.Should().Be("TAX-9988");
        supplier.Address.Should().Be("Line 1, Line 2, Shanghai");

        var user = await dbContext.Users.SingleAsync();
        user.Id.Should().Be("V0001");
        user.SupplierId.Should().Be(supplier.Id);
    }

    [Fact]
    public async Task ImportSuppliersFromExcelAsync_WhenRequestTokenCanceled_ShouldStillPersistRows()
    {
        var options = new DbContextOptionsBuilder<SupplierSystemDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var dbContext = new SupplierSystemDbContext(options);
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(x => x.LogAsync(It.IsAny<SupplierSystem.Application.Models.Audit.AuditEntry>()))
            .Returns(Task.CompletedTask);

        var service = new SupplierService(
            dbContext,
            auditService.Object,
            NullLogger<SupplierService>.Instance);

        var excelBytes = CreateSapVendorWorkbook();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.ImportSuppliersFromExcelAsync(
            excelBytes,
            "Vendor List 20260130.xlsx",
            "admin",
            cts.Token);

        result.Summary.ImportedRows.Should().Be(1);
        result.Summary.Errors.Should().BeEmpty();
        (await dbContext.Suppliers.CountAsync()).Should().Be(1);
    }

    private static byte[] CreateSapVendorWorkbook()
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Vendor List");

        sheet.Cells[1, 1].Value = "VENDOR";
        sheet.Cells[1, 2].Value = "VTYPE";
        sheet.Cells[1, 3].Value = "VNDNAM";
        sheet.Cells[1, 4].Value = "VNALPH";
        sheet.Cells[1, 5].Value = "VNDAD1";
        sheet.Cells[1, 6].Value = "VNDAD2";
        sheet.Cells[1, 7].Value = "ADDRS3";
        sheet.Cells[1, 8].Value = "VCON";
        sheet.Cells[1, 9].Value = "VPHONE";
        sheet.Cells[1, 10].Value = "VMVFAX";
        sheet.Cells[1, 11].Value = "VPAYTY";
        sheet.Cells[1, 12].Value = "VTERMS";
        sheet.Cells[1, 13].Value = "VTAXCD";
        sheet.Cells[1, 14].Value = "VTMDSC";
        sheet.Cells[1, 15].Value = "CCDESC";
        sheet.Cells[1, 16].Value = "CNAME";
        sheet.Cells[1, 17].Value = "VMDATN";
        sheet.Cells[1, 18].Value = "VCURR";
        sheet.Cells[1, 19].Value = "VMUF10";
        sheet.Cells[1, 20].Value = "VMREF2";

        sheet.Cells[2, 1].Value = "V0001";
        sheet.Cells[2, 2].Value = "RAW";
        sheet.Cells[2, 3].Value = "Acme Metals Ltd";
        sheet.Cells[2, 4].Value = "ACME";
        sheet.Cells[2, 5].Value = "Line 1";
        sheet.Cells[2, 6].Value = "Line 2";
        sheet.Cells[2, 7].Value = "Shanghai";
        sheet.Cells[2, 8].Value = "Alice Chen";
        sheet.Cells[2, 9].Value = "13800001111";
        sheet.Cells[2, 10].Value = "021-60000000";
        sheet.Cells[2, 11].Value = "Bank Transfer";
        sheet.Cells[2, 12].Value = "NET30";
        sheet.Cells[2, 13].Value = "TAX-9988";
        sheet.Cells[2, 14].Value = "Preferred vendor";
        sheet.Cells[2, 15].Value = "China";
        sheet.Cells[2, 16].Value = "CN";
        sheet.Cells[2, 17].Value = "2026-01-30";
        sheet.Cells[2, 18].Value = "USD";
        sheet.Cells[2, 19].Value = "N/A";
        sheet.Cells[2, 20].Value = "SAP reference";

        return package.GetAsByteArray();
    }
}
