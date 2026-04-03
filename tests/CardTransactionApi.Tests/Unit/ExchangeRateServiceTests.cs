using System.Net;
using System.Text.Json;
using CardTransactionApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardTransactionApi.Tests.Unit;

public class ExchangeRateServiceTests
{
    private readonly Mock<ILogger<ExchangeRateService>> _loggerMock = new();

    private ExchangeRateService CreateServiceWithResponse(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(jsonResponse, statusCode);
        var httpClient = new HttpClient(handler);
        return new ExchangeRateService(httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task GetExchangeRateForDateAsync_WithValidResponse_ReturnsRate()
    {
        var json = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { exchange_rate = "0.920", record_date = "2024-06-30", country_currency_desc = "Euro Zone-Euro" }
            }
        });

        var service = CreateServiceWithResponse(json);
        var rate = await service.GetExchangeRateForDateAsync("Euro Zone-Euro", new DateTime(2024, 7, 15));

        Assert.NotNull(rate);
        Assert.Equal(0.920m, rate.Value);
    }

    [Fact]
    public async Task GetExchangeRateForDateAsync_WithEmptyData_ReturnsNull()
    {
        var json = JsonSerializer.Serialize(new { data = Array.Empty<object>() });

        var service = CreateServiceWithResponse(json);
        var rate = await service.GetExchangeRateForDateAsync("FakeCurrency", new DateTime(2024, 7, 15));

        Assert.Null(rate);
    }

    [Fact]
    public async Task GetLatestExchangeRateAsync_WithValidResponse_ReturnsRate()
    {
        var json = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { exchange_rate = "1.350", record_date = "2024-09-30", country_currency_desc = "United Kingdom-Pound" }
            }
        });

        var service = CreateServiceWithResponse(json);
        var rate = await service.GetLatestExchangeRateAsync("United Kingdom-Pound");

        Assert.NotNull(rate);
        Assert.Equal(1.350m, rate.Value);
    }

    [Fact]
    public async Task GetLatestExchangeRateAsync_WithEmptyResponse_ReturnsNull()
    {
        var json = JsonSerializer.Serialize(new { data = Array.Empty<object>() });

        var service = CreateServiceWithResponse(json);
        var rate = await service.GetLatestExchangeRateAsync("NonExistent");

        Assert.Null(rate);
    }

    [Fact]
    public async Task GetExchangeRateForDateAsync_WithInvalidRateString_ReturnsNull()
    {
        var json = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { exchange_rate = "not-a-number", record_date = "2024-06-30", country_currency_desc = "Euro Zone-Euro" }
            }
        });

        var service = CreateServiceWithResponse(json);
        var rate = await service.GetExchangeRateForDateAsync("Euro Zone-Euro", new DateTime(2024, 7, 15));

        Assert.Null(rate);
    }

    [Fact]
    public async Task GetAvailableCurrenciesAsync_WithValidResponse_ReturnsSortedDistinctList()
    {
        var json = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { country_currency_desc = "Japan-Yen", record_date = "2024-12-31" },
                new { country_currency_desc = "Canada-Dollar", record_date = "2024-12-31" },
                new { country_currency_desc = "Euro Zone-Euro", record_date = "2024-12-31" },
                new { country_currency_desc = "Canada-Dollar", record_date = "2024-09-30" }
            }
        });

        var service = CreateServiceWithResponse(json);
        var currencies = await service.GetAvailableCurrenciesAsync();

        Assert.Equal(3, currencies.Count);
        Assert.Equal("Canada-Dollar", currencies[0]);
        Assert.Equal("Euro Zone-Euro", currencies[1]);
        Assert.Equal("Japan-Yen", currencies[2]);
    }

    [Fact]
    public async Task GetAvailableCurrenciesAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        var json = JsonSerializer.Serialize(new { data = Array.Empty<object>() });

        var service = CreateServiceWithResponse(json);
        var currencies = await service.GetAvailableCurrenciesAsync();

        Assert.Empty(currencies);
    }
}

/// <summary>
/// A fake HTTP message handler that returns a pre-defined response.
/// Used to test HttpClient-based services without making real network calls.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseContent;
    private readonly HttpStatusCode _statusCode;

    public FakeHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseContent = responseContent;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
