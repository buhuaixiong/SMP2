using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class SettlementsController
{
    [HttpPost("monthly-statements")]
    public async Task<IActionResult> CreateMonthlyStatements([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_accountant", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var year = ReadInt(body, "year");
        var month = ReadInt(body, "month");
        if (!year.HasValue || !month.HasValue)
        {
            return BadRequest(new { message = "year and month are required." });
        }

        if (month < 1 || month > 12)
        {
            return BadRequest(new { message = "month must be between 1 and 12." });
        }

        var startDate = new DateTime(year.Value, month.Value, 1);
        var endDate = startDate.AddMonths(1);

        var supplierIds = ReadIntArray(body, "supplier_ids", "supplierIds");
        var suppliers = await _settlementStore.LoadEligibleSuppliersAsync(supplierIds, cancellationToken);

        if (suppliers.Count == 0)
        {
            return Ok(new
            {
                message = "No eligible suppliers found.",
                results = Array.Empty<object>(),
                failed_count = 0
            });
        }

        var results = new List<object>();
        var createdAt = DateTime.UtcNow;
        var createdBy = user?.Id;

        foreach (var supplier in suppliers)
        {
            try
            {
                var invoices = await _settlementStore.LoadVerifiedInvoicesAsync(
                    supplier.Id,
                    startDate,
                    endDate,
                    cancellationToken);

                var totalInvoices = invoices.Count;
                var totalAmount = invoices.Sum(i => i.Amount);
                var taxAmount = 0m;
                foreach (var invoice in invoices)
                {
                    var rate = ParseTaxRate(invoice.TaxRate);
                    if (rate <= 0)
                    {
                        continue;
                    }

                    taxAmount += invoice.Amount * rate;
                }

                var grandTotal = totalAmount + taxAmount;
                var statementNumber = $"STM-{year:0000}{month:00}-{supplier.Id}";

                var details = new
                {
                    invoices = invoices.Select(i => new
                    {
                        id = i.Id,
                        invoice_number = i.InvoiceNumber,
                        amount = i.Amount,
                        invoice_date = i.InvoiceDate.ToString("yyyy-MM-dd"),
                        rfq_id = i.RfqId,
                        rfq_title = i.RfqTitle
                    }),
                    supplier_info = new
                    {
                        name = supplier.CompanyName,
                        bank_account = supplier.BankAccount,
                        contact_email = supplier.ContactEmail
                    }
                };

                var settlement = new Settlement
                {
                    StatementNumber = statementNumber,
                    SupplierId = supplier.Id,
                    Type = "monthly",
                    PeriodStart = startDate.ToString("yyyy-MM-dd"),
                    PeriodEnd = endDate.ToString("yyyy-MM-dd"),
                    TotalInvoices = totalInvoices,
                    TotalAmount = totalAmount,
                    TaxAmount = taxAmount,
                    GrandTotal = grandTotal,
                    Status = "draft",
                    CreatedBy = createdBy,
                    CreatedAt = createdAt,
                    Details = JsonSerializer.Serialize(details)
                };

                _settlementStore.AddSettlement(settlement);
                await _settlementStore.SaveChangesAsync(cancellationToken);

                results.Add(new
                {
                    statement_number = statementNumber,
                    supplier_name = supplier.CompanyName,
                    total_invoices = totalInvoices,
                    grand_total = grandTotal,
                    settlement_id = settlement.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Settlements] Failed to generate statement for supplier {SupplierId}.", supplier.Id);
            }
        }

        return Ok(new
        {
            message = $"Successfully generated {results.Count} supplier statement(s).",
            results,
            failed_count = suppliers.Count - results.Count
        });
    }

    private static decimal ParseTaxRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var trimmed = value.Trim();
        var hasPercent = trimmed.EndsWith("%", StringComparison.Ordinal);
        if (hasPercent)
        {
            trimmed = trimmed[..^1].Trim();
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) &&
            !decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
        {
            return 0m;
        }

        if (hasPercent || parsed > 1m)
        {
            parsed /= 100m;
        }

        return parsed < 0 ? 0m : parsed;
    }
}
