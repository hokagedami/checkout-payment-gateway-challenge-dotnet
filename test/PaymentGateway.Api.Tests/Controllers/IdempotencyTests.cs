using Moq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests.Controllers;

/// <summary>
/// Tests for idempotency key behavior
/// Ensures duplicate requests with the same idempotency key return the same payment
/// </summary>
[TestFixture]
public class IdempotencyTests : PaymentsControllerTestBase
{
    [Test]
    public async Task ProcessPayment_WithIdempotencyKey_ReturnsSamePaymentOnRetry()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "auth-123" });
        var (client, context) = CreateTestClient(mockBankClient);

        var request = CreateValidPaymentRequest();
        var idempotencyKey = "test-idempotency-key-123";

        // Act - First request
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Act - Second request with same idempotency key
        var secondResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(firstPayment, Is.Not.Null);
        Assert.That(secondPayment, Is.Not.Null);

        // Same payment ID returned
        Assert.That(secondPayment.Id, Is.EqualTo(firstPayment.Id));
        Assert.That(secondPayment.Status, Is.EqualTo(firstPayment.Status));
        Assert.That(secondPayment.IdempotencyKey, Is.EqualTo(idempotencyKey));

        // Bank was called only once
        mockBankClient.Verify(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Once);

        // Only one payment stored
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessPayment_WithDifferentIdempotencyKeys_CreatesMultiplePayments()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "auth-123" });
        var (client, context) = CreateTestClient(mockBankClient);

        var request = CreateValidPaymentRequest();

        // Act - First request with key 1
        client.DefaultRequestHeaders.Add("Idempotency-Key", "key-1");
        var firstResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Clear headers and add different key
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "key-2");
        var secondResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(firstPayment, Is.Not.Null);
        Assert.That(secondPayment, Is.Not.Null);

        // Different payment IDs
        Assert.That(secondPayment.Id, Is.Not.EqualTo(firstPayment.Id));
        Assert.That(firstPayment.IdempotencyKey, Is.EqualTo("key-1"));
        Assert.That(secondPayment.IdempotencyKey, Is.EqualTo("key-2"));

        // Bank was called twice
        mockBankClient.Verify(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Exactly(2));

        // Two payments stored
        Assert.That(context.Payments.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task ProcessPayment_WithoutIdempotencyKey_CreatesMultiplePayments()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "auth-123" });
        var (client, context) = CreateTestClient(mockBankClient);

        var request = CreateValidPaymentRequest();

        // Act - Two requests without idempotency key
        var firstResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        var secondResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(firstPayment, Is.Not.Null);
        Assert.That(secondPayment, Is.Not.Null);

        // Different payment IDs
        Assert.That(secondPayment.Id, Is.Not.EqualTo(firstPayment.Id));
        Assert.That(firstPayment.IdempotencyKey, Is.Null);
        Assert.That(secondPayment.IdempotencyKey, Is.Null);

        // Bank was called twice
        mockBankClient.Verify(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Exactly(2));

        // Two payments stored
        Assert.That(context.Payments.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task ProcessPayment_WithIdempotencyKey_ReturnsRejectedPaymentOnRetry()
    {
        // Arrange
        var (client, context) = CreateTestClient();

        var invalidRequest = new PostPaymentRequest
        {
            CardNumber = "123", // Invalid
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var idempotencyKey = "rejected-key";

        // Act - First request (will be rejected)
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await client.PostAsJsonAsync("/api/Payments", invalidRequest);

        // Act - Second request with same idempotency key
        var secondResponse = await client.PostAsJsonAsync("/api/Payments", invalidRequest);
        var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK)); // Returns existing rejected payment with 200

        Assert.That(secondPayment, Is.Not.Null);
        Assert.That(secondPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(secondPayment.IdempotencyKey, Is.EqualTo(idempotencyKey));

        // Only one payment stored
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessPayment_WithIdempotencyKey_ReturnsDeclinedPaymentOnRetry()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = false, AuthorizationCode = "declined-123" });
        var (client, context) = CreateTestClient(mockBankClient);

        var request = CreateValidPaymentRequest();
        var idempotencyKey = "declined-key";

        // Act - First request (will be declined)
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Act - Second request with same idempotency key
        var secondResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(firstPayment, Is.Not.Null);
        Assert.That(secondPayment, Is.Not.Null);

        Assert.That(firstPayment.Status, Is.EqualTo(PaymentStatus.Declined));
        Assert.That(secondPayment.Id, Is.EqualTo(firstPayment.Id));
        Assert.That(secondPayment.Status, Is.EqualTo(PaymentStatus.Declined));
        Assert.That(secondPayment.IdempotencyKey, Is.EqualTo(idempotencyKey));

        // Bank was called only once
        mockBankClient.Verify(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Once);

        // Only one payment stored
        Assert.That(context.Payments.Count(), Is.EqualTo(1));
    }
}
