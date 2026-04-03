using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CardTransactionApi.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExchangeRateService> _logger;
    private const string BaseUrl = "https://api.fiscaldata.treasury.gov/services/api/fiscal_service/v1/accounting/od/rates_of_exchange";

    public ExchangeRateService(HttpClient httpClient, ILogger<ExchangeRateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal?> GetExchangeRateForDateAsync(string currency, DateTime transactionDate)
    {
        // Rate must be on or before the transaction date, within the prior 6 months
        var sixMonthsBefore = transactionDate.AddMonths(-6);
        var onOrBefore = transactionDate;

        var url = $"{BaseUrl}?fields=country_currency_desc,exchange_rate,record_date" +
                  $"&filter=country_currency_desc:eq:{Uri.EscapeDataString(currency)}" +
                  $",record_date:gte:{sixMonthsBefore:yyyy-MM-dd}" +
                  $",record_date:lte:{onOrBefore:yyyy-MM-dd}" +
                  $"&sort=-record_date" +
                  $"&page[size]=1";

        return await FetchExchangeRateAsync(url);
    }

    public async Task<decimal?> GetLatestExchangeRateAsync(string currency)
    {
        var url = $"{BaseUrl}?fields=country_currency_desc,exchange_rate,record_date" +
                  $"&filter=country_currency_desc:eq:{Uri.EscapeDataString(currency)}" +
                  $"&sort=-record_date" +
                  $"&page[size]=1";

        return await FetchExchangeRateAsync(url);
    }

    public async Task<List<string>> GetAvailableCurrenciesAsync()
    {
        try
        {
            // First, find the most recent record date
            var latestUrl = $"{BaseUrl}?fields=record_date" +
                            $"&sort=-record_date" +
                            $"&page[size]=1";

            _logger.LogInformation("Fetching latest record date from: {Url}", latestUrl);

            var latestResponse = await _httpClient.GetFromJsonAsync<TreasuryApiResponse>(latestUrl);

            if (latestResponse?.Data is null || latestResponse.Data.Count == 0)
            {
                return new List<string>();
            }

            var latestDate = latestResponse.Data[0].RecordDate;

            // Then, fetch all currencies for that date
            var url = $"{BaseUrl}?fields=country_currency_desc" +
                      $"&filter=record_date:eq:{latestDate}" +
                      $"&page[size]=1000";

            _logger.LogInformation("Fetching available currencies from: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<TreasuryApiResponse>(url);

            if (response?.Data is null || response.Data.Count == 0)
            {
                return new List<string>();
            }

            return response.Data
                .Select(r => r.CountryCurrencyDesc)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .Order()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available currencies from Treasury API.");
            throw;
        }
    }

    private async Task<decimal?> FetchExchangeRateAsync(string url)
    {
        try
        {
            _logger.LogInformation("Fetching exchange rate from: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<TreasuryApiResponse>(url);

            if (response?.Data is null || response.Data.Count == 0)
            {
                _logger.LogWarning("No exchange rate data returned from Treasury API.");
                return null;
            }

            var rateString = response.Data[0].ExchangeRate;
            if (decimal.TryParse(rateString, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                return rate;
            }

            _logger.LogWarning("Could not parse exchange rate value: {Rate}", rateString);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exchange rate from Treasury API.");
            throw;
        }
    }
}

public class TreasuryApiResponse
{
    [JsonPropertyName("data")]
    public List<TreasuryRateRecord> Data { get; set; } = new();
}

public class TreasuryRateRecord
{
    [JsonPropertyName("exchange_rate")]
    public string ExchangeRate { get; set; } = string.Empty;

    [JsonPropertyName("record_date")]
    public string RecordDate { get; set; } = string.Empty;

    [JsonPropertyName("country_currency_desc")]
    public string CountryCurrencyDesc { get; set; } = string.Empty;
}
