using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SupplierSystem.Api.Models.Requisitions;

public sealed class RequisitionCreateForm
{
    [FromForm(Name = "requesting_department")]
    public string? RequestingDepartment { get; set; }

    [FromForm(Name = "required_date")]
    public string? RequiredDate { get; set; }

    [FromForm(Name = "priority")]
    public string? Priority { get; set; }

    [FromForm(Name = "notes")]
    public string? Notes { get; set; }

    [FromForm(Name = "items")]
    public string? Items { get; set; }

    [FromForm(Name = "pr_number")]
    public string? PrNumber { get; set; }

    [FromForm(Name = "attachments")]
    public List<IFormFile> Attachments { get; set; } = new();
}
