using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Rfq;

public sealed partial class LineItemWorkflowService(
    SupplierSystemDbContext dbContext,
    IAuditService auditService,
    RfqPriceAuditService priceAuditService,
    ILogger<LineItemWorkflowService> logger) : NodeServiceBase
{
    private readonly SupplierSystemDbContext _dbContext = dbContext;
    private readonly IAuditService _auditService = auditService;
    private readonly RfqPriceAuditService _priceAuditService = priceAuditService;
    private readonly ILogger<LineItemWorkflowService> _logger = logger;

    private static class LineItemStatus
    {
        public const string Draft = "draft";
        public const string PendingDirector = "pending_director";
        public const string PendingPo = "pending_po";
        public const string Completed = "completed";
        public const string Rejected = "rejected";
    }
}
