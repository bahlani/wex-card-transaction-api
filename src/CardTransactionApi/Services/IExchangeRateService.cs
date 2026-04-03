namespace CardTransactionApi.Services;

/// <summary>
/// Service for retrieving currency exchange rates from the
/// Treasury Reporting Rates of Exchange API.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>
    /// Gets the exchange rate for a currency on or before a specific date,
    /// within the prior 6 months. Used for transaction conversion (Req #3).
    /// </summary>
    /// <param name="currency">The country-currency description (e.g., "Euro Zone-Euro", "Canada-Dollar").</param>
    /// <param name="transactionDate">The date of the transaction.</param>
    /// <returns>The exchange rate, or null if no rate is available within 6 months.</returns>
    Task<decimal?> GetExchangeRateForDateAsync(string currency, DateTime transactionDate);

    /// <summary>
    /// Gets the latest available exchange rate for a currency.
    /// Used for balance conversion (Req #4).
    /// </summary>
    /// <param name="currency">The country-currency description (e.g., "Euro Zone-Euro", "Canada-Dollar").</param>
    /// <returns>The latest exchange rate, or null if the currency is not found.</returns>
    Task<decimal?> GetLatestExchangeRateAsync(string currency);

    /// <summary>
    /// Gets the list of available country-currency descriptions from the Treasury API.
    /// </summary>
    /// <returns>A sorted list of country-currency descriptions (e.g., "Canada-Dollar", "Euro Zone-Euro").</returns>
    Task<List<string>> GetAvailableCurrenciesAsync();
}
