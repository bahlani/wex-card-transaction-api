namespace CardTransactionApi.Dtos;

public class CardResponse
{
    public Guid Id { get; set; }
    public decimal CreditLimit { get; set; }
}
