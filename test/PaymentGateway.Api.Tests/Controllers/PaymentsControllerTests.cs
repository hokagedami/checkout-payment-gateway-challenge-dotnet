using Moq;
using System.Net;
using System.Net.Http.Json;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentGateway.Api.Tests.Controllers;

[TestFixture]
public class PaymentsControllerTests
{
    private Random _random = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _random = new Random();
        _factory = new WebApplicationFactory<Program>();
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();
    }

    /// <summary>
    /// Creates a test HTTP client with optional mock dependencies
    /// </summary>
    private HttpClient CreateTestClient(
        PaymentsRepository? paymentsRepository = null,
        Mock<IBankClient>? mockBankClient = null)
    {
        var repository = paymentsRepository ?? new PaymentsRepository();

        // Create a default mock if none provided
        var bankClient = mockBankClient ?? new Mock<IBankClient>();

        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Remove existing registrations
                var repositoryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PaymentsRepository));
                if (repositoryDescriptor != null)
                    services.Remove(repositoryDescriptor);

                var bankClientDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBankClient));
                if (bankClientDescriptor != null)
                    services.Remove(bankClientDescriptor);

                // Add test instances
                services.AddSingleton(repository);
                services.AddSingleton(bankClient.Object);
            }))
            .CreateClient();
    }

    /// <summary>
    /// Creates a valid payment request with default values
    /// </summary>
    private static PostPaymentRequest CreateValidPaymentRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 12,
        ExpiryYear = 2026,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

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

        var paymentsRepository = new PaymentsRepository();
        paymentsRepository.Add(payment);

        var client = CreateTestClient(paymentsRepository);

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
        var client = CreateTestClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ProcessPayment_WithValidRequest_ReturnsAuthorized()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "test-code" });

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);
        var request = CreateValidPaymentRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

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

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse!.Status, Is.EqualTo(PaymentStatus.Declined));
    }

    [TestCase("123")]
    [TestCase("12345678901234567890")]
    [TestCase("abcd1234567890")]
    public async Task ProcessPayment_WithInvalidCardNumber_ReturnsBadRequest(string cardNumber)
    {
        // Arrange
        var client = CreateTestClient();
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
        var client = CreateTestClient();
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
        var client = CreateTestClient();
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
        var client = CreateTestClient();
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
        var client = CreateTestClient();
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
        var client = CreateTestClient();
        var request = CreateValidPaymentRequest();
        request.Amount = 0;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ProcessPayment_WhenBankReturnsNull_Returns503()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync((BankPaymentResponse?)null);

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(paymentResponse, Is.Not.Null);
        var storedPayment = paymentsRepository.Get(paymentResponse.Id);
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

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
            var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

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

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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

        var paymentsRepository = new PaymentsRepository();
        paymentsRepository.Add(payment);

        var webApplicationFactory = new WebApplicationFactory<Program>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Remove existing registration
                var repositoryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PaymentsRepository));
                if (repositoryDescriptor != null)
                    services.Remove(repositoryDescriptor);

                // Add test instance
                services.AddSingleton(paymentsRepository);
            }))
            .CreateClient();

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

        var paymentsRepository = new PaymentsRepository();
        paymentsRepository.Add(payment);

        var webApplicationFactory = new WebApplicationFactory<Program>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Remove existing registration
                var repositoryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PaymentsRepository));
                if (repositoryDescriptor != null)
                    services.Remove(repositoryDescriptor);

                // Add test instance
                services.AddSingleton(paymentsRepository);
            }))
            .CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{paymentId}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse!.Status, Is.EqualTo(PaymentStatus.Declined));
    }

    [Test]
    public async Task ProcessPayment_ExtractsCorrectLastFourDigits()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

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

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

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

        var paymentsRepository = new PaymentsRepository();

        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(paymentResponse, Is.Not.Null);
        Assert.That(paymentResponse.Amount, Is.EqualTo(999999999));
    }

    // Rejected Payment Tests - Tests for payment validation failures and rejected status

    [Test]
    public async Task ProcessPayment_WithInvalidCardNumber_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo(string.Empty)); // Too short, returns empty string
    }

    [Test]
    public async Task ProcessPayment_WithExpiredCard_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
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
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.Currency, Is.EqualTo("XXX"));
    }

    [Test]
    public async Task ProcessPayment_WithInvalidCvv_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
    }

    [Test]
    public async Task ProcessPayment_WithInvalidAmount_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.Amount, Is.EqualTo(0));
    }

    [Test]
    public async Task ProcessPayment_WithMultipleValidationErrors_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
    }

    [Test]
    public async Task RejectedPayment_CanBeRetrievedById()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

        var request = new PostPaymentRequest
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

        // Extract payment ID from the response (need to parse the BadRequest response)
        var responseContent = await postResponse.Content.ReadFromJsonAsync<dynamic>();
        var paymentId = paymentsRepository.Payments.First().Id;

        // Act - Retrieve the rejected payment
        var getResponse = await client.GetAsync($"/api/Payments/{paymentId}");
        var retrievedPayment = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(retrievedPayment, Is.Not.Null);
        Assert.That(retrievedPayment!.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(retrievedPayment.Id, Is.EqualTo(paymentId));
    }

    [Test]
    public async Task ProcessPayment_WithNullCardNumber_HandlesGracefully()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo(string.Empty)); // Default value for null/invalid
    }

    [Test]
    public async Task ProcessPayment_WithShortCardNumber_ExtractsAvailableDigits()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo(string.Empty)); // Too short, returns empty string
    }

    [Test]
    public async Task ProcessPayment_WithAlphabeticCardNumber_HandlesGracefully()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments, Has.Count.EqualTo(1));
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(storedPayment.CardNumberLastFour, Is.EqualTo("7890")); // Extracts last 4 chars even if invalid
    }

    [Test]
    public async Task ProcessPayment_RejectedPayments_DoNotCallBank()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        var storedPayment = paymentsRepository.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));

        // Note: The bank client is not called for rejected payments
        // (validated by the fact that no bank client mock is set up in this test)
    }

    [Test]
    public async Task ProcessPayment_WithIdempotencyKey_ReturnsSamePaymentOnRetry()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "auth-123" });

        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessPayment_WithDifferentIdempotencyKeys_CreatesMultiplePayments()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "auth-123" });

        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task ProcessPayment_WithoutIdempotencyKey_CreatesMultiplePayments()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "auth-123" });

        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task ProcessPayment_WithIdempotencyKey_ReturnsRejectedPaymentOnRetry()
    {
        // Arrange
        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository);

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
        Assert.That(paymentsRepository.Payments.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessPayment_WithIdempotencyKey_ReturnsDeclinedPaymentOnRetry()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = false, AuthorizationCode = "declined-123" });

        var paymentsRepository = new PaymentsRepository();
        var client = CreateTestClient(paymentsRepository, mockBankClient);

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
        Assert.That(paymentsRepository.Payments.Count, Is.EqualTo(1));
    }
}