using System.ComponentModel.DataAnnotations;

namespace CardTransactionApi.Models;

public class Card
{
    public Guid Id { get; set; }

    [Required]
    public decimal CreditLimit { get; set; }

    public List<Transaction> Transactions { get; set; } = new();
}
