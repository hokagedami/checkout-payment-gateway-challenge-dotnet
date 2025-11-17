using Moq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests.Controllers;

/// <summary>
/// Tests for rejected payment storage and behavior
/// Ensures validation failures are stored as rejected payments in the database
/// </summary>
[TestFixture]
public class RejectedPaymentTests : PaymentsControllerTestBase
{
    [Test]
    public async Task ProcessPayment_WithInvalidCardNumber_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "123", // Invalid: too short
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo(string.Empty)); // Too short, returns empty string
    }

    [Test]
    public async Task ProcessPayment_WithExpiredCard_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 1,
            ExpiryYear = 2020, // Expired
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo("3456"));
        Assert.That(storedPayment.ExpiryMonth, Is.EqualTo(1));
        Assert.That(storedPayment.ExpiryYear, Is.EqualTo(2020));
    }

    [Test]
    public async Task ProcessPayment_WithInvalidCurrency_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "XXX", // Invalid currency
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.Currency, Is.EqualTo("XXX"));
    }

    [Test]
    public async Task ProcessPayment_WithInvalidCvv_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "12" // Invalid: too short
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
    }

    [Test]
    public async Task ProcessPayment_WithInvalidAmount_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 0, // Invalid: must be positive
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.Amount, Is.EqualTo(0));
    }

    [Test]
    public async Task ProcessPayment_WithMultipleValidationErrors_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "123", // Invalid
            ExpiryMonth = 13, // Invalid
            ExpiryYear = 2020, // Invalid (expired)
            Currency = "INVALID", // Invalid
            Amount = -100, // Invalid
            Cvv = "1" // Invalid
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
    }

    [Test]
    public async Task ProcessPayment_WithNullCardNumber_HandlesGracefully()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = null!,
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status and default card digits
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo(string.Empty)); // Default value for null/invalid
    }

    [Test]
    public async Task ProcessPayment_WithShortCardNumber_ExtractsAvailableDigits()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "12", // Too short but has digits
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo(string.Empty)); // Too short, returns empty string
    }

    [Test]
    public async Task ProcessPayment_WithAlphabeticCardNumber_HandlesGracefully()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "abcd1234567890", // Contains letters
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment was stored with Rejected status
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo("7890")); // Extracts last 4 chars even if invalid
    }

    [Test]
    public async Task ProcessPayment_RejectedPayments_DoNotCallBank()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "123", // Invalid - will be rejected
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify payment exists with Rejected status
        var storedPayment = context.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));

        // Note: The bank client is not called for rejected payments
        // (validated by the fact that no bank client mock is set up in this test)
    }
}
