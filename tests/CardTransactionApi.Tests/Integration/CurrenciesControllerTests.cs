using System.Net;
using System.Net.Http.Json;
using CardTransactionApi.Dtos;
using Moq;

namespace CardTransactionApi.Tests.Integration;

public class CurrenciesControllerTests : IDisposable
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private void SetupFactory()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task GetCurrencies_ReturnsSortedList()
    {
        SetupFactory();

        _factory.MockExchangeRateService
            .Setup(s => s.GetAvailableCurrenciesAsync())
            .ReturnsAsync(new List<string> { "Canada-Dollar", "Euro Zone-Euro", "Japan-Yen" });

        var response = await _client.GetAsync("/api/currencies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var currencies = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(currencies);
        Assert.Equal(3, currencies.Count);
        Assert.Contains("Canada-Dollar", currencies);
        Assert.Contains("Euro Zone-Euro", currencies);
        Assert.Contains("Japan-Yen", currencies);
    }

    [Fact]
    public async Task GetCurrencies_WhenExternalServiceDown_Returns502WithErrorCode()
    {
        SetupFactory();

        _factory.MockExchangeRateService
            .Setup(s => s.GetAvailableCurrenciesAsync())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var response = await _client.GetAsync("/api/currencies");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("CURRENCY_CONVERSION_UNAVAILABLE", body!.ErrorCode);
    }
}
