using Moq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests.Controllers;

/// <summary>
/// Tests for processing payments via POST /api/Payments
/// Covers successful payments (authorized and declined), storage, and edge cases
/// </summary>
[TestFixture]
public class PostPaymentTests : PaymentsControllerTestBase
{
    [Test]
    public async Task ProcessPayment_WithValidRequest_ReturnsAuthorized()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "test-code" });

        var (client, context) = CreateTestClient(mockBankClient);
        var request = CreateValidPaymentRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await ReadApiResponseAsync<PostPaymentResponse>(response);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse!.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(paymentResponse.CardNumberLastFour, Is.EqualTo("8877"));
    }

    [Test]
    public async Task ProcessPayment_WithValidRequest_ReturnsDeclined()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = false });

        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248878",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 500,
            Cvv = "456"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await ReadApiResponseAsync<PostPaymentResponse>(response);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse!.Status, Is.EqualTo(PaymentStatus.Declined));
    }

    [Test]
    public async Task ProcessPayment_WhenBankReturnsNull_Returns503()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync((BankPaymentResponse?)null);

        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248870",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "EUR",
            Amount = 300,
            Cvv = "789"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task ProcessPayment_StoresPaymentInRepository()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "test-auth" });

        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 6,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 1500,
            Cvv = "456"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await ReadApiResponseAsync<PostPaymentResponse>(response);

        // Assert
        Assert.That(paymentResponse, Is.Not.Null);
        var paymentsRepository = new PaymentsRepository(context);
        var storedPayment = await paymentsRepository.GetAsync(paymentResponse.Id);
        Assert.That(storedPayment, Is.Not.Null);
        Assert.That(storedPayment.Id, Is.EqualTo(paymentResponse.Id));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo("3456"));
    }

    [Test]
    public async Task ProcessPayment_WithMultipleCurrencies_AllSucceed()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var (client, context) = CreateTestClient(mockBankClient);

        var currencies = new[] { "USD", "GBP", "EUR" };

        // Act & Assert
        foreach (var currency in currencies)
        {
            var request = new PostPaymentRequest
            {
                CardNumber = "1234567890123456",
                ExpiryMonth = 12,
                ExpiryYear = 2026,
                Currency = currency,
                Amount = 100,
                Cvv = "123"
            };

            var response = await client.PostAsJsonAsync("/api/Payments", request);
            var paymentResponse = await ReadApiResponseAsync<PostPaymentResponse>(response);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(paymentResponse, Is.Not.Null);
            Assert.That(paymentResponse.Currency, Is.EqualTo(currency));
        }
    }

    [Test]
    public async Task ProcessPayment_WithDifferentCardLengths_AllSucceed()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var (client, context) = CreateTestClient(mockBankClient);

        var cardNumbers = new[]
        {
            "12345678901234",     // 14 digits
            "123456789012345",    // 15 digits
            "1234567890123456",   // 16 digits
            "12345678901234567",  // 17 digits
            "123456789012345678", // 18 digits
            "1234567890123456789" // 19 digits
        };

        // Act & Assert
        foreach (var cardNumber in cardNumbers)
        {
            var request = new PostPaymentRequest
            {
                CardNumber = cardNumber,
                ExpiryMonth = 12,
                ExpiryYear = 2026,
                Currency = "GBP",
                Amount = 100,
                Cvv = "123"
            };

            var response = await client.PostAsJsonAsync("/api/Payments", request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }

    [Test]
    public async Task ProcessPayment_With3DigitCvv_Succeeds()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ProcessPayment_With4DigitCvv_Succeeds()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "1234"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ProcessPayment_ExtractsCorrectLastFourDigits()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "4532015112830366",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await ReadApiResponseAsync<PostPaymentResponse>(response);

        // Assert
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse.CardNumberLastFour, Is.EqualTo("0366"));
    }

    [Test]
    public async Task ProcessPayment_WithMinimumAmount_Succeeds()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await ReadApiResponseAsync<PostPaymentResponse>(response);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse.Amount, Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessPayment_WithLargeAmount_Succeeds()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 999999999,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await ReadApiResponseAsync<PostPaymentResponse>(response);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse.Amount, Is.EqualTo(999999999));
    }
}
