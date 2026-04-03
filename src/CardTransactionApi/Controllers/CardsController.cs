using CardTransactionApi.Data;
using CardTransactionApi.Dtos;
using CardTransactionApi.Models;
using CardTransactionApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardTransactionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IExchangeRateService _exchangeRateService;

    public CardsController(AppDbContext db, IExchangeRateService exchangeRateService)
    {
        _db = db;
        _exchangeRateService = exchangeRateService;
    }

    /// <summary>
    /// Requirement #1: Create a Card with a credit limit.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CardResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CardResponse>> CreateCard([FromBody] CreateCardRequest request)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            CreditLimit = request.CreditLimit
        };

        _db.Cards.Add(card);
        await _db.SaveChangesAsync();

        var response = new CardResponse
        {
            Id = card.Id,
            CreditLimit = card.CreditLimit
        };

        return CreatedAtAction(nameof(GetBalance), new { id = card.Id }, response);
    }

    /// <summary>
    /// Requirement #4: Retrieve the available balance of a card, optionally converted to a specified currency.
    /// </summary>
    /// <param name="id">The card ID.</param>
    /// <param name="currency">Country-currency from Treasury API, e.g. "Canada-Dollar", "United Kingdom-Pound", "Euro Zone-Euro". Call GET /api/currencies for the full list.</param>
    [HttpGet("{id}/balance")]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BalanceResponse>> GetBalance(Guid id, [FromQuery] string? currency = null)
    {
        var card = await _db.Cards.FindAsync(id);

        if (card is null)
        {
            return NotFound(new ErrorResponse("CARD_NOT_FOUND", $"Card with ID '{id}' not found."));
        }

        // SQLite doesn't support SUM on decimal, so we fetch amounts and sum client-side.
        // Only the Amount column is selected — not full transaction rows.
        var amounts = await _db.Transactions
            .Where(t => t.CardId == id)
            .Select(t => t.Amount)
            .ToListAsync();
        var totalSpent = amounts.Sum();
        var availableBalance = card.CreditLimit - totalSpent;

        var response = new BalanceResponse
        {
            CardId = card.Id,
            CreditLimit = card.CreditLimit,
            TotalSpent = totalSpent,
            AvailableBalance = availableBalance,
            Currency = "USD"
        };

        // If a currency is specified, convert using the latest exchange rate
        if (!string.IsNullOrWhiteSpace(currency))
        {
            decimal? exchangeRate;
            try
            {
                exchangeRate = await _exchangeRateService.GetLatestExchangeRateAsync(currency);
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new ErrorResponse("CURRENCY_CONVERSION_UNAVAILABLE",
                    "Currency conversion is temporarily unavailable. Please try again later."));
            }

            if (exchangeRate is null)
            {
                return BadRequest(new ErrorResponse("EXCHANGE_RATE_NOT_FOUND", $"No exchange rate available for currency '{currency}'."));
            }

            response.Currency = currency;
            response.ExchangeRate = exchangeRate.Value;
            response.ConvertedCreditLimit = Math.Round(card.CreditLimit * exchangeRate.Value, 2);
            response.ConvertedTotalSpent = Math.Round(totalSpent * exchangeRate.Value, 2);
            response.ConvertedAvailableBalance = Math.Round(availableBalance * exchangeRate.Value, 2);
        }

        return Ok(response);
    }
}
