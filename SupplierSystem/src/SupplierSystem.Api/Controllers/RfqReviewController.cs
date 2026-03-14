using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.Services.Rfq;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rfq")]
[ServiceFilter(typeof(LegacyContractDeprecationFilter))]
public sealed class RfqReviewController : NodeControllerBase
{
    private readonly RfqService _rfqService;
    private readonly RfqQuoteService _rfqQuoteService;

    public RfqReviewController(RfqService rfqService, RfqQuoteService rfqQuoteService, IWebHostEnvironment environment) : base(environment)
    {
        _rfqService = rfqService;
        _rfqQuoteService = rfqQuoteService;
    }

    [HttpGet("{id:int}/price-comparison")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ComparePrices(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var comparison = await _rfqQuoteService.ComparePricesAsync(id, user!, cancellationToken);
            return Success(comparison);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("{id:int}/review")]
    [RfqValidationFilter(RfqValidationScenario.Review)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReviewRfq(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = new ReviewRfqRequest
        {
            SelectedQuoteId = JsonHelper.GetInt(body, "selectedQuoteId"),
            Comments = JsonHelper.GetString(body, "comments"),
            ReviewScoresJson = JsonHelper.TryGetProperty(body, "reviewScores", out var scoreElement)
                ? scoreElement.GetRawText()
                : null,
        };

        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqService.ReviewAsync(id, request, user!, cancellationToken);
            return Success(rfq, 200, "RFQ review completed successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
