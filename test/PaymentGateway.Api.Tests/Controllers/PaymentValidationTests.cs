using System.Net;
using System.Net.Http.Json;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests.Controllers;

/// <summary>
/// Tests for payment request validation failures
/// Covers invalid card numbers, expiry dates, currencies, CVVs, and amounts
/// </summary>
[TestFixture]
public class PaymentValidationTests : PaymentsControllerTestBase
{
    [TestCase("123")]
    [TestCase("12345678901234567890")]
    [TestCase("abcd1234567890")]
    public async Task ProcessPayment_WithInvalidCardNumber_ReturnsBadRequest(string cardNumber)
    {
        // Arrange
        var (client, context) = CreateTestClient();
        var request = CreateValidPaymentRequest();
        request.CardNumber = cardNumber;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase(0)]
    [TestCase(13)]
    public async Task ProcessPayment_WithInvalidExpiryMonth_ReturnsBadRequest(int month)
    {
        // Arrange
        var (client, context) = CreateTestClient();
        var request = CreateValidPaymentRequest();
        request.ExpiryMonth = month;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ProcessPayment_WithExpiredCard_ReturnsBadRequest()
    {
        // Arrange
        var (client, context) = CreateTestClient();
        var request = CreateValidPaymentRequest();
        request.ExpiryMonth = 1;
        request.ExpiryYear = 2020;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("US")]
    [TestCase("GBPP")]
    [TestCase("XXX")]
    public async Task ProcessPayment_WithInvalidCurrency_ReturnsBadRequest(string currency)
    {
        // Arrange
        var (client, context) = CreateTestClient();
        var request = CreateValidPaymentRequest();
        request.Currency = currency;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("12")]
    [TestCase("12345")]
    [TestCase("abc")]
    public async Task ProcessPayment_WithInvalidCvv_ReturnsBadRequest(string cvv)
    {
        // Arrange
        var (client, context) = CreateTestClient();
        var request = CreateValidPaymentRequest();
        request.Cvv = cvv;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ProcessPayment_WithInvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        var (client, context) = CreateTestClient();
        var request = CreateValidPaymentRequest();
        request.Amount = 0;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
