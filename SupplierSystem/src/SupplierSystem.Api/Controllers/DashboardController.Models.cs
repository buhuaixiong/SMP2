using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class DashboardController
{
    private sealed class SupplierSummaryContext
    {
        public SupplierSummaryContext(Supplier supplier, List<SupplierDocument> documents, ComplianceSummary summary)
        {
            Supplier = supplier;
            Documents = documents;
            Summary = summary;
        }

        public Supplier Supplier { get; }
        public List<SupplierDocument> Documents { get; }
        public ComplianceSummary Summary { get; }
    }

    private sealed class ComplianceSummary
    {
        public List<MissingField> MissingProfileFields { get; init; } = new();
        public List<MissingDocumentType> MissingDocumentTypes { get; init; } = new();
    }

    private sealed class MissingField
    {
        public string Key { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }

    private sealed class MissingDocumentType
    {
        public string Type { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }

    private sealed class TodoItem
    {
        public string Type { get; init; } = string.Empty;
        public int Count { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Route { get; init; } = string.Empty;
        public string Priority { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
    }

    private sealed class ProfileRequirement
    {
        public ProfileRequirement(string key, string label, Func<Supplier, object?> valueSelector)
        {
            Key = key;
            Label = label;
            ValueSelector = valueSelector;
        }

        public string Key { get; }
        public string Label { get; }
        public Func<Supplier, object?> ValueSelector { get; }
    }

    private sealed class DocumentRequirement
    {
        public DocumentRequirement(string type, string label, IReadOnlyList<string>? aliases = null)
        {
            Type = type;
            Label = label;
            Aliases = aliases ?? Array.Empty<string>();
        }

        public string Type { get; }
        public string Label { get; }
        public IReadOnlyList<string> Aliases { get; }
    }
}
