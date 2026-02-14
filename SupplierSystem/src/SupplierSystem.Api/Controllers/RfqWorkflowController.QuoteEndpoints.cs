using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    [HttpPost("{id:int}/quotes")]
    public async Task<IActionResult> SubmitQuote(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var user = HttpContext.GetAuthUser();
        if (user?.SupplierId == null)
        {
            return StatusCode(403, new { message = "Only suppliers can submit quotes." });
        }

        var (payload, attachments) = await ReadQuotePayloadAsync(cancellationToken);
        if (payload == null)
        {
            return BadRequest(new { message = "Invalid quote data format." });
        }

        var lineItemsResult = TryParseSubmitLineItems(payload.Value);
        if (!lineItemsResult.Success)
        {
            return BadRequest(new { message = lineItemsResult.ErrorMessage });
        }

        var lineItems = lineItemsResult.LineItems;

        var paymentTerms = ReadStringValue(payload.Value, "paymentTerms");
        var notes = ReadStringValue(payload.Value, "notes");
        var currency = ReadStringValue(payload.Value, "currency");
        var taxStatus = ReadStringValue(payload.Value, "taxStatus");
        var deliveryTerms = ReadStringValue(payload.Value, "deliveryTerms");
        var shippingCountry = ReadStringValue(payload.Value, "shippingCountry");
        var shippingLocation = ReadStringValue(payload.Value, "shippingLocation");

        if (!QuoteTaxStatusNormalizer.TryNormalize(taxStatus, out var normalizedQuoteTaxStatus))
        {
            return BadRequest(new { message = "Invalid quote-level taxStatus." });
        }

        if (string.IsNullOrWhiteSpace(shippingCountry))
        {
            return BadRequest(new { message = "Shipping country is required." });
        }

        var normalizedShippingCountry = shippingCountry.Trim().ToUpperInvariant();
        var normalizedShippingLocation = string.IsNullOrWhiteSpace(shippingLocation)
            ? null
            : shippingLocation.Trim();

        try
        {
            var rfq = await _dbContext.Rfqs.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var rfqStatus = rfq.Status ?? string.Empty;
            if (!string.Equals(rfqStatus, "published", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rfqStatus, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "RFQ is not accepting quotes." });
            }

            if (!string.IsNullOrWhiteSpace(rfq.ValidUntil) &&
                DateTime.TryParse(rfq.ValidUntil, out var deadline) &&
                deadline < DateTime.UtcNow)
            {
                return BadRequest(new { message = "RFQ has expired." });
            }

            var invitation = await _dbContext.SupplierRfqInvitations.AsNoTracking()
                .FirstOrDefaultAsync(i => i.RfqId == id && i.SupplierId == user.SupplierId, cancellationToken);
            if (invitation == null)
            {
                return StatusCode(403, new { message = "You are not invited to quote on this RFQ." });
            }

            var hasQuote = await _dbContext.Quotes.AsNoTracking()
                .AnyAsync(q => q.RfqId == id && q.SupplierId == user.SupplierId, cancellationToken);
            if (hasQuote)
            {
                return StatusCode(409, new
                {
                    message = "You have already submitted a quote. Please use the update endpoint to modify it."
                });
            }

            var selectedCurrency = !string.IsNullOrWhiteSpace(currency)
                ? currency.Trim().ToUpperInvariant()
                : rfq.Currency ?? DefaultCurrency;

            var lineItemIds = lineItems.Select(item => (long)item.RfqLineItemId).Distinct().ToList();
            var rfqLineItems = await _dbContext.RfqLineItems.AsNoTracking()
                .Where(li => li.RfqId == id && lineItemIds.Contains(li.Id))
                .Select(li => new { li.Id, li.Quantity })
                .ToListAsync(cancellationToken);

            var quantityMap = rfqLineItems.ToDictionary(li => li.Id, li => li.Quantity);
            var missingLineItem = lineItems.FirstOrDefault(item => !quantityMap.ContainsKey(item.RfqLineItemId));
            if (missingLineItem != null)
            {
                return BadRequest(new
                {
                    message = $"Line item {missingLineItem.Index}: Invalid rfqLineItemId."
                });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var totalAmount = lineItems.Sum(item =>
            {
                if (!item.UnitPrice.HasValue)
                {
                    return 0m;
                }

                var quantity = quantityMap[item.RfqLineItemId];
                return item.UnitPrice.Value * quantity;
            });

            var now = DateTime.UtcNow.ToString("o");
            var ipAddress = HttpContext.GetClientIp();
            var quote = new Quote
            {
                RfqId = id,
                SupplierId = user.SupplierId.Value,
                TotalAmount = totalAmount,
                Currency = selectedCurrency,
                TaxStatus = normalizedQuoteTaxStatus,
                PaymentTerms = string.IsNullOrWhiteSpace(paymentTerms) ? null : paymentTerms,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                Status = "submitted",
                SubmittedAt = now,
                Version = 1,
                IsLatest = true,
                DeliveryTerms = string.IsNullOrWhiteSpace(deliveryTerms) ? null : deliveryTerms,
                ShippingCountry = normalizedShippingCountry,
                ShippingLocation = normalizedShippingLocation,
                CreatedAt = now,
                UpdatedAt = now,
                IpAddress = ipAddress,
            };

            _dbContext.Quotes.Add(quote);
            await _dbContext.SaveChangesAsync(cancellationToken);

            decimal totalStandardCostLocal = 0m;
            decimal totalStandardCostUsd = 0m;
            decimal totalTariffLocal = 0m;
            decimal totalTariffUsd = 0m;
            var hasSpecialTariff = false;

            foreach (var item in lineItems)
            {
                var quantity = quantityMap[item.RfqLineItemId];
                var unitPrice = item.UnitPrice;
                var hasPrice = unitPrice.HasValue;
                decimal? totalPrice = unitPrice is { } unitPriceTotal ? unitPriceTotal * quantity : null;
                TariffCalculationResult? calc = null;

                if (unitPrice.HasValue)
                {
                    var unitPriceValue = unitPrice.Value;
                    calc = await _tariffService.CalculateStandardCostAsync(
                        unitPriceValue,
                        normalizedShippingCountry,
                        item.ProductGroup,
                        item.ProductOrigin,
                        currency: selectedCurrency,
                        cancellationToken: cancellationToken);

                    totalStandardCostLocal += ToFixedNumber(calc.StandardCostLocal * quantity);
                    if (calc.StandardCostUsd.HasValue)
                    {
                        totalStandardCostUsd += ToFixedNumber(calc.StandardCostUsd.Value * quantity);
                    }

                    totalTariffLocal += ToFixedNumber(calc.TotalTariffAmount * quantity);
                    if (calc.TotalTariffAmountUsd.HasValue)
                    {
                        totalTariffUsd += ToFixedNumber(calc.TotalTariffAmountUsd.Value * quantity);
                    }

                    if (calc.HasSpecialTariff)
                    {
                        hasSpecialTariff = true;
                    }
                }

                _dbContext.QuoteLineItems.Add(new QuoteLineItem
                {
                    QuoteId = quote.Id,
                    RfqLineItemId = item.RfqLineItemId,
                    UnitPrice = item.UnitPrice,
                    MinimumOrderQuantity = item.MinimumOrderQuantity,
                    StandardPackageQuantity = item.StandardPackageQuantity,
                    TotalPrice = totalPrice,
                    Brand = item.Brand,
                    TaxStatus = string.IsNullOrWhiteSpace(item.TaxStatus) ? null : item.TaxStatus,
                    DeliveryPeriod = item.DeliveryPeriod,
                    Parameters = item.Parameters,
                    Notes = item.Notes,
                    ProductOrigin = item.ProductOrigin,
                    ProductGroup = item.ProductGroup,
                    OriginalPriceUsd = calc?.OriginalPriceUsd,
                    ExchangeRate = calc?.ExchangeRate,
                    ExchangeRateDate = hasPrice ? DateTime.UtcNow.ToString("yyyy-MM-dd") : null,
                    TariffRate = calc?.TariffRate,
                    TariffRatePercent = ParsePercentValue(calc?.TariffRatePercent),
                    TariffAmountLocal = calc?.TariffAmount,
                    TariffAmountUsd = calc?.TariffAmountUsd,
                    SpecialTariffRate = calc?.SpecialTariffRate,
                    SpecialTariffRatePercent = ParsePercentValue(calc?.SpecialTariffRatePercent),
                    SpecialTariffAmountLocal = calc?.SpecialTariffAmount,
                    SpecialTariffAmountUsd = calc?.SpecialTariffAmountUsd,
                    HasSpecialTariff = calc?.HasSpecialTariff ?? false,
                    StandardCostLocal = calc?.StandardCostLocal,
                    StandardCostUsd = calc?.StandardCostUsd,
                    StandardCostCurrency = calc?.StandardCostCurrency,
                    CalculatedAt = hasPrice ? DateTime.UtcNow.ToString("o") : null,
                });
            }

            quote.TotalStandardCostLocal = totalStandardCostLocal;
            quote.TotalStandardCostUsd = totalStandardCostUsd > 0m ? totalStandardCostUsd : null;
            quote.TotalTariffAmountLocal = totalTariffLocal;
            quote.TotalTariffAmountUsd = totalTariffUsd > 0m ? totalTariffUsd : null;
            quote.HasSpecialTariff = hasSpecialTariff;

            var storedFiles = await SaveAttachmentsAsync(attachments, id, cancellationToken);
            long? uploadedBy = null;
            if (!string.IsNullOrWhiteSpace(user.Id) && long.TryParse(user.Id, out var parsedUserId))
            {
                uploadedBy = parsedUserId;
            }
            foreach (var file in storedFiles)
            {
                var fileSize = file.Size > int.MaxValue ? (int?)null : (int)file.Size;
                _dbContext.QuoteAttachments.Add(new QuoteAttachment
                {
                    QuoteId = quote.Id,
                    OriginalName = file.OriginalName,
                    StoredName = file.StoredName,
                    FileType = file.ContentType,
                    FileSize = fileSize,
                    UploadedAt = now,
                    UploadedBy = uploadedBy,
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync("quote", quote.Id.ToString(CultureInfo.InvariantCulture), "submit",
                new
                {
                    rfqId = id,
                    lineItemCount = lineItems.Count,
                    totalAmount,
                    attachmentCount = storedFiles.Count,
                },
                user,
                cancellationToken);

            await _priceAuditService.UpsertQuoteAuditAsync(quote, ipAddress, cancellationToken);

            await TryNotifyQuoteSubmittedAsync(rfq, cancellationToken);

            var responseQuote = await BuildQuoteResponseAsync(quote, quote.Currency ?? DefaultCurrency, cancellationToken);
            return StatusCode(201, new
            {
                message = "Quote submitted successfully",
                data = responseQuote,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit quote.");
            return StatusCode(500, new { message = "Failed to submit quote." });
        }
    }

    [HttpGet("{id:int}/quotes")]
    public async Task<IActionResult> GetQuotes(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return StatusCode(401, new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var hasPurchaserPermission = PurchaserQuotePermissions.Any(granted.Contains);
        var isSupplierUser = user.SupplierId.HasValue;

        if (!hasPurchaserPermission && !isSupplierUser)
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        if (!hasPurchaserPermission && isSupplierUser)
        {
            var invited = await _dbContext.SupplierRfqInvitations.AsNoTracking()
                .AnyAsync(inv => inv.RfqId == id && inv.SupplierId == user.SupplierId, cancellationToken);
            if (!invited)
            {
                return StatusCode(403, new { message = "You are not invited to this RFQ." });
            }
        }

        var visibility = await QuoteVisibility.GetVisibilityAsync(_dbContext, id, user, cancellationToken);
        if (visibility.Locked)
        {
            return Ok(new
            {
                data = new List<object>(),
                quotesVisible = false,
                visibilityReason = new
                {
                    totalInvited = visibility.Context.InvitedCount,
                    submittedCount = visibility.Context.SubmittedCount,
                    deadline = visibility.Context.Deadline,
                    message = QuoteVisibility.QuoteVisibilityLockMessage,
                },
            });
        }

        try
        {
            var quoteQuery = from q in _dbContext.Quotes.AsNoTracking()
                             join s in _dbContext.Suppliers.AsNoTracking() on q.SupplierId equals s.Id
                             where q.RfqId == id
                             select new { Quote = q, s.CompanyName, s.CompanyId, s.Stage };

            if (!hasPurchaserPermission && isSupplierUser)
            {
                quoteQuery = quoteQuery.Where(row => row.Quote.SupplierId == user.SupplierId);
            }

            var quoteRows = await quoteQuery
                .OrderByDescending(row => row.Quote.SubmittedAt)
                .ToListAsync(cancellationToken);

            var result = new List<Dictionary<string, object?>>();
            foreach (var row in quoteRows)
            {
                var quoteDict = NodeCaseMapper.ToSnakeCaseDictionary(row.Quote);
                quoteDict["companyName"] = row.CompanyName;
                quoteDict["supplierName"] = row.CompanyName;
                quoteDict["companyId"] = row.CompanyId;
                quoteDict["stage"] = row.Stage;

                var quoteResponse = await BuildQuoteResponseAsync(quoteDict, row.Quote.Currency ?? DefaultCurrency, cancellationToken);
                result.Add(quoteResponse);
            }

            return Ok(new { data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch quotes.");
            return StatusCode(500, new { message = "Failed to fetch quotes." });
        }
    }

    [HttpPut("{id:int}/quotes/{quoteId:int}")]
    public async Task<IActionResult> UpdateQuote(int id, int quoteId, CancellationToken cancellationToken)
    {
        if (id <= 0 || quoteId <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ or quote ID." });
        }

        var user = HttpContext.GetAuthUser();
        if (user?.SupplierId == null)
        {
            return StatusCode(403, new { message = "Only suppliers can modify quotes." });
        }

        var (payload, attachments) = await ReadQuotePayloadAsync(cancellationToken);
        if (payload == null)
        {
            return BadRequest(new { message = "Invalid quote data format." });
        }

        var lineItemsResult = TryParseUpdateLineItems(payload.Value);
        if (!lineItemsResult.Success)
        {
            return BadRequest(new { message = lineItemsResult.ErrorMessage });
        }

        var lineItems = lineItemsResult.LineItems;
        var paymentTerms = ReadStringValue(payload.Value, "paymentTerms");
        var notes = ReadStringValue(payload.Value, "notes");
        var currency = ReadStringValue(payload.Value, "currency");
        var taxStatus = ReadStringValue(payload.Value, "taxStatus");
        var deliveryTerms = ReadStringValue(payload.Value, "deliveryTerms");
        var shippingCountry = ReadStringValue(payload.Value, "shippingCountry");
        var shippingLocation = ReadStringValue(payload.Value, "shippingLocation");

        if (!QuoteTaxStatusNormalizer.TryNormalize(taxStatus, out var normalizedQuoteTaxStatus))
        {
            return BadRequest(new { message = "Invalid quote-level taxStatus." });
        }

        if (string.IsNullOrWhiteSpace(shippingCountry))
        {
            return BadRequest(new { message = "Shipping country is required." });
        }

        var normalizedShippingCountry = shippingCountry.Trim().ToUpperInvariant();
        var normalizedShippingLocation = string.IsNullOrWhiteSpace(shippingLocation)
            ? null
            : shippingLocation.Trim();

        try
        {
            var quote = await _dbContext.Quotes
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.RfqId == id && q.SupplierId == user.SupplierId, cancellationToken);
            if (quote == null)
            {
                return NotFound(new { message = "Quote not found or access denied." });
            }

            var rfq = await _dbContext.Rfqs.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var rfqStatus = rfq.Status ?? string.Empty;
            if (!string.Equals(rfqStatus, "published", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rfqStatus, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "RFQ is not accepting quote updates." });
            }

            var invitation = await _dbContext.SupplierRfqInvitations.AsNoTracking()
                .FirstOrDefaultAsync(i => i.RfqId == id && i.SupplierId == user.SupplierId, cancellationToken);
            if (invitation == null)
            {
                return StatusCode(403, new { message = "You are not invited to this RFQ." });
            }

            if (!string.IsNullOrWhiteSpace(rfq.ValidUntil) &&
                DateTime.TryParse(rfq.ValidUntil, out var deadline) &&
                deadline < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Cannot modify quote after deadline." });
            }

            var selectedCurrency = !string.IsNullOrWhiteSpace(currency)
                ? currency.Trim().ToUpperInvariant()
                : quote.Currency ?? rfq.Currency ?? DefaultCurrency;

            var lineItemIds = lineItems.Select(item => (long)item.RfqLineItemId).Distinct().ToList();
            var rfqLineItems = await _dbContext.RfqLineItems.AsNoTracking()
                .Where(li => li.RfqId == id && lineItemIds.Contains(li.Id))
                .Select(li => new { li.Id, li.Quantity })
                .ToListAsync(cancellationToken);

            var quantityMap = rfqLineItems.ToDictionary(li => li.Id, li => li.Quantity);
            var missingLineItem = lineItems.FirstOrDefault(item => !quantityMap.ContainsKey(item.RfqLineItemId));
            if (missingLineItem != null)
            {
                return BadRequest(new
                {
                    message = $"Line item {missingLineItem.Index}: Invalid rfqLineItemId."
                });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var totalAmount = lineItems.Sum(item =>
            {
                var quantity = quantityMap[item.RfqLineItemId];
                return (item.UnitPrice ?? 0m) * quantity;
            });

            quote.TotalAmount = totalAmount;
            quote.Currency = selectedCurrency;
            quote.TaxStatus = normalizedQuoteTaxStatus ?? quote.TaxStatus ?? "inclusive";
            quote.PaymentTerms = string.IsNullOrWhiteSpace(paymentTerms) ? null : paymentTerms;
            quote.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
            quote.DeliveryTerms = string.IsNullOrWhiteSpace(deliveryTerms) ? null : deliveryTerms;
            quote.ShippingCountry = normalizedShippingCountry;
            quote.ShippingLocation = normalizedShippingLocation;
            quote.Version = (quote.Version ?? 0) + 1;
            quote.UpdatedAt = DateTime.UtcNow.ToString("o");

            var existingLineItems = await _dbContext.QuoteLineItems
                .Where(li => li.QuoteId == quoteId)
                .ToListAsync(cancellationToken);
            _dbContext.QuoteLineItems.RemoveRange(existingLineItems);

            foreach (var item in lineItems)
            {
                var quantity = quantityMap[item.RfqLineItemId];
                var totalPrice = (item.UnitPrice ?? 0m) * quantity;

                _dbContext.QuoteLineItems.Add(new QuoteLineItem
                {
                    QuoteId = quoteId,
                    RfqLineItemId = item.RfqLineItemId,
                    UnitPrice = item.UnitPrice,
                    MinimumOrderQuantity = item.MinimumOrderQuantity,
                    StandardPackageQuantity = item.StandardPackageQuantity,
                    TotalPrice = totalPrice,
                    Brand = item.Brand,
                    TaxStatus = string.IsNullOrWhiteSpace(item.TaxStatus) ? null : item.TaxStatus,
                    DeliveryPeriod = item.DeliveryPeriod,
                    Parameters = item.Parameters,
                    Notes = item.Notes,
                    ProductOrigin = item.ProductOrigin,
                    ProductGroup = item.ProductGroup,
                });
            }

            var storedFiles = await SaveAttachmentsAsync(attachments, id, cancellationToken);
            if (storedFiles.Count > 0)
            {
                var now = DateTime.UtcNow.ToString("o");
                long? uploadedBy = null;
                if (!string.IsNullOrWhiteSpace(user.Id) && long.TryParse(user.Id, out var parsedUserId))
                {
                    uploadedBy = parsedUserId;
                }
                foreach (var file in storedFiles)
                {
                    var fileSize = file.Size > int.MaxValue ? (int?)null : (int)file.Size;
                    _dbContext.QuoteAttachments.Add(new QuoteAttachment
                    {
                        QuoteId = quoteId,
                        OriginalName = file.OriginalName,
                        StoredName = file.StoredName,
                        FileType = file.ContentType,
                        FileSize = fileSize,
                        UploadedAt = now,
                        UploadedBy = uploadedBy,
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync("quote", quoteId.ToString(CultureInfo.InvariantCulture), "update",
                new { lineItemCount = lineItems.Count, totalAmount },
                user,
                cancellationToken);

            await _priceAuditService.UpsertQuoteAuditAsync(quote, quote.IpAddress, cancellationToken);

            var responseQuote = await BuildQuoteResponseAsync(quote, quote.Currency ?? DefaultCurrency, cancellationToken);
            return Ok(new
            {
                message = "Quote updated successfully",
                data = responseQuote,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update quote.");
            return StatusCode(500, new { message = "Failed to update quote." });
        }
    }

    [HttpPut("{id:int}/quotes/{quoteId:int}/withdraw")]
    public async Task<IActionResult> WithdrawQuote(int id, int quoteId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (id <= 0 || quoteId <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ or quote ID." });
        }

        var user = HttpContext.GetAuthUser();
        if (user?.SupplierId == null)
        {
            return StatusCode(403, new { message = "Only suppliers can withdraw quotes." });
        }

        try
        {
            var rfq = await _dbContext.Rfqs.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var rfqStatus = rfq.Status ?? string.Empty;
            if (!string.Equals(rfqStatus, "published", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rfqStatus, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "RFQ is not accepting quote withdrawals." });
            }

            var quote = await _dbContext.Quotes
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.RfqId == id && q.SupplierId == user.SupplierId, cancellationToken);
            if (quote == null)
            {
                return NotFound(new { message = "Quote not found or access denied." });
            }

            quote.Status = "withdrawn";
            quote.UpdatedAt = DateTime.UtcNow.ToString("o");
            quote.IpAddress = HttpContext.GetClientIp();

            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogAuditAsync("quote", quote.Id.ToString(CultureInfo.InvariantCulture), "withdraw",
                new { rfqId = id }, user, cancellationToken);

            return Ok(new { message = "Quote withdrawn successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to withdraw quote.");
            return StatusCode(500, new { message = "Failed to withdraw quote." });
        }
    }
}
