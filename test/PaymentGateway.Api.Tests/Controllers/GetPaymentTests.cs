using System.Net;
using System.Net.Http.Json;

using PaymentGateway.Api.Data;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests.Controllers;

/// <summary>
/// Tests for retrieving payments via GET /api/Payments/{id}
/// </summary>
[TestFixture]
public class GetPaymentTests : PaymentsControllerTestBase
{
    [Test]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            ExpiryYear = _random.Next(2023, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999).ToString(),
            Currency = "GBP"
        };

        (HttpClient client, PaymentGatewayDbContext context) = CreateTestClient();
        var paymentsRepository = new PaymentsRepository(context);
        await paymentsRepository.AddAsync(payment);

        // Act
        var response = await client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
    }

    [Test]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        (HttpClient client, _) = CreateTestClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetPayment_ReturnsCorrectPaymentDetails()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new PostPaymentResponse
        {
            Id = paymentId,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "5678",
            ExpiryMonth = 3,
            ExpiryYear = 2028,
            Currency = "EUR",
            Amount = 2500
        };

        (HttpClient client, PaymentGatewayDbContext context) = CreateTestClient();
        var paymentsRepository = new PaymentsRepository(context);
        await paymentsRepository.AddAsync(payment);

        // Act
        var response = await client.GetAsync($"/api/Payments/{paymentId}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse.Id, Is.EqualTo(paymentId));
        Assert.That(paymentResponse!.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(paymentResponse.CardNumberLastFour, Is.EqualTo("5678"));
        Assert.That(paymentResponse.ExpiryMonth, Is.EqualTo(3));
        Assert.That(paymentResponse.ExpiryYear, Is.EqualTo(2028));
        Assert.That(paymentResponse.Currency, Is.EqualTo("EUR"));
        Assert.That(paymentResponse.Amount, Is.EqualTo(2500));
    }

    [Test]
    public async Task GetPayment_WithDeclinedPayment_ReturnsCorrectStatus()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new PostPaymentResponse
        {
            Id = paymentId,
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "9999",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 500
        };

        var (client, context) = CreateTestClient();
        var paymentsRepository = new PaymentsRepository(context);
        await paymentsRepository.AddAsync(payment);

        // Act
        var response = await client.GetAsync($"/api/Payments/{paymentId}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse!.Status, Is.EqualTo(PaymentStatus.Declined));
    }

    [Test]
    public async Task RejectedPayment_CanBeRetrievedById()
    {
        // Arrange
        var mockBankClient = new Moq.Mock<PaymentGateway.Api.Services.IBankClient>();
        var (client, context) = CreateTestClient(mockBankClient);

        var request = new PaymentGateway.Api.Models.Requests.PostPaymentRequest
        {
            CardNumber = "123",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act - Create rejected payment
        var postResponse = await client.PostAsJsonAsync("/api/Payments", request);

        // Extract payment ID from the context
        var paymentId = context.Payments.First().Id;

        // Act - Retrieve the rejected payment
        var getResponse = await client.GetAsync($"/api/Payments/{paymentId}");
        var retrievedPayment = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(retrievedPayment, Is.Not.Null);
        Assert.That(retrievedPayment!.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(retrievedPayment.Id, Is.EqualTo(paymentId));
    }
}
