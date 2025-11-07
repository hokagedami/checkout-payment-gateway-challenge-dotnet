using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Validation;

public class FutureExpiryDateAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var expiryMonthProperty = instance.GetType().GetProperty("ExpiryMonth");
        var expiryYearProperty = instance.GetType().GetProperty("ExpiryYear");

        if (expiryMonthProperty == null || expiryYearProperty == null)
        {
            return new ValidationResult("ExpiryMonth and ExpiryYear properties must be present");
        }

        var expiryMonth = (int)(expiryMonthProperty.GetValue(instance) ?? 0);
        var expiryYear = (int)(expiryYearProperty.GetValue(instance) ?? 0);

        // Create a date representing the last day of the expiry month
        var expiryDate = new DateTime(expiryYear, expiryMonth, DateTime.DaysInMonth(expiryYear, expiryMonth));
        var today = DateTime.Today;

        return expiryDate < today ? 
            new ValidationResult("Card expiry date must be in the future") : 
            ValidationResult.Success;
    }
}
