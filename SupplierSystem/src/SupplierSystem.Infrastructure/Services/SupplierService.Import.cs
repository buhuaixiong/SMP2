using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    private const string DefaultSupplierPassword = "666666";
    private const string DefaultSupplierRole = "temp_supplier";

    public async Task<SupplierImportResponse> ImportSuppliersFromExcelAsync(
        byte[] fileContent,
        string fileName,
        string importedBy,
        CancellationToken cancellationToken)
    {
        var summary = new SupplierImportSummary
        {
            SheetName = string.Empty,
            ScannedRows = 0,
            ImportedRows = 0,
            Created = 0,
            Updated = 0,
            Skipped = 0,
            PasswordResets = 0,
            Errors = new List<SupplierImportError>()
        };

        var results = new List<SupplierImportResult>();
        var parsedRows = new List<SupplierImportRow>();
        var seenCompanyIds = new HashSet<string>();

        // 解析Excel文件
        using var stream = new MemoryStream(fileContent);
        using var package = new ExcelPackage(stream);

        if (package.Workbook.Worksheets.Count == 0)
        {
            summary.Errors.Add(new SupplierImportError { Row = 0, Message = "The workbook does not contain any sheets." });
            return new SupplierImportResponse { Summary = summary, Results = results };
        }

        var worksheet = package.Workbook.Worksheets[0];
        summary.SheetName = worksheet.Name;

        // 获取表头
        var headerRow = 1;
        var headerMap = new Dictionary<string, int>();
        var maxColumn = worksheet.Dimension.End.Column;

        for (var col = 1; col <= maxColumn; col++)
        {
            var headerValue = worksheet.Cells[headerRow, col].Value?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(headerValue))
            {
                var normalizedHeader = NormalizeHeader(headerValue);
                headerMap[normalizedHeader] = col;
            }
        }

        // 解析数据行
        var startRow = headerRow + 1;
        var endRow = worksheet.Dimension.End.Row;

        for (var row = startRow; row <= endRow; row++)
        {
            summary.ScannedRows++;

            // 检查行是否为空
            var hasContent = false;
            for (var col = 1; col <= maxColumn; col++)
            {
                if (!string.IsNullOrEmpty(worksheet.Cells[row, col].Value?.ToString()))
                {
                    hasContent = true;
                    break;
                }
            }

            if (!hasContent)
            {
                continue;
            }

            try
            {
                var importRow = ParseRow(worksheet, row, headerMap);

                // 验证必填字段
                if (string.IsNullOrEmpty(importRow.CompanyId))
                {
                    summary.Errors.Add(new SupplierImportError { Row = row, Message = "Missing vendor code / company identifier." });
                    summary.Skipped++;
                    continue;
                }

                if (seenCompanyIds.Contains(importRow.CompanyId!))
                {
                    summary.Errors.Add(new SupplierImportError { Row = row, Message = $"Duplicate vendor code detected in file: {importRow.CompanyId}." });
                    summary.Skipped++;
                    continue;
                }

                if (string.IsNullOrEmpty(importRow.CompanyName))
                {
                    summary.Errors.Add(new SupplierImportError { Row = row, Message = "Missing supplier/vendor name." });
                    summary.Skipped++;
                    continue;
                }

                seenCompanyIds.Add(importRow.CompanyId!);
                importRow.RowNumber = row;
                parsedRows.Add(importRow);
            }
            catch (Exception ex)
            {
                summary.Errors.Add(new SupplierImportError { Row = row, Message = ex.Message });
                summary.Skipped++;
            }
        }

        if (parsedRows.Count == 0)
        {
            return new SupplierImportResponse { Summary = summary, Results = results };
        }

        // 执行数据库导入（使用事务）
        var passwordHash = HashPassword(DefaultSupplierPassword);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        await using var transaction = await _context.Database.BeginTransactionAsync(CancellationToken.None);

        try
        {
            foreach (var importRow in parsedRows)
            {
                try
                {
                    // 检查供应商是否已存在
                    var existingSupplier = await _context.Suppliers
                        .FirstOrDefaultAsync(s => s.CompanyId == importRow.CompanyId, CancellationToken.None);

                    int supplierId;
                    string action;
                    string? defaultPassword = null;

                    if (existingSupplier != null)
                    {
                        // 更新现有供应商
                        existingSupplier.CompanyName = importRow.CompanyName ?? existingSupplier.CompanyName;
                        existingSupplier.ContactPerson = importRow.ContactPerson ?? existingSupplier.ContactPerson;
                        existingSupplier.ContactPhone = importRow.ContactPhone ?? existingSupplier.ContactPhone;
                        existingSupplier.ContactEmail = importRow.ContactEmail ?? existingSupplier.ContactEmail;
                        existingSupplier.Category = importRow.Category ?? existingSupplier.Category;
                        existingSupplier.Address = importRow.Address ?? existingSupplier.Address;
                        existingSupplier.Notes = importRow.Notes ?? existingSupplier.Notes;
                        existingSupplier.PaymentTerms = importRow.PaymentTerms ?? existingSupplier.PaymentTerms;
                        existingSupplier.Region = importRow.Region ?? existingSupplier.Region;
                        existingSupplier.PaymentCurrency = importRow.PaymentCurrency ?? existingSupplier.PaymentCurrency;
                        existingSupplier.FaxNumber = importRow.FaxNumber ?? existingSupplier.FaxNumber;
                        existingSupplier.BusinessRegistrationNumber = importRow.BusinessRegistrationNumber ?? existingSupplier.BusinessRegistrationNumber;
                        existingSupplier.UpdatedAt = now;

                        await _context.SaveChangesAsync(CancellationToken.None);
                        supplierId = existingSupplier.Id;
                        action = "updated";
                        summary.Updated++;
                    }
                    else
                    {
                        // 创建新供应商
                        var newSupplier = new Supplier
                        {
                            CompanyName = importRow.CompanyName!,
                            CompanyId = importRow.CompanyId!,
                            ContactPerson = importRow.ContactPerson,
                            ContactPhone = importRow.ContactPhone,
                            ContactEmail = importRow.ContactEmail,
                            Category = importRow.Category,
                            Address = importRow.Address,
                            Status = "potential",
                            CreatedBy = importedBy,
                            CreatedAt = now,
                            UpdatedAt = now,
                            Notes = importRow.Notes,
                            PaymentTerms = importRow.PaymentTerms,
                            Region = importRow.Region,
                            PaymentCurrency = importRow.PaymentCurrency,
                            FaxNumber = importRow.FaxNumber,
                            BusinessRegistrationNumber = importRow.BusinessRegistrationNumber,
                            Stage = "info_pending"
                        };

                        _context.Suppliers.Add(newSupplier);
                        await _context.SaveChangesAsync(CancellationToken.None);
                        supplierId = newSupplier.Id;
                        action = "created";
                        defaultPassword = DefaultSupplierPassword;
                        summary.Created++;
                    }

                    // 创建或更新供应商用户账户
                    var userResult = await EnsureSupplierAccountAsync(supplierId, importRow.CompanyId!, importRow.CompanyName!, passwordHash, CancellationToken.None);

                    if (userResult.PasswordReset)
                    {
                        summary.PasswordResets++;
                    }

                    // 记录审计日志
                    await _auditService.LogAsync(new AuditEntry
                    {
                        EntityType = "supplier",
                        EntityId = supplierId.ToString(),
                        Action = action == "created" ? "import_create" : "import_update",
                        ActorId = importedBy,
                        ActorName = importedBy,
                        Changes = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            source = "excel",
                            rowNumber = importRow.RowNumber,
                            companyId = importRow.CompanyId,
                            action,
                            userAction = userResult.Action
                        })
                    });

                    if (userResult.Action == "created")
                    {
                        await _auditService.LogAsync(new AuditEntry
                        {
                            EntityType = "user",
                            EntityId = importRow.CompanyId,
                            Action = "import_user_create",
                            ActorId = importedBy,
                            ActorName = importedBy,
                            Changes = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                source = "excel",
                                rowNumber = importRow.RowNumber,
                                supplierId,
                                supplierCode = importRow.CompanyId,
                                role = DefaultSupplierRole,
                                passwordReset = userResult.PasswordReset
                            })
                        });
                    }

                    summary.ImportedRows++;
                    results.Add(new SupplierImportResult
                    {
                        RowNumber = importRow.RowNumber,
                        Action = action,
                        SupplierId = supplierId,
                        CompanyId = importRow.CompanyId,
                        CompanyName = importRow.CompanyName,
                        DefaultPassword = defaultPassword
                    });
                }
                catch (Exception ex)
                {
                    summary.Errors.Add(new SupplierImportError { Row = importRow.RowNumber, Message = ex.Message });
                    summary.Skipped++;
                }
            }

            await transaction.CommitAsync(CancellationToken.None);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new SupplierImportResponse { Summary = summary, Results = results };
    }

    private static string NormalizeHeader(string header)
    {
        if (header == null)
            return string.Empty;

        return header.Trim()
            .ToLower()
            .Replace("(", "")
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\t", "")
            .Replace("  ", " ")
            .Replace("__", "_")
            .Trim('_');
    }

    private static string GetCellValue(ExcelWorksheet worksheet, int row, int col)
    {
        var value = worksheet.Cells[row, col].Value;
        if (value == null)
            return string.Empty;

        if (value is DateTime dateTime)
            return dateTime.ToString("yyyy-MM-dd");

        return value.ToString()?.Trim() ?? string.Empty;
    }

    private static SupplierImportRow ParseRow(ExcelWorksheet worksheet, int row, Dictionary<string, int> headerMap)
    {
        var rowData = new SupplierImportRow();

        // 公司ID
        rowData.CompanyId = GetValue(worksheet, row, headerMap, new[] { "vendor_code", "vendor_no", "vendorid", "vendor_id", "supplier_code", "supplier_no", "supplierid", "code", "id", "vendor", "lifnr" });

        // 公司名称
        rowData.CompanyName = GetValue(worksheet, row, headerMap, new[] { "supplier_name", "vendor_name", "company_name", "name", "legal_name", "simple_name", "short_name", "vndnam", "vendor_name1", "name1" });

        // 联系人
        rowData.ContactPerson = GetValue(worksheet, row, headerMap, new[] { "contact", "contact_person", "contact_name", "primary_contact", "sales_contact", "vcon", "contactperson" });

        // 电话
        rowData.ContactPhone = GetValue(worksheet, row, headerMap, new[] { "contact_phone", "phone", "telephone", "tel", "mobile", "mobile_phone", "vphone", "tele1", "telephone1" });

        // 邮箱
        rowData.ContactEmail = GetValue(worksheet, row, headerMap, new[] { "contact_email", "email", "mail", "email_address" });

        // 类别
        rowData.Category = GetValue(worksheet, row, headerMap, new[] { "type", "supplier_type", "vendor_type", "category", "classification", "vtype", "account_group", "ktokk" });

        // 地址 (合并多个字段)
        rowData.Address = GetJoinedValue(worksheet, row, headerMap, new[] { "address", "address_1", "address_line_1", "address1", "address_line1", "address_2", "address_line_2", "address2", "address_line2", "city", "state", "province", "country", "postcode", "zip", "vndad1", "vndad2", "addrs3" });

        // 备注
        rowData.Notes = GetJoinedValue(worksheet, row, headerMap, new[] { "details", "detail", "notes", "note", "remarks", "remark", "vtmdsc", "vmuf10", "vmref2" });

        // 付款条款
        rowData.PaymentTerms = GetValue(worksheet, row, headerMap, new[] { "payment_term", "payment_terms", "terms_of_payment", "payment_terms_days", "payment_condition", "vterms", "vpayty", "zterm" });

        // 地区
        rowData.Region = GetValue(worksheet, row, headerMap, new[] { "region", "country", "area", "market", "ccdesc", "cname" });

        // 货币
        rowData.PaymentCurrency = GetValue(worksheet, row, headerMap, new[] { "payment_currency", "currency", "vcurr", "waers" });

        // 传真
        rowData.FaxNumber = GetValue(worksheet, row, headerMap, new[] { "fax", "fax_no", "fax_number", "vmvfax", "fax1" });

        // 税号
        rowData.BusinessRegistrationNumber = GetValue(worksheet, row, headerMap, new[] { "tax", "tax_no", "tax_number", "tax_id", "vat", "vat_no", "vat_number", "gst", "gst_number", "vtaxcd", "stcd1", "stceg" });

        return rowData;
    }

    private static string GetValue(ExcelWorksheet worksheet, int row, Dictionary<string, int> headerMap, string[] possibleHeaders)
    {
        foreach (var header in possibleHeaders)
        {
            if (headerMap.TryGetValue(header, out var col) && col > 0)
            {
                var value = GetCellValue(worksheet, row, col);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }
        return string.Empty;
    }

    private static string GetJoinedValue(ExcelWorksheet worksheet, int row, Dictionary<string, int> headerMap, string[] possibleHeaders)
    {
        var values = new List<string>();
        foreach (var header in possibleHeaders)
        {
            if (headerMap.TryGetValue(header, out var col) && col > 0)
            {
                var value = GetCellValue(worksheet, row, col);
                if (!string.IsNullOrEmpty(value))
                    values.Add(value);
            }
        }
        return string.Join(", ", values);
    }

    private async Task<(string Action, bool PasswordReset)> EnsureSupplierAccountAsync(
        int supplierRowId,
        string supplierCode,
        string supplierName,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        // 检查用户是否已存在
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == supplierCode, cancellationToken);

        if (existingUser == null)
        {
            // 创建新用户
            var newUser = new User
            {
                Id = supplierCode,
                Name = supplierName,
                Username = supplierCode,
                Role = DefaultSupplierRole,
                Password = passwordHash,
                SupplierId = supplierRowId
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync(cancellationToken);

            return ("created", true);
        }

        // 检查是否需要更新
        var needsUpdate =
            existingUser.Name != supplierName ||
            existingUser.Role != DefaultSupplierRole ||
            existingUser.SupplierId != supplierRowId;

        if (needsUpdate)
        {
            existingUser.Name = supplierName;
            existingUser.Role = DefaultSupplierRole;
            existingUser.SupplierId = supplierRowId;

            await _context.SaveChangesAsync(cancellationToken);
            return ("updated", false);
        }

        return ("unchanged", false);
    }

    private static string HashPassword(string password)
    {
        // 使用BCrypt哈希密码
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }
}
