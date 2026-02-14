using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Exceptions;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 临时账户

    public async Task<object> IssueTempAccountAsync(int supplierId, object request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { supplierId }, cancellationToken);
        if (supplier == null)
        {
            _logger.LogWarning("IssueTempAccountAsync failed - supplier not found: {SupplierId}", supplierId);
            throw new HttpResponseException(404, "Supplier not found.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(30);

        var tempAccount = new
        {
            supplierId,
            username = $"SUP{supplierId.ToString("D6")}",
            password = GenerateRandomPassword(12),
            status = "active",
            currency = "USD",
            sequenceNumber = 1,
            expiresAt = expiresAt.ToString("o"),
            issuedAt = now.ToString("o"),
            forceReissue = false
        };

        // 更新供应商的临时账户状态
        supplier.TempAccountStatus = "issued";
        supplier.TempAccountExpiresAt = expiresAt.ToString("o");
        await _context.SaveChangesAsync(cancellationToken);

        return tempAccount;
    }

    public async Task<object> FinalizeSupplierCodeAsync(int supplierId, object request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { supplierId }, cancellationToken);
        if (supplier == null)
        {
            _logger.LogWarning("FinalizeSupplierCodeAsync failed - supplier not found: {SupplierId}", supplierId);
            throw new HttpResponseException(404, "Supplier not found.");
        }

        // 生成供应商代码
        var supplierCode = $"SUP-{DateTime.Now:yyyyMM}-{supplierId.ToString("D4")}";
        supplier.SupplierCode = supplierCode;
        supplier.Stage = "active";

        await _context.SaveChangesAsync(cancellationToken);

        return new
        {
            supplierId,
            supplierCode,
            defaultPassword = "TempPass123!",
            stage = "active",
            finalizedAt = DateTime.UtcNow.ToString("o")
        };
    }

    #endregion

    private static string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
        var random = new Random();
        return new string(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
