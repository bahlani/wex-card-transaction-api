using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CardTransactionApi.Dtos;
using Moq;

namespace CardTransactionApi.Tests.Integration;

public class ErrorResponse
{
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

public class TransactionsControllerTests : IDisposable
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

    private async Task<CardResponse> CreateTestCardAsync(decimal creditLimit = 5000m)
    {
        var response = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = creditLimit });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardResponse>())!;
    }

    [Fact]
    public async Task CreateTransaction_WithValidData_ReturnsCreatedTransaction()
    {
        SetupFactory();
        var card = await CreateTestCardAsync();
        var request = new CreateTransactionRequest
        {
            Description = "Grocery Store",
            TransactionDate = new DateTime(2024, 6, 15),
            Amount = 42.50m
        };

        var response = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(transaction);
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(card.Id, transaction.CardId);
        Assert.Equal("Grocery Store", transaction.Description);
        Assert.Equal(42.50m, transaction.Amount);
    }

    [Fact]
    public async Task CreateTransaction_WithEmptyBody_ReturnsBadRequest()
    {
        SetupFactory();
        var card = await CreateTestCardAsync();
        var response = await _client.PostAsync($"/api/cards/{card.Id}/transactions",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_ForNonExistentCard_ReturnsNotFound()
    {
        SetupFactory();
        var request = new CreateTransactionRequest
        {
            Description = "Test",
            TransactionDate = DateTime.UtcNow,
            Amount = 10m
        };

        var response = await _client.PostAsJsonAsync($"/api/cards/{Guid.NewGuid()}/transactions", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_ExceedingCreditLimit_ReturnsBadRequestWithErrorCode()
    {
        SetupFactory();
        var card = await CreateTestCardAsync(creditLimit: 100m);
        var request = new CreateTransactionRequest
        {
            Description = "Big Purchase",
            TransactionDate = new DateTime(2024, 6, 15),
            Amount = 150m
        };

        var response = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("INSUFFICIENT_BALANCE", body!.ErrorCode);
    }

    [Fact]
    public async Task CreateTransaction_ExceedingRemainingBalance_ReturnsBadRequestWithErrorCode()
    {
        SetupFactory();
        var card = await CreateTestCardAsync(creditLimit: 100m);

        // First transaction: spend 80
        var first = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "First Purchase",
                TransactionDate = new DateTime(2024, 6, 15),
                Amount = 80m
            });
        first.EnsureSuccessStatusCode();

        // Second transaction: try to spend 30 (only 20 remaining)
        var response = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "Second Purchase",
                TransactionDate = new DateTime(2024, 6, 16),
                Amount = 30m
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("INSUFFICIENT_BALANCE", body!.ErrorCode);
    }

    [Fact]
    public async Task GetTransaction_WithoutCurrency_ReturnsOriginalTransaction()
    {
        SetupFactory();
        var card = await CreateTestCardAsync();
        var createResponse = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "Book Store",
                TransactionDate = new DateTime(2024, 3, 10),
                Amount = 25.99m
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        var response = await _client.GetAsync($"/api/transactions/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(transaction);
        Assert.Equal("Book Store", transaction.Description);
        Assert.Equal(25.99m, transaction.Amount);
    }

    [Fact]
    public async Task GetTransaction_WithCurrency_ReturnsConvertedTransaction()
    {
        SetupFactory();

        _factory.MockExchangeRateService
            .Setup(s => s.GetExchangeRateForDateAsync("Euro Zone-Euro", It.IsAny<DateTime>()))
            .ReturnsAsync(0.85m);

        var card = await CreateTestCardAsync();
        var createResponse = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "Coffee Shop",
                TransactionDate = new DateTime(2024, 6, 15),
                Amount = 100m
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        var response = await _client.GetAsync($"/api/transactions/{created!.Id}?currency=Euro Zone-Euro");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var converted = await response.Content.ReadFromJsonAsync<ConvertedTransactionResponse>();
        Assert.NotNull(converted);
        Assert.Equal(100m, converted.OriginalAmount);
        Assert.Equal(0.85m, converted.ExchangeRate);
        Assert.Equal(85.00m, converted.ConvertedAmount);
        Assert.Equal("Euro Zone-Euro", converted.Currency);
    }

    [Fact]
    public async Task GetTransaction_WithUnavailableCurrency_ReturnsBadRequest()
    {
        SetupFactory();

        _factory.MockExchangeRateService
            .Setup(s => s.GetExchangeRateForDateAsync("FakeCurrency", It.IsAny<DateTime>()))
            .ReturnsAsync((decimal?)null);

        var card = await CreateTestCardAsync();
        var createResponse = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "Test",
                TransactionDate = new DateTime(2024, 1, 15),
                Amount = 50m
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        var response = await _client.GetAsync($"/api/transactions/{created!.Id}?currency=FakeCurrency");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTransaction_ForNonExistentId_ReturnsNotFound()
    {
        SetupFactory();
        var response = await _client.GetAsync($"/api/transactions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTransaction_WhenExternalServiceDown_Returns502WithErrorCode()
    {
        SetupFactory();

        _factory.MockExchangeRateService
            .Setup(s => s.GetExchangeRateForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var card = await CreateTestCardAsync();
        var createResponse = await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions",
            new CreateTransactionRequest
            {
                Description = "Test",
                TransactionDate = new DateTime(2024, 6, 15),
                Amount = 50m
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        var response = await _client.GetAsync($"/api/transactions/{created!.Id}?currency=Canada-Dollar");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("CURRENCY_CONVERSION_UNAVAILABLE", body!.ErrorCode);
    }
}
