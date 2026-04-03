namespace CardTransactionApi.Dtos;

public class BalanceResponse
{
    public Guid CardId { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AvailableBalance { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? ExchangeRate { get; set; }
    public decimal? ConvertedCreditLimit { get; set; }
    public decimal? ConvertedTotalSpent { get; set; }
    public decimal? ConvertedAvailableBalance { get; set; }
}
