using Microsoft.Extensions.Logging;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 临时账户

    public async Task<TempAccountResponse> IssueTempAccountAsync(
        int supplierId,
        IssueTempAccountRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { supplierId }, cancellationToken);
        if (supplier == null)
        {
            _logger.LogWarning("IssueTempAccountAsync failed - supplier not found: {SupplierId}", supplierId);
            throw new HttpResponseException(404, "Supplier not found.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = ResolveExpiresAt(request, now);
        var password = string.IsNullOrWhiteSpace(request.Password)
            ? GenerateRandomPassword(12)
            : request.Password!;
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency!.Trim().ToUpperInvariant();

        var tempAccount = new TempAccountResponse
        {
            SupplierId = supplierId,
            Username = $"SUP{supplierId.ToString("D6")}",
            Password = password,
            Status = "active",
            Currency = currency,
            SequenceNumber = 1,
            ExpiresAt = expiresAt.ToString("o"),
            IssuedAt = now.ToString("o"),
            ForceReissue = request.ForceReissue,
        };

        // 更新供应商的临时账户状态
        supplier.TempAccountStatus = "issued";
        supplier.TempAccountExpiresAt = expiresAt.ToString("o");
        await _context.SaveChangesAsync(cancellationToken);

        return tempAccount;
    }

    public async Task<FinalizeSupplierCodeResponse> FinalizeSupplierCodeAsync(
        int supplierId,
        FinalizeSupplierCodeRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { supplierId }, cancellationToken);
        if (supplier == null)
        {
            _logger.LogWarning("FinalizeSupplierCodeAsync failed - supplier not found: {SupplierId}", supplierId);
            throw new HttpResponseException(404, "Supplier not found.");
        }

        var supplierCode = string.IsNullOrWhiteSpace(request.SupplierCode)
            ? $"SUP-{DateTime.UtcNow:yyyyMM}-{supplierId.ToString("D4")}"
            : request.SupplierCode.Trim().ToUpperInvariant();
        var defaultPassword = string.IsNullOrWhiteSpace(request.Password) ? "TempPass123!" : request.Password!;
        var stage = string.IsNullOrWhiteSpace(request.Stage) ? "active" : request.Stage.Trim();

        supplier.SupplierCode = supplierCode;
        supplier.Stage = stage;

        await _context.SaveChangesAsync(cancellationToken);

        return new FinalizeSupplierCodeResponse
        {
            SupplierId = supplierId,
            SupplierCode = supplierCode,
            DefaultPassword = defaultPassword,
            Stage = stage,
            FinalizedAt = DateTime.UtcNow.ToString("o")
        };
    }

    #endregion

    private static string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
        var random = new Random();
        return new string(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private static DateTime ResolveExpiresAt(IssueTempAccountRequest request, DateTime issuedAt)
    {
        if (!string.IsNullOrWhiteSpace(request.ExpiresAt) && DateTime.TryParse(request.ExpiresAt, out var explicitExpiry))
        {
            return explicitExpiry.ToUniversalTime();
        }

        var expiresInDays = request.ExpiresInDays.GetValueOrDefault(30);
        return issuedAt.AddDays(Math.Max(1, expiresInDays));
    }
}
