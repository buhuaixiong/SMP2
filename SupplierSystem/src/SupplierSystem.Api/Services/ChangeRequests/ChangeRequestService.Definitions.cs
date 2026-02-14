using System.Linq;
using System.Text.Json;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Services.ChangeRequests;

public sealed partial class ChangeRequestService
{
    private const string ChangeTypeRequired = "profile_update_required";
    private const string ChangeTypeOptional = "profile_update_optional";

    private static readonly HashSet<string> LegacyRequiredTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "profile_update",
        ChangeTypeRequired
    };

    private static readonly IReadOnlyList<ChangeRequestWorkflowStep> RequiredWorkflow =
    [
        new("purchaser", "采购员审批", Permissions.PurchaserUpgradeInit, ["purchaser"]),
        new("quality_manager", "品质审批", Permissions.QualityManagerUpgradeReview, ["quality_manager"]),
        new("procurement_manager", "采购经理审批", Permissions.ProcurementManagerUpgradeApprove, ["procurement_manager"]),
        new("procurement_director", "采购总监审批", Permissions.ProcurementDirectorProcessException, ["procurement_director"]),
        new("finance_director", "财务总监审批", Permissions.FinanceDirectorCompliance, ["finance_director"]),
    ];

    private static readonly IReadOnlyList<ChangeRequestWorkflowStep> OptionalWorkflow =
    [
        new("purchaser", "采购员确认", Permissions.PurchaserUpgradeInit, ["purchaser"]),
    ];

    private static readonly IReadOnlyList<IReadOnlyList<ChangeRequestWorkflowStep>> AllWorkflows =
    [
        RequiredWorkflow,
        OptionalWorkflow,
    ];

    private static readonly IReadOnlyList<ChangeRequestFieldDefinition> RequiredFields =
    [
        new("companyName", "Company name"),
        new("companyId", "Supplier code"),
        new("contactPerson", "Primary contact name"),
        new("contactPhone", "Primary contact phone"),
        new("contactEmail", "Primary contact email"),
        new("category", "Supplier category"),
        new("address", "Registered address"),
        new("businessRegistrationNumber", "Business registration number"),
        new("paymentTerms", "Payment terms"),
        new("paymentCurrency", "Payment currency"),
        new("bankAccount", "Bank account"),
        new("region", "Region"),
    ];

    private static readonly IReadOnlyDictionary<string, string> OptionalFieldLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["notes"] = "备注",
            ["serviceCategory"] = "服务类别",
            ["financialContact"] = "财务联系人",
        };

    private static readonly HashSet<string> RequiredFieldKeys =
        new(RequiredFields.Select(field => field.Key), StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> RequiredFieldLabels =
        RequiredFields.ToDictionary(field => field.Key, field => field.Label, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> HighRiskFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "companyName",
            "businessRegistrationNumber",
            "bankAccount",
        };

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
