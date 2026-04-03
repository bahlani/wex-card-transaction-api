using System.Net;
using System.Net.Http.Json;
using CardTransactionApi.Dtos;
using Moq;

namespace CardTransactionApi.Tests.Integration;

public class CardsControllerTests : IDisposable
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
    public async Task CreateCard_WithValidCreditLimit_ReturnsCreatedCard()
    {
        SetupFactory();
        var request = new CreateCardRequest { CreditLimit = 5000.00m };

        var response = await _client.PostAsJsonAsync("/api/cards", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.NotNull(card);
        Assert.NotEqual(Guid.Empty, card.Id);
        Assert.Equal(5000.00m, card.CreditLimit);
    }

    [Fact]
    public async Task CreateCard_WithZeroCreditLimit_ReturnsBadRequest()
    {
        SetupFactory();
        var request = new CreateCardRequest { CreditLimit = 0m };

        var response = await _client.PostAsJsonAsync("/api/cards", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCard_WithNegativeCreditLimit_ReturnsBadRequest()
    {
        SetupFactory();
        var request = new CreateCardRequest { CreditLimit = -100m };

        var response = await _client.PostAsJsonAsync("/api/cards", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCard_WithEmptyBody_ReturnsBadRequest()
    {
        SetupFactory();
        var response = await _client.PostAsync("/api/cards",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCard_WithNullBody_ReturnsBadRequest()
    {
        SetupFactory();
        var response = await _client.PostAsync("/api/cards",
            new StringContent("", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_ForNonExistentCard_ReturnsNotFound()
    {
        SetupFactory();
        var response = await _client.GetAsync($"/api/cards/{Guid.NewGuid()}/balance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_WithNoTransactions_ReturnsCreditLimitAsBalance()
    {
        SetupFactory();

        // Create a card
        var createResponse = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = 1000m });
        createResponse.EnsureSuccessStatusCode();
        var card = await createResponse.Content.ReadFromJsonAsync<CardResponse>();

        // Get balance
        var response = await _client.GetAsync($"/api/cards/{card!.Id}/balance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.Equal(1000m, balance.CreditLimit);
        Assert.Equal(0m, balance.TotalSpent);
        Assert.Equal(1000m, balance.AvailableBalance);
        Assert.Equal("USD", balance.Currency);
    }

    [Fact]
    public async Task GetBalance_WithTransactions_ReturnsCorrectAvailableBalance()
    {
        SetupFactory();

        // Create a card
        var createResponse = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = 1000m });
        createResponse.EnsureSuccessStatusCode();
        var card = await createResponse.Content.ReadFromJsonAsync<CardResponse>();

        // Add transactions
        var t1 = await _client.PostAsJsonAsync($"/api/cards/{card!.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "Purchase 1",
                TransactionDate = DateTime.UtcNow,
                Amount = 200m
            });
        t1.EnsureSuccessStatusCode();

        var t2 = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "Purchase 2",
                TransactionDate = DateTime.UtcNow,
                Amount = 300m
            });
        t2.EnsureSuccessStatusCode();

        // Get balance
        var response = await _client.GetAsync($"/api/cards/{card.Id}/balance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();

        Assert.NotNull(balance);
        Assert.Equal(1000m, balance.CreditLimit);
        Assert.Equal(500m, balance.TotalSpent);
        Assert.Equal(500m, balance.AvailableBalance);
    }

    [Fact]
    public async Task GetBalance_WithCurrencyConversion_ReturnsConvertedValues()
    {
        SetupFactory();

        _factory.MockExchangeRateService
            .Setup(s => s.GetLatestExchangeRateAsync("Euro Zone-Euro"))
            .ReturnsAsync(0.92m);

        // Create a card
        var createResponse = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = 1000m });
        createResponse.EnsureSuccessStatusCode();
        var card = await createResponse.Content.ReadFromJsonAsync<CardResponse>();

        // Add a transaction
        var t = await _client.PostAsJsonAsync($"/api/cards/{card!.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "Test",
                TransactionDate = DateTime.UtcNow,
                Amount = 200m
            });
        t.EnsureSuccessStatusCode();

        // Get balance in Euro
        var response = await _client.GetAsync($"/api/cards/{card.Id}/balance?currency=Euro Zone-Euro");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();

        Assert.NotNull(balance);
        Assert.Equal("Euro Zone-Euro", balance.Currency);
        Assert.Equal(0.92m, balance.ExchangeRate);
        Assert.Equal(920.00m, balance.ConvertedCreditLimit);
        Assert.Equal(184.00m, balance.ConvertedTotalSpent);
        Assert.Equal(736.00m, balance.ConvertedAvailableBalance);
    }

    [Fact]
    public async Task GetBalance_WithUnavailableCurrency_ReturnsBadRequest()
    {
        SetupFactory();

        _factory.MockExchangeRateService
            .Setup(s => s.GetLatestExchangeRateAsync("FakeCurrency"))
            .ReturnsAsync((decimal?)null);

        var createResponse = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = 1000m });
        createResponse.EnsureSuccessStatusCode();
        var card = await createResponse.Content.ReadFromJsonAsync<CardResponse>();

        var response = await _client.GetAsync($"/api/cards/{card!.Id}/balance?currency=FakeCurrency");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_WhenExternalServiceDown_Returns502WithErrorCode()
    {
        SetupFactory();

        _factory.MockExchangeRateService
            .Setup(s => s.GetLatestExchangeRateAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var createResponse = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = 1000m });
        createResponse.EnsureSuccessStatusCode();
        var card = await createResponse.Content.ReadFromJsonAsync<CardResponse>();

        var response = await _client.GetAsync($"/api/cards/{card!.Id}/balance?currency=Canada-Dollar");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("CURRENCY_CONVERSION_UNAVAILABLE", body!.ErrorCode);
    }
}
