using System.ComponentModel.DataAnnotations;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests.Validation;

[TestFixture]
public class FutureExpiryDateAttributeTests
{
    [Test]
    public void Validate_WithFutureDate_ReturnsSuccess()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = DateTime.Now.Year + 1,
            Currency = "GBP",
            Amount = 100,
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

    [Test]
    public void Validate_WithPastDate_ReturnsError()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 1,
            ExpiryYear = 2020,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.ErrorMessage != null && r.ErrorMessage.Contains("expiry date must be in the future"))));
    }

    [Test]
    public void Validate_WithCurrentMonthAndYear_ReturnsSuccess()
    {
        // Arrange
        var now = DateTime.Now;
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = now.Month,
            ExpiryYear = now.Year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        // Should be valid as we check against the last day of the month
        Assert.That(isValid, Is.True);
    }

    [Test]
    public void Validate_WithLastMonthOfCurrentYear_ReturnsError()
    {
        // Arrange
        var now = DateTime.Now;
        if (now.Month == 1)
        {
            // Skip test if we're in January
            return;
        }

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = now.Month - 1,
            ExpiryYear = now.Year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.ErrorMessage != null && r.ErrorMessage.Contains("expiry date must be in the future"))));
    }

    [Test]
    public void Validate_WithNextMonth_ReturnsSuccess()
    {
        // Arrange
        var now = DateTime.Now;
        var nextMonth = now.AddMonths(1);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = nextMonth.Month,
            ExpiryYear = nextMonth.Year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [TestCase(1, 2026)]
    [TestCase(6, 2027)]
    [TestCase(12, 2028)]
    [TestCase(3, 2030)]
    public void Validate_WithVariousFutureDates_ReturnsSuccess(int month, int year)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = month,
            ExpiryYear = year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [TestCase(1, 2020)]
    [TestCase(12, 2019)]
    [TestCase(6, 2015)]
    [TestCase(3, 2010)]
    public void Validate_WithVariousPastDates_ReturnsError(int month, int year)
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = month,
            ExpiryYear = year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.False);
        Assert.That(results, Does.Contain(results.FirstOrDefault(r => r.ErrorMessage != null && r.ErrorMessage.Contains("expiry date must be in the future"))));
    }

    [Test]
    public void Validate_WithDecemberOfNextYear_ReturnsSuccess()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = DateTime.Now.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [Test]
    public void Validate_WithJanuaryOfNextYear_ReturnsSuccess()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 1,
            ExpiryYear = DateTime.Now.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.That(isValid, Is.True);
    }
}
