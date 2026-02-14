using System.IO.Compression;
using System.Xml.Linq;

namespace SupplierSystem.Api.Services.Excel;

public sealed record ExcelSheet(string Name, IReadOnlyDictionary<int, List<string>> Rows);

public sealed class ExcelOpenXmlReader
{
    private static readonly XNamespace SpreadsheetNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly XNamespace DocumentRelNs =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly XNamespace PackageRelNs =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    public ExcelSheet ReadSheet(Stream stream, string sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new InvalidOperationException("Sheet name is required.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var sheets = workbook
            .Root?
            .Element(SpreadsheetNs + "sheets")?
            .Elements(SpreadsheetNs + "sheet")
            .ToList() ?? new List<XElement>();

        var sheet = sheets.FirstOrDefault(element =>
            string.Equals(element.Attribute("name")?.Value, sheetName, StringComparison.OrdinalIgnoreCase));

        if (sheet == null)
        {
            var available = sheets
                .Select(element => element.Attribute("name")?.Value)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            throw new InvalidOperationException(
                $"Worksheet \"{sheetName}\" not found. Available: {string.Join(", ", available)}");
        }

        var relId = sheet.Attribute(DocumentRelNs + "id")?.Value;
        if (string.IsNullOrWhiteSpace(relId))
        {
            throw new InvalidOperationException("Worksheet relationship id is missing.");
        }

        var sheetPath = ResolveSheetPath(archive, relId);
        var sharedStrings = LoadSharedStrings(archive);

        var sheetDoc = LoadXml(archive, sheetPath);
        var rows = new Dictionary<int, List<string>>();

        foreach (var row in sheetDoc.Descendants(SpreadsheetNs + "row"))
        {
            var rowIndex = ParseRowIndex(row.Attribute("r")?.Value);
            if (rowIndex <= 0)
            {
                continue;
            }

            var values = new List<string>();
            foreach (var cell in row.Elements(SpreadsheetNs + "c"))
            {
                var columnIndex = ParseColumnIndex(cell.Attribute("r")?.Value);
                if (columnIndex < 0)
                {
                    continue;
                }

                while (values.Count <= columnIndex)
                {
                    values.Add(string.Empty);
                }

                values[columnIndex] = ReadCellValue(cell, sharedStrings);
            }

            rows[rowIndex] = values;
        }

        return new ExcelSheet(sheetName, rows);
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry == null)
        {
            throw new InvalidOperationException($"Excel part \"{path}\" is missing.");
        }

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string ResolveSheetPath(ZipArchive archive, string relationshipId)
    {
        var rels = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var relationship = rels
            .Root?
            .Elements(PackageRelNs + "Relationship")
            .FirstOrDefault(element => string.Equals(element.Attribute("Id")?.Value, relationshipId, StringComparison.Ordinal));

        var target = relationship?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("Worksheet relationship target is missing.");
        }

        var normalized = target.TrimStart('/');
        if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"xl/{normalized}";
        }

        return normalized.Replace("\\", "/", StringComparison.Ordinal);
    }

    private static List<string> LoadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
        {
            return new List<string>();
        }

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        return doc
            .Descendants(SpreadsheetNs + "si")
            .Select(element => string.Concat(element.Descendants(SpreadsheetNs + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var cellType = cell.Attribute("t")?.Value;

        if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return ReadInlineString(cell);
        }

        var valueElement = cell.Element(SpreadsheetNs + "v");
        var rawValue = valueElement?.Value ?? string.Empty;

        if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(rawValue, out var index) && index >= 0 && index < sharedStrings.Count)
            {
                return sharedStrings[index];
            }

            return string.Empty;
        }

        if (string.Equals(cellType, "b", StringComparison.OrdinalIgnoreCase))
        {
            return rawValue == "1" ? "TRUE" : "FALSE";
        }

        if (string.Equals(cellType, "str", StringComparison.OrdinalIgnoreCase))
        {
            return rawValue;
        }

        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            return rawValue;
        }

        return ReadInlineString(cell);
    }

    private static string ReadInlineString(XElement cell)
    {
        var inline = cell.Element(SpreadsheetNs + "is");
        if (inline == null)
        {
            return string.Empty;
        }

        return string.Concat(inline.Descendants(SpreadsheetNs + "t").Select(text => text.Value));
    }

    private static int ParseColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return -1;
        }

        var index = 0;
        foreach (var ch in cellReference)
        {
            if (char.IsDigit(ch))
            {
                break;
            }

            if (ch is < 'A' or > 'Z')
            {
                continue;
            }

            index = (index * 26) + (ch - 'A' + 1);
        }

        return index > 0 ? index - 1 : -1;
    }

    private static int ParseRowIndex(string? value)
    {
        return int.TryParse(value, out var index) ? index : -1;
    }
}
