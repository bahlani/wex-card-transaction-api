using CardTransactionApi.Dtos;
using CardTransactionApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CardTransactionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CurrenciesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;

    public CurrenciesController(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    /// <summary>
    /// Returns the list of supported country-currency values from the Treasury API.
    /// Use these values for the currency query parameter on other endpoints.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<List<string>>> GetCurrencies()
    {
        try
        {
            var currencies = await _exchangeRateService.GetAvailableCurrenciesAsync();
            return Ok(currencies);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new ErrorResponse("CURRENCY_CONVERSION_UNAVAILABLE",
                "Currency conversion is temporarily unavailable. Please try again later."));
        }
    }
}
