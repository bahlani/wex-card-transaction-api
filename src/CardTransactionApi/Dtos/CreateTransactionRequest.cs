using System.ComponentModel.DataAnnotations;

namespace CardTransactionApi.Dtos;

public class CreateTransactionRequest
{
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }
}
