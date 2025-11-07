using System.ComponentModel.DataAnnotations;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests.Models;

[TestFixture]
public class PostPaymentRequestValidationTests
{
    [Test]
    public void Validate_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
        Assert.That(results, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithMissingCardNumber_ReturnsError(string? cardNumber)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = cardNumber!,
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.MemberNames.Contains("CardNumber"))));
    }

    [TestCase("123")]
    [TestCase("12345678901")]
    [TestCase("12345678901234567890")]
    [TestCase("abcd1234567890")]
    [TestCase("1234 5678 9012 3456")]
    [TestCase("1234-5678-9012-3456")]
    public void Validate_WithInvalidCardNumber_ReturnsError(string cardNumber)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.MemberNames.Contains("CardNumber"))));
    }

    [TestCase("12345678901234")]
    [TestCase("123456789012345")]
    [TestCase("1234567890123456")]
    [TestCase("12345678901234567")]
    [TestCase("123456789012345678")]
    [TestCase("1234567890123456789")]
    public void Validate_WithValidCardNumberLengths_ReturnsSuccess(string cardNumber)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(13)]
    [TestCase(100)]
    public void Validate_WithInvalidExpiryMonth_ReturnsError(int month)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = month,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.MemberNames.Contains("ExpiryMonth"))));
    }

    [TestCase(1)]
    [TestCase(6)]
    [TestCase(12)]
    public void Validate_WithValidExpiryMonth_ReturnsSuccess(int month)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = month,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithMissingCurrency_ReturnsError(string? currency)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = currency!,
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.MemberNames.Contains("Currency"))));
    }

    [TestCase("US")]
    [TestCase("GBPP")]
    [TestCase("E")]
    [TestCase("EURO")]
    [TestCase("XXX")]
    [TestCase("JPY")]
    [TestCase("123")]
    public void Validate_WithInvalidCurrency_ReturnsError(string currency)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = currency,
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.MemberNames.Contains("Currency"))));
    }

    [TestCase("USD")]
    [TestCase("GBP")]
    [TestCase("EUR")]
    public void Validate_WithValidCurrency_ReturnsSuccess(string currency)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = currency,
            Amount = 1000,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public void Validate_WithInvalidAmount_ReturnsError(int amount)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = amount,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.MemberNames.Contains("Amount"))));
    }

    [TestCase(1)]
    [TestCase(100)]
    [TestCase(1050)]
    [TestCase(999999)]
    [TestCase(int.MaxValue)]
    public void Validate_WithValidAmount_ReturnsSuccess(int amount)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = amount,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithMissingCvv_ReturnsError(string? cvv)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = cvv!
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.MemberNames.Contains("Cvv"))));
    }

    [TestCase("12")]
    [TestCase("12345")]
    [TestCase("1")]
    [TestCase("abc")]
    [TestCase("12a")]
    public void Validate_WithInvalidCvv_ReturnsError(string cvv)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = cvv
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.MemberNames.Contains("Cvv"))));
    }

    [TestCase("123")]
    [TestCase("456")]
    [TestCase("999")]
    [TestCase("0000")]
    [TestCase("1234")]
    public void Validate_WithValidCvv_ReturnsSuccess(string cvv)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = cvv
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [Test]
    public void Validate_WithAllInvalidFields_ReturnsMultipleErrors()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "123",
            ExpiryMonth = 13,
            ExpiryYear = 2020,
            Currency = "INVALID",
            Amount = -100,
            Cvv = "1"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results.Count >= 5, Is.True); // At least 5 validation errors
    }
}
