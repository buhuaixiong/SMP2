using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Workflows;

namespace SupplierSystem.Api.Services.TempSuppliers;

public sealed partial class TempSupplierUpgradeService
{
    internal static readonly IReadOnlyList<UpgradeRequirementDefinition> RequiredDocuments = new List<UpgradeRequirementDefinition>
    {
        new UpgradeRequirementDefinition(
            "quality_compensation_agreement",
            "\u8d28\u91cf\u8d54\u507f\u534f\u8bae",
            "\u786e\u4fdd\u7b7e\u7f72\u7684\u8d28\u91cf\u8d54\u507f\u6761\u6b3e\u53cc\u65b9\u7559\u5b58\u3002",
            true),
        new UpgradeRequirementDefinition(
            "incoming_packaging_transport_agreement",
            "\u6765\u6599\u5305\u88c5\u8fd0\u8f93\u534f\u8bae",
            "\u7ea6\u5b9a\u5305\u88c5\u89c4\u8303\u53ca\u8fd0\u8f93\u8981\u6c42\u3002",
            true),
        new UpgradeRequirementDefinition(
            "quality_assurance_agreement",
            "\u8d28\u91cf\u4fdd\u8bc1\u534f\u8bae",
            "\u660e\u786e\u8d28\u91cf\u5f02\u5e38\u5904\u7406\u6d41\u7a0b\u3002",
            true),
        new UpgradeRequirementDefinition(
            "quality_kpi_targets",
            "\u8d28\u91cfKPI\u76ee\u6807",
            "\u63d0\u4ea4\u6700\u65b0\u7684\u8d28\u91cfKPI\u6307\u6807\u53ca\u627f\u8bfa\u3002",
            true),
        new UpgradeRequirementDefinition(
            "supplier_handbook_template",
            "\u4f9b\u5e94\u5546\u624b\u518c\u6a21\u677f",
            "\u63d0\u4f9b\u6700\u65b0\u7684\u4f9b\u5e94\u5546\u624b\u518c\u7248\u672c\u3002",
            true),
        new UpgradeRequirementDefinition(
            "supplemental_agreement",
            "\u8865\u5145\u534f\u8bae",
            "\u8bb0\u5f55\u53cc\u65b9\u7ea6\u5b9a\u7684\u9644\u52a0\u6761\u6b3e\uff0c\u53ef\u9009\u4e0a\u4f20\u3002",
            false),
    };

    public async Task<object> GetUpgradeRequirementsAsync(CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);
        var templates = await BuildTemplateMapAsync(cancellationToken);

        var requirements = RequiredDocuments
            .Select(doc => new UpgradeRequirementItem
            {
                Code = doc.Code,
                Name = doc.Name,
                Description = doc.Description,
                Required = doc.Required,
                Template = templates.TryGetValue(doc.Code, out var template) ? template : null
            })
            .ToList();

        var workflow = TemporarySupplierUpgradeWorkflow.Definition.Steps
            .Select((step, index) => new UpgradeWorkflowStepItem
            {
                Key = step.Key,
                Label = step.Label,
                Permission = step.Permission,
                Order = index + 1
            })
            .ToList();

        return new
        {
            requiredDocuments = requirements,
            workflow
        };
    }

    private async Task<Dictionary<string, object>> BuildTemplateMapAsync(CancellationToken cancellationToken)
    {
        var templates = await _dbContext.TemplateDocuments
            .AsNoTracking()
            .Where(item => item.IsActive)
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            var entry = new
            {
                id = template.Id,
                templateCode = template.TemplateCode,
                templateName = template.TemplateName,
                description = template.Description,
                originalName = template.OriginalName,
                downloadUrl = $"/uploads/templates/{template.StoredName}"
            };
            map[template.TemplateCode] = entry;
        }

        return map;
    }
}
