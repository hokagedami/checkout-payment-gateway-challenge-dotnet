using System.Net;
using System.Net.Http.Json;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests.E2E;

/// <summary>
/// End-to-End tests that run against the actual Payment Gateway API and Bank Simulator.
/// These tests require the services to be running (via docker-compose).
/// Run with: docker-compose -f docker-compose.test.yml up --abort-on-container-exit
/// </summary>
[TestFixture]
[Category("E2E")]
public class PaymentGatewayE2ETests : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;

    public PaymentGatewayE2ETests()
    {
        // Use environment variable for base URL, default to localhost for local testing
        _baseUrl = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_URL") ?? "http://localhost:5000";
        _client = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(60) // Increased for retry policies
        };
    }

    [SetUp]
    public void SetUp()
    {
        // Clean up headers from previous tests
        _client.DefaultRequestHeaders.Remove("X-API-Key");
        _client.DefaultRequestHeaders.Remove("Idempotency-Key");

        // Add API key for authentication
        _client.DefaultRequestHeaders.Add("X-API-Key", "test-api-key-1");
    }

    [Test]
    public async Task PostPayment_WithValidCard_ReturnsAuthorized()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877", // Valid card number for bank simulator
            ExpiryMonth = 4,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(payment, Is.Not.Null);
        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(payment.CardNumberLastFour, Is.EqualTo("8877"));
        Assert.That(payment.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task PostPayment_WithDeclinedCard_ReturnsDeclined()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248878", // Declined card number for bank simulator
            ExpiryMonth = 4,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 100,
            Cvv = "456"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(payment, Is.Not.Null);
        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Declined));
        Assert.That(payment.CardNumberLastFour, Is.EqualTo("8878"));
    }

    [Test]
    public async Task PostPayment_WithInvalidCardNumber_ReturnsRejected()
    {
        // Arrange
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
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Verify we can parse the error response
        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.That(errorResponse, Does.Contain("Card number"));
    }

    [Test]
    public async Task PostPayment_WithExpiredCard_ReturnsRejected()
    {
        // Arrange
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
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetPayment_WithValidId_ReturnsPayment()
    {
        // Arrange - First create a payment
        var postRequest = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2026,
            Currency = "EUR",
            Amount = 500,
            Cvv = "123"
        };

        var postResponse = await _client.PostAsJsonAsync("/api/Payments", postRequest);
        var createdPayment = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.That(createdPayment, Is.Not.Null);

        // Act - Retrieve the payment
        var getResponse = await _client.GetAsync($"/api/Payments/{createdPayment.Id}");
        var retrievedPayment = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(retrievedPayment, Is.Not.Null);
        Assert.That(retrievedPayment.Id, Is.EqualTo(createdPayment.Id));
        Assert.That(retrievedPayment.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(retrievedPayment.CardNumberLastFour, Is.EqualTo("8877"));
        Assert.That(retrievedPayment.Amount, Is.EqualTo(500));
        Assert.That(retrievedPayment.Currency, Is.EqualTo("EUR"));
    }

    [Test]
    public async Task GetPayment_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/Payments/{nonExistentId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PostPayment_MultipleCurrencies_AllSucceed()
    {
        // Arrange
        var currencies = new[] { "USD", "GBP", "EUR" };
        var paymentIds = new List<Guid>();

        // Act
        foreach (var currency in currencies)
        {
            var request = new PostPaymentRequest
            {
                CardNumber = "2222405343248877",
                ExpiryMonth = 4,
                ExpiryYear = 2026,
                Currency = currency,
                Amount = 100,
                Cvv = "123"
            };

            var response = await _client.PostAsJsonAsync("/api/Payments", request);
            var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(payment, Is.Not.Null);
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Authorized));
            Assert.That(payment.Currency, Is.EqualTo(currency));
            paymentIds.Add(payment.Id);
        }

        // Assert - Verify all payments can be retrieved
        Assert.That(paymentIds, Has.Count.EqualTo(3));
        Assert.That(paymentIds.Distinct().Count(), Is.EqualTo(paymentIds.Count)); // All IDs are unique
    }

    [Test]
    public async Task PostPayment_WithLeadingZeroCardNumber_PreservesZeros()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "4532015112830366", // Last 4 digits are 0366
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(payment, Is.Not.Null);
        Assert.That(payment.CardNumberLastFour, Is.EqualTo("0366")); // Preserves leading zero
    }

    [Test]
    public async Task E2E_CompletePaymentFlow_AuthorizedDeclinedRejected()
    {
        // Test complete flow with all three statuses

        // 1. Authorized payment
        var authorizedRequest = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        var authResponse = await _client.PostAsJsonAsync("/api/Payments", authorizedRequest);
        var authPayment = await authResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.That(authResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(authPayment, Is.Not.Null);
        Assert.That(authPayment.Status, Is.EqualTo(PaymentStatus.Authorized));

        // 2. Declined payment
        var declinedRequest = new PostPaymentRequest
        {
            CardNumber = "2222405343248878",
            ExpiryMonth = 4,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 2000,
            Cvv = "456"
        };

        var decResponse = await _client.PostAsJsonAsync("/api/Payments", declinedRequest);
        var decPayment = await decResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.That(decResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(decPayment, Is.Not.Null);
        Assert.That(decPayment.Status, Is.EqualTo(PaymentStatus.Declined));

        // 3. Rejected payment
        var rejectedRequest = new PostPaymentRequest
        {
            CardNumber = "123",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 3000,
            Cvv = "123"
        };

        var rejResponse = await _client.PostAsJsonAsync("/api/Payments", rejectedRequest);

        Assert.That(rejResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // 4. Verify all payments can be retrieved (authorized and declined)
        var authGetResponse = await _client.GetAsync($"/api/Payments/{authPayment.Id}");
        Assert.That(authGetResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var decGetResponse = await _client.GetAsync($"/api/Payments/{decPayment.Id}");
        Assert.That(decGetResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Category("E2E")]
    public async Task PostPayment_WithIdempotencyKey_PreventsDuplicatePayments()
    {
        // Arrange
        var idempotencyKey = $"e2e-test-{Guid.NewGuid()}";
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 5000,
            Cvv = "123"
        };

        // Act - First request with idempotency key
        _client.DefaultRequestHeaders.Remove("Idempotency-Key");
        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await _client.PostAsJsonAsync("/api/Payments", request);
        var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Act - Second request with same idempotency key (simulating retry)
        var secondResponse = await _client.PostAsJsonAsync("/api/Payments", request);
        var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(firstPayment, Is.Not.Null);
        Assert.That(secondPayment, Is.Not.Null);

        // Same payment returned
        Assert.That(secondPayment.Id, Is.EqualTo(firstPayment.Id));
        Assert.That(secondPayment.Status, Is.EqualTo(firstPayment.Status));
        Assert.That(secondPayment.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(secondPayment.IdempotencyKey, Is.EqualTo(idempotencyKey));
        Assert.That(secondPayment.Amount, Is.EqualTo(5000));
    }

    [Test]
    [Category("E2E")]
    public async Task PostPayment_WithDifferentIdempotencyKeys_CreatesMultiplePayments()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "EUR",
            Amount = 2500,
            Cvv = "456"
        };

        // Act - First payment with idempotency key 1
        _client.DefaultRequestHeaders.Remove("Idempotency-Key");
        _client.DefaultRequestHeaders.Add("Idempotency-Key", $"e2e-key-1-{Guid.NewGuid()}");
        var firstResponse = await _client.PostAsJsonAsync("/api/Payments", request);
        var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Act - Second payment with different idempotency key
        _client.DefaultRequestHeaders.Remove("Idempotency-Key");
        _client.DefaultRequestHeaders.Add("Idempotency-Key", $"e2e-key-2-{Guid.NewGuid()}");
        var secondResponse = await _client.PostAsJsonAsync("/api/Payments", request);
        var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(firstPayment, Is.Not.Null);
        Assert.That(secondPayment, Is.Not.Null);

        // Different payments created
        Assert.That(secondPayment.Id, Is.Not.EqualTo(firstPayment.Id));
        Assert.That(firstPayment.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(secondPayment.Status, Is.EqualTo(PaymentStatus.Authorized));
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
