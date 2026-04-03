using System.ComponentModel.DataAnnotations;

namespace CardTransactionApi.Dtos;

public class CreateCardRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Credit limit must be greater than zero.")]
    public decimal CreditLimit { get; set; }
}
