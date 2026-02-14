using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Services.Rfq;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/tariffs")]
public sealed class TariffsController : NodeControllerBase
{
    private readonly TariffCalculationService _tariffService;

    public TariffsController(TariffCalculationService tariffService, IWebHostEnvironment environment) : base(environment)
    {
        _tariffService = tariffService;
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        try
        {
            var countries = await _tariffService.GetAvailableCountriesAsync(cancellationToken);
            var productGroups = await _tariffService.GetProductGroupsAsync(cancellationToken);

            var mappedCountries = countries.Select(country => new
            {
                code = country.TryGetValue("country_code", out var code) ? code?.ToString() ?? string.Empty : string.Empty,
                name = country.TryGetValue("country_name", out var name) ? name?.ToString() ?? string.Empty : string.Empty,
                nameZh = country.TryGetValue("country_name_zh", out var nameZh) ? nameZh?.ToString() : null,
            }).ToList();

            return Ok(new
            {
                data = new
                {
                    countries = mappedCountries,
                    productGroups = productGroups,
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to load tariff options", error = ex.Message });
        }
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var originalPrice = ReadDecimal(body, "originalPrice", "original_price");
        var shippingCountry = ReadString(body, "shippingCountry", "shipping_country");
        var productGroup = ReadString(body, "productGroup", "product_group");
        var productOrigin = ReadString(body, "productOrigin", "product_origin");
        var projectLocation = ReadString(body, "projectLocation", "project_location") ?? "HZ";
        var deliveryTerms = ReadString(body, "deliveryTerms", "delivery_terms") ?? string.Empty;
        var currency = ReadString(body, "currency") ?? "CNY";

        if (!originalPrice.HasValue || originalPrice.Value <= 0)
        {
            return BadRequest(new { message = "originalPrice must be a positive number" });
        }

        if (string.IsNullOrWhiteSpace(shippingCountry))
        {
            return BadRequest(new { message = "shippingCountry is required" });
        }

        if (string.IsNullOrWhiteSpace(productGroup))
        {
            return BadRequest(new { message = "productGroup is required" });
        }

        try
        {
            var result = await _tariffService.CalculateStandardCostAsync(
                originalPrice.Value,
                shippingCountry,
                productGroup,
                productOrigin,
                projectLocation,
                deliveryTerms,
                currency,
                cancellationToken);

            return Ok(new { data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to calculate tariff", error = ex.Message });
        }
    }

    private static decimal? ReadDecimal(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
            {
                return value.ToString();
            }
        }

        return null;
    }
}
