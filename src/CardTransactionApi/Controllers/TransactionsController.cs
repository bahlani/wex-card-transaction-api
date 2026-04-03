using CardTransactionApi.Data;
using CardTransactionApi.Dtos;
using CardTransactionApi.Models;
using CardTransactionApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardTransactionApi.Controllers;

[ApiController]
[Route("api")]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IExchangeRateService _exchangeRateService;

    public TransactionsController(AppDbContext db, IExchangeRateService exchangeRateService)
    {
        _db = db;
        _exchangeRateService = exchangeRateService;
    }

    /// <summary>
    /// Requirement #2: Store a purchase transaction associated with a specific card.
    /// </summary>
    [HttpPost("cards/{cardId}/transactions")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> CreateTransaction(
        Guid cardId,
        [FromBody] CreateTransactionRequest request)
    {
        var card = await _db.Cards.FindAsync(cardId);

        if (card is null)
        {
            return NotFound(new { errorCode = "CARD_NOT_FOUND", error = $"Card with ID '{cardId}' not found." });
        }

        // Design decision: reject transactions exceeding the available balance.
        // The spec defines balance as credit limit minus transactions but doesn't explicitly
        // prevent overspending. We enforce it here as a sensible credit card constraint.
        // SQLite doesn't support SUM on decimal, so we fetch amounts and sum client-side.
        // Only the Amount column is selected — not full transaction rows.
        var amounts = await _db.Transactions
            .Where(t => t.CardId == cardId)
            .Select(t => t.Amount)
            .ToListAsync();
        var totalSpent = amounts.Sum();
        var availableBalance = card.CreditLimit - totalSpent;

        if (request.Amount > availableBalance)
        {
            return BadRequest(new
            {
                errorCode = "INSUFFICIENT_BALANCE",
                error = $"Transaction amount ${request.Amount:F2} exceeds available balance ${availableBalance:F2}."
            });
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            Description = request.Description,
            TransactionDate = request.TransactionDate,
            Amount = request.Amount
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();

        var response = new TransactionResponse
        {
            Id = transaction.Id,
            CardId = transaction.CardId,
            Description = transaction.Description,
            TransactionDate = transaction.TransactionDate,
            Amount = transaction.Amount
        };

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, response);
    }

    /// <summary>
    /// Requirement #3: Retrieve a purchase transaction, optionally converted to a specified currency.
    /// Uses exchange rate on or before the transaction date, within 6 months.
    /// </summary>
    /// <param name="id">The transaction ID.</param>
    /// <param name="currency">Country-currency from Treasury API, e.g. "Canada-Dollar", "United Kingdom-Pound", "Euro Zone-Euro". Call GET /api/currencies for the full list.</param>
    [HttpGet("transactions/{id}")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetTransaction(Guid id, [FromQuery] string? currency = null)
    {
        var transaction = await _db.Transactions.FindAsync(id);

        if (transaction is null)
        {
            return NotFound(new { errorCode = "TRANSACTION_NOT_FOUND", error = $"Transaction with ID '{id}' not found." });
        }

        // If no currency specified, return the transaction in its original currency
        if (string.IsNullOrWhiteSpace(currency))
        {
            return Ok(new TransactionResponse
            {
                Id = transaction.Id,
                CardId = transaction.CardId,
                Description = transaction.Description,
                TransactionDate = transaction.TransactionDate,
                Amount = transaction.Amount
            });
        }

        // Convert to specified currency using rate on or before transaction date (within 6 months)
        decimal? exchangeRate;
        try
        {
            exchangeRate = await _exchangeRateService.GetExchangeRateForDateAsync(
                currency, transaction.TransactionDate);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new
            {
                errorCode = "CURRENCY_CONVERSION_UNAVAILABLE",
                error = "Currency conversion is temporarily unavailable. Please try again later."
            });
        }

        if (exchangeRate is null)
        {
            return BadRequest(new
            {
                errorCode = "EXCHANGE_RATE_NOT_FOUND",
                error = $"The purchase cannot be converted to the target currency '{currency}'. " +
                        $"No exchange rate is available within 6 months on or before the transaction date " +
                        $"({transaction.TransactionDate:yyyy-MM-dd})."
            });
        }

        var convertedAmount = Math.Round(transaction.Amount * exchangeRate.Value, 2);

        return Ok(new ConvertedTransactionResponse
        {
            Id = transaction.Id,
            Description = transaction.Description,
            TransactionDate = transaction.TransactionDate,
            OriginalAmount = transaction.Amount,
            ExchangeRate = exchangeRate.Value,
            ConvertedAmount = convertedAmount,
            Currency = currency
        });
    }
}
