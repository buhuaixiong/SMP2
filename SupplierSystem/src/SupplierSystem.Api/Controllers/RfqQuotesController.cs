using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.Services.Rfq;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rfq")]
[ServiceFilter(typeof(LegacyContractDeprecationFilter))]
public sealed class RfqQuotesController : NodeControllerBase
{
    private readonly RfqQuoteService _rfqQuoteService;

    public RfqQuotesController(RfqQuoteService rfqQuoteService, IWebHostEnvironment environment) : base(environment)
    {
        _rfqQuoteService = rfqQuoteService;
    }

    [HttpPost("{id:int}/quotes")]
    [RfqValidationFilter(RfqValidationScenario.SubmitQuote)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitQuote(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = body.Deserialize<SubmitQuoteRequest>(CreateJsonOptions()) ?? new SubmitQuoteRequest();

        try
        {
            var user = HttpContext.GetAuthUser();
            var quote = await _rfqQuoteService.SubmitQuoteAsync(id, request, user!, HttpContext.GetClientIp(), cancellationToken);
            return SendCreated(quote, "Quote submitted successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{rfqId:int}/quotes/{quoteId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateQuote(int rfqId, int quoteId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = body.Deserialize<SubmitQuoteRequest>(CreateJsonOptions()) ?? new SubmitQuoteRequest();

        try
        {
            var user = HttpContext.GetAuthUser();
            var quote = await _rfqQuoteService.UpdateQuoteAsync(rfqId, quoteId, request, user!, cancellationToken);
            return Success(quote, 200, "Quote updated successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{rfqId:int}/quotes/{quoteId:int}/withdraw")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> WithdrawQuote(int rfqId, int quoteId, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var quote = await _rfqQuoteService.WithdrawQuoteAsync(rfqId, quoteId, user!, cancellationToken);
            return Success(quote, 200, "Quote withdrawn successfully");
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("quotes/{quoteId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetQuoteDetails(int quoteId, CancellationToken cancellationToken)
    {
        try
        {
            var user = HttpContext.GetAuthUser();
            var quote = await _rfqQuoteService.GetQuoteDetailsAsync(quoteId, user!, cancellationToken);
            return Success(quote);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }
}
