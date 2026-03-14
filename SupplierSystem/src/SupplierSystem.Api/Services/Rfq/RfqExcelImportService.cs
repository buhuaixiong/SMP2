using System.Globalization;
using System.Text.RegularExpressions;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.Services.Excel;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class RfqExcelImportService
{
    private static readonly Regex EightDigitDate = new("^[0-9]{8}$", RegexOptions.Compiled);
    private static readonly Regex IsoDate = new("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", RegexOptions.Compiled);

    private readonly ExcelOpenXmlReader _excelReader;

    public RfqExcelImportService(ExcelOpenXmlReader excelReader)
    {
        _excelReader = excelReader;
    }

    public ImportExcelResult Parse(Stream stream, string sheetName, int headerRow)
    {
        var normalizedSheetName = string.IsNullOrWhiteSpace(sheetName) ? "PRBuyer" : sheetName.Trim();
        var normalizedHeaderRow = headerRow > 0 ? headerRow : 15;

        var sheet = _excelReader.ReadSheet(stream, normalizedSheetName);
        if (!sheet.Rows.TryGetValue(normalizedHeaderRow, out var headers))
        {
            throw new InvalidOperationException($"Header row {normalizedHeaderRow} not found in worksheet \"{normalizedSheetName}\".");
        }

        var mapping = BuildColumnMapping(headers);
        ValidateRequiredFields(mapping);

        var dataRows = sheet.Rows
            .Where(entry => entry.Key > normalizedHeaderRow)
            .OrderBy(entry => entry.Key)
            .Select(entry => entry.Value)
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        return ParseDataRows(dataRows, mapping, normalizedSheetName);
    }

    private static ImportExcelResult ParseDataRows(
        IReadOnlyList<List<string>> dataRows,
        ColumnMapping mapping,
        string sheetName)
    {
        if (dataRows.Count == 0)
        {
            throw new InvalidOperationException("Excel file does not contain any data rows.");
        }

        var firstRow = dataRows[0];
        var prNumber = GetCellValue(firstRow, mapping.PrNumber);
        var department = GetCellValue(firstRow, mapping.Dept);
        var currency = GetCellValue(firstRow, mapping.Currency);
        var expectDate = GetCellValue(firstRow, mapping.ExpectDate);

        if (string.IsNullOrWhiteSpace(currency))
        {
            currency = "CNY";
        }

        var requirements = new List<ImportExcelRequirement>();
        var vendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal totalAmount = 0m;

        foreach (var row in dataRows)
        {
            var itemDescription = GetCellValue(row, mapping.ItemDescription);
            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                continue;
            }

            var qty = ParseDecimal(GetCellValue(row, mapping.Qty));
            var unit = GetCellValue(row, mapping.Unit);
            if (string.IsNullOrWhiteSpace(unit))
            {
                unit = "EA";
            }

            var unitPrice = ParseDecimal(GetCellValue(row, mapping.UnitPrice));
            var amount = ParseDecimal(GetCellValue(row, mapping.Amount));
            var remark = GetCellValue(row, mapping.Remark);
            var eciPn = GetCellValue(row, mapping.EciPn);
            var vendorName = GetCellValue(row, mapping.Vendor);

            requirements.Add(new ImportExcelRequirement(
                itemDescription,
                qty,
                unit,
                string.IsNullOrWhiteSpace(eciPn) ? null : eciPn,
                unitPrice > 0m ? unitPrice : null,
                amount > 0m ? amount : null,
                string.IsNullOrWhiteSpace(remark) ? null : remark));

            totalAmount += amount;

            if (!string.IsNullOrWhiteSpace(vendorName))
            {
                vendors.Add(vendorName);
            }
        }

        var formattedExpectDate = FormatDate(expectDate);
        var requirementDate = string.IsNullOrWhiteSpace(formattedExpectDate)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : formattedExpectDate;

        var title = GenerateRfqTitle(prNumber, department, requirements);
        var description = $"从PR {prNumber} 导入，共 {requirements.Count} 个物料行";

        return new ImportExcelResult(
            title,
            department,
            requirementDate,
            currency,
            totalAmount,
            description,
            requirements,
            vendors.ToList(),
            prNumber,
            new ImportExcelMetadata(DateTime.UtcNow.ToString("o"), requirements.Count, sheetName));
    }

    private static ColumnMapping BuildColumnMapping(IReadOnlyList<string> headers)
    {
        return new ColumnMapping(
            FindColumnIndex(headers, new[] { "PR No.", "PR No", "PR编号" }),
            FindColumnIndex(headers, new[] { "Line", "行号", "序号" }),
            FindColumnIndex(headers, new[] { "Dept", "Department", "部门" }),
            FindColumnIndex(headers, new[] { "Item Description", "Description", "物料描述", "名称" }),
            FindColumnIndex(headers, new[] { "ECI P/N", "P/N", "物料编号" }),
            FindColumnIndex(headers, new[] { "PR Iss.Date", "Issue Date", "发行日期" }),
            FindColumnIndex(headers, new[] { "PR initiator", "Initiator", "发起人" }),
            FindColumnIndex(headers, new[] { "Qty.", "Qty", "Quantity", "数量" }),
            FindColumnIndex(headers, new[] { "Unit", "单位" }),
            FindColumnIndex(headers, new[] { "Currency", "币种" }),
            FindColumnIndex(headers, new[] { "Unit price", " Unit price ", "单价" }),
            FindColumnIndex(headers, new[] { "Amount", " Amount ", "金额", "总额" }),
            FindColumnIndex(headers, new[] { "Vendor name", "Vendor", "供应商", "供应商名称" }),
            FindColumnIndex(headers, new[] { "Expect Date", "Expected Date", "期望日期", "交货日期" }),
            FindColumnIndex(headers, new[] { "Remark", "Note", "Notes", "备注" }));
    }

    private static void ValidateRequiredFields(ColumnMapping mapping)
    {
        var missing = new List<string>();

        if (mapping.PrNumber == -1)
        {
            missing.Add("PR编号 (PR No.)");
        }

        if (mapping.Dept == -1)
        {
            missing.Add("部门 (Dept)");
        }

        if (mapping.ItemDescription == -1)
        {
            missing.Add("物料描述 (Item Description)");
        }

        if (mapping.Qty == -1)
        {
            missing.Add("数量 (Qty.)");
        }

        if (mapping.Unit == -1)
        {
            missing.Add("单位 (Unit)");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Excel缺少必需字段: {string.Join(", ", missing)}");
        }
    }

    private static int FindColumnIndex(IReadOnlyList<string> headers, IEnumerable<string> possibleNames)
    {
        for (var i = 0; i < headers.Count; i += 1)
        {
            var header = headers[i]?.Trim() ?? string.Empty;
            if (header.Length == 0)
            {
                continue;
            }

            foreach (var name in possibleNames)
            {
                if (header.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string GetCellValue(IReadOnlyList<string> row, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= row.Count)
        {
            return string.Empty;
        }

        return row[columnIndex]?.Trim() ?? string.Empty;
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var sanitized = value.Replace(",", string.Empty, StringComparison.Ordinal);
        if (decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static string? FormatDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (EightDigitDate.IsMatch(trimmed))
        {
            return $"{trimmed[..4]}-{trimmed.Substring(4, 2)}-{trimmed.Substring(6, 2)}";
        }

        if (IsoDate.IsMatch(trimmed))
        {
            return trimmed;
        }

        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate))
        {
            try
            {
                return DateTime.FromOADate(oaDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParse(trimmed, out parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static string GenerateRfqTitle(
        string prNumber,
        string department,
        IReadOnlyList<ImportExcelRequirement> requirements)
    {
        var itemsSummary = requirements
            .Take(3)
            .Select(item =>
                item.ItemName.Length > 20 ? item.ItemName[..20] : item.ItemName)
            .ToList();

        var summaryText = string.Join("、", itemsSummary);
        var suffix = requirements.Count > 3 ? "等" : string.Empty;

        return $"[PR: {prNumber}] {department} - {summaryText}{suffix}";
    }

    private sealed record ColumnMapping(
        int PrNumber,
        int Line,
        int Dept,
        int ItemDescription,
        int EciPn,
        int IssueDate,
        int Initiator,
        int Qty,
        int Unit,
        int Currency,
        int UnitPrice,
        int Amount,
        int Vendor,
        int ExpectDate,
        int Remark);
}
