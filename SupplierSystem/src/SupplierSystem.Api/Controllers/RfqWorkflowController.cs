using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rfq-workflow")]
public sealed partial class RfqWorkflowController(
    SupplierSystemDbContext dbContext,
    TariffCalculationService tariffService,
    PrExcelService prExcelService,
    RfqExcelImportService rfqExcelImportService,
    RfqPriceAuditService priceAuditService,
    IAuditService auditService,
    IWebHostEnvironment environment,
    ILogger<RfqWorkflowController> logger) : ControllerBase
{
    private const string DefaultCurrency = "CNY";
    private static readonly Dictionary<string, string> CountryAliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HK"] = "HK",
        ["HKG"] = "HK",
        ["HONG KONG"] = "HK",
        ["HONGKONG"] = "HK",
        ["HONG-KONG"] = "HK",
    };

    private static readonly Regex CountryCodeRegex = new("^[A-Z]{2,3}$", RegexOptions.Compiled);

    private readonly SupplierSystemDbContext _dbContext = dbContext;
    private readonly TariffCalculationService _tariffService = tariffService;
    private readonly PrExcelService _prExcelService = prExcelService;
    private readonly RfqExcelImportService _rfqExcelImportService = rfqExcelImportService;
    private readonly RfqPriceAuditService _priceAuditService = priceAuditService;
    private readonly IAuditService _auditService = auditService;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly ILogger<RfqWorkflowController> _logger = logger;

    private static readonly HashSet<string> PurchaserQuotePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        Permissions.PurchaserRfqTarget,
        Permissions.ProcurementManagerRfqReview,
        Permissions.ProcurementDirectorRfqApprove,
    };
}
