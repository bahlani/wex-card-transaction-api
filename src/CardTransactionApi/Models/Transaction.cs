using System.ComponentModel.DataAnnotations;

namespace CardTransactionApi.Models;

public class Transaction
{
    public Guid Id { get; set; }

    [Required]
    public Guid CardId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    public decimal Amount { get; set; }

    public Card Card { get; set; } = null!;
}
