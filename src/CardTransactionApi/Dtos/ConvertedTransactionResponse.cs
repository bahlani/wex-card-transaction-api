namespace CardTransactionApi.Dtos;

public class ConvertedTransactionResponse
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
}
