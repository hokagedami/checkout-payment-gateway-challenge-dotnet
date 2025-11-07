using System.ComponentModel.DataAnnotations;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Models.Requests;

[FutureExpiryDate]
public class PostPaymentRequest
{
    [Required(ErrorMessage = "Card number is required")]
    [RegularExpression(@"^\d{14,19}$", ErrorMessage = "Card number must be between 14-19 numeric characters")]
    public string CardNumber { get; set; }

    [Required(ErrorMessage = "Expiry month is required")]
    [Range(1, 12, ErrorMessage = "Expiry month must be between 1 and 12")]
    public int ExpiryMonth { get; set; }

    [Required(ErrorMessage = "Expiry year is required")]
    public int ExpiryYear { get; set; }

    [Required(ErrorMessage = "Currency is required")]
    [RegularExpression("^(USD|GBP|EUR)$", ErrorMessage = "Currency must be one of: USD, GBP, EUR")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be exactly 3 characters")]
    public string Currency { get; set; }

    [Required(ErrorMessage = "Amount is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Amount must be a positive integer")]
    public int Amount { get; set; }

    [Required(ErrorMessage = "CVV is required")]
    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3-4 numeric characters")]
    public string Cvv { get; set; }
}