using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Controllers;

public sealed partial class WarehouseReceiptsController
{
    private IActionResult? RequireAccess(AuthUser? user, bool allowSupplier, bool allowStaff)
    {
        if (user == null)
        {
            return Unauthorized();
        }

        if (allowStaff && HasAnyPermission(user, StaffPermissions))
        {
            return null;
        }

        if (allowSupplier && user.SupplierId.HasValue && HasAnyPermission(user, SupplierPermissions))
        {
            return null;
        }

        return Forbidden();
    }

    private static bool HasAnyPermission(AuthUser user, params string[] permissions)
    {
        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
    }

    private static bool TryParseDate(string? value, out DateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTime.TryParse(value, out parsed);
    }

    private sealed class WarehouseReceiptSummaryDto
    {
        public int Id { get; set; }

        [JsonPropertyName("reconciliation_id")]
        public int? ReconciliationId { get; set; }

        [JsonPropertyName("receipt_number")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [JsonPropertyName("receipt_date")]
        public string ReceiptDate { get; set; } = string.Empty;

        [JsonPropertyName("warehouse_location")]
        public string? WarehouseLocation { get; set; }

        [JsonPropertyName("received_by")]
        public string? ReceivedBy { get; set; }

        [JsonPropertyName("item_details")]
        public string? ItemDetails { get; set; }

        [JsonPropertyName("total_items")]
        public decimal TotalItems { get; set; }

        [JsonPropertyName("external_system_id")]
        public string? ExternalSystemId { get; set; }

        [JsonPropertyName("sync_status")]
        public string? SyncStatus { get; set; }

        [JsonPropertyName("last_sync_at")]
        public string? LastSyncAt { get; set; }

        [JsonPropertyName("sync_error")]
        public string? SyncError { get; set; }

        [JsonPropertyName("reconciliation_number")]
        public string? ReconciliationNumber { get; set; }

        public string? SupplierName { get; set; }
        public string? InvoiceNumber { get; set; }
    }

    private sealed class WarehouseReceiptLineItemDto
    {
        public int Id { get; set; }

        [JsonPropertyName("item_code")]
        public string? ItemCode { get; set; }

        [JsonPropertyName("item_name")]
        public string ItemName { get; set; } = string.Empty;

        [JsonPropertyName("specification")]
        public string? Specification { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("unit_price")]
        public decimal UnitPrice { get; set; }

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("tax_rate")]
        public decimal? TaxRate { get; set; }

        [JsonPropertyName("tax_amount")]
        public decimal? TaxAmount { get; set; }

        [JsonPropertyName("total_amount")]
        public decimal TotalAmount { get; set; }
    }

    private sealed class WarehouseReceiptDetailResponse
    {
        public WarehouseReceiptSummaryDto Receipt { get; set; } = new();
        public List<WarehouseReceiptLineItemDto> LineItems { get; set; } = new();
    }

    private sealed record ReceiptLineSummary(int WarehouseReceiptId, decimal Quantity, int LineCount);
}
