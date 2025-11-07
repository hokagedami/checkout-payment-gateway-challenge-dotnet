using System.ComponentModel.DataAnnotations;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests.Models;

public class PostPaymentRequestValidationTests
{
    [Fact]
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
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
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
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("CardNumber"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345678901")]
    [InlineData("12345678901234567890")]
    [InlineData("abcd1234567890")]
    [InlineData("1234 5678 9012 3456")]
    [InlineData("1234-5678-9012-3456")]
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
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("CardNumber"));
    }

    [Theory]
    [InlineData("12345678901234")]
    [InlineData("123456789012345")]
    [InlineData("1234567890123456")]
    [InlineData("12345678901234567")]
    [InlineData("123456789012345678")]
    [InlineData("1234567890123456789")]
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
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
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
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("ExpiryMonth"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
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
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
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
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Currency"));
    }

    [Theory]
    [InlineData("US")]
    [InlineData("GBPP")]
    [InlineData("E")]
    [InlineData("EURO")]
    [InlineData("XXX")]
    [InlineData("JPY")]
    [InlineData("123")]
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
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Currency"));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("EUR")]
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
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
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
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Amount"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1050)]
    [InlineData(999999)]
    [InlineData(int.MaxValue)]
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
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
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
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Cvv"));
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("1")]
    [InlineData("abc")]
    [InlineData("12a")]
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
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Cvv"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("456")]
    [InlineData("999")]
    [InlineData("0000")]
    [InlineData("1234")]
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
        Assert.True(isValid);
    }

    [Fact]
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
        Assert.False(isValid);
        Assert.True(results.Count >= 5); // At least 5 validation errors
    }
}
