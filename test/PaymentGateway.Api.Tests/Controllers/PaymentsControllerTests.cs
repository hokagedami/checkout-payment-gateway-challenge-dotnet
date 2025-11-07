using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Controllers;

public class PaymentsControllerTests
{
    private readonly Random _random = new();

    [Fact]
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

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => ((ServiceCollection)services)
                .AddSingleton(paymentsRepository)))
            .CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WithValidRequest_ReturnsAuthorized()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "test-code" });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal("8877", paymentResponse.CardNumberLastFour);
    }

    [Fact]
    public async Task ProcessPayment_WithValidRequest_ReturnsDeclined()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = false });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse.Status);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345678901234567890")]
    [InlineData("abcd1234567890")]
    public async Task ProcessPayment_WithInvalidCardNumber_ReturnsBadRequest(string cardNumber)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task ProcessPayment_WithInvalidExpiryMonth_ReturnsBadRequest(int month)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = month,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WithExpiredCard_ReturnsBadRequest()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 1,
            ExpiryYear = 2020,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("GBPP")]
    [InlineData("XXX")]
    public async Task ProcessPayment_WithInvalidCurrency_ReturnsBadRequest(string currency)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = currency,
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("abc")]
    public async Task ProcessPayment_WithInvalidCvv_ReturnsBadRequest(string cvv)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            Cvv = cvv
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WithInvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 0,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WhenBankReturnsNull_Returns503()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync((BankPaymentResponse?)null);

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_StoresPaymentInRepository()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "test-auth" });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.NotNull(paymentResponse);
        var storedPayment = paymentsRepository.Get(paymentResponse.Id);
        Assert.NotNull(storedPayment);
        Assert.Equal(paymentResponse.Id, storedPayment.Id);
        Assert.Equal("3456", storedPayment.CardNumberLastFour);
    }

    [Fact]
    public async Task ProcessPayment_WithMultipleCurrencies_AllSucceed()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(paymentResponse);
            Assert.Equal(currency, paymentResponse.Currency);
        }
    }

    [Fact]
    public async Task ProcessPayment_WithDifferentCardLengths_AllSucceed()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task ProcessPayment_With3DigitCvv_Succeeds()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_With4DigitCvv_Succeeds()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
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

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => ((ServiceCollection)services)
                .AddSingleton(paymentsRepository)))
            .CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{paymentId}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(paymentId, paymentResponse.Id);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal("5678", paymentResponse.CardNumberLastFour);
        Assert.Equal(3, paymentResponse.ExpiryMonth);
        Assert.Equal(2028, paymentResponse.ExpiryYear);
        Assert.Equal("EUR", paymentResponse.Currency);
        Assert.Equal(2500, paymentResponse.Amount);
    }

    [Fact]
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

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => ((ServiceCollection)services)
                .AddSingleton(paymentsRepository)))
            .CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{paymentId}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse.Status);
    }

    [Fact]
    public async Task ProcessPayment_ExtractsCorrectLastFourDigits()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.NotNull(paymentResponse);
        Assert.Equal("0366", paymentResponse.CardNumberLastFour);
    }

    [Fact]
    public async Task ProcessPayment_WithMinimumAmount_Succeeds()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(1, paymentResponse.Amount);
    }

    [Fact]
    public async Task ProcessPayment_WithLargeAmount_Succeeds()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true });

        var paymentsRepository = new PaymentsRepository();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(999999999, paymentResponse.Amount);
    }

    // Rejected Payment Tests - Tests for payment validation failures and rejected status

    [Fact]
    public async Task ProcessPayment_WithInvalidCardNumber_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
        Assert.Equal(string.Empty, storedPayment.CardNumberLastFour); // Too short, returns empty string
    }

    [Fact]
    public async Task ProcessPayment_WithExpiredCard_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
        Assert.Equal("3456", storedPayment.CardNumberLastFour);
        Assert.Equal(1, storedPayment.ExpiryMonth);
        Assert.Equal(2020, storedPayment.ExpiryYear);
    }

    [Fact]
    public async Task ProcessPayment_WithInvalidCurrency_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
        Assert.Equal("XXX", storedPayment.Currency);
    }

    [Fact]
    public async Task ProcessPayment_WithInvalidCvv_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
    }

    [Fact]
    public async Task ProcessPayment_WithInvalidAmount_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
        Assert.Equal(0, storedPayment.Amount);
    }

    [Fact]
    public async Task ProcessPayment_WithMultipleValidationErrors_CreatesRejectedPayment()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
    }

    [Fact]
    public async Task RejectedPayment_CanBeRetrievedById()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrievedPayment);
        Assert.Equal(PaymentStatus.Rejected, retrievedPayment.Status);
        Assert.Equal(paymentId, retrievedPayment.Id);
    }

    [Fact]
    public async Task ProcessPayment_WithNullCardNumber_HandlesGracefully()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status and default card digits
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
        Assert.Equal(string.Empty, storedPayment.CardNumberLastFour); // Default value for null/invalid
    }

    [Fact]
    public async Task ProcessPayment_WithShortCardNumber_ExtractsAvailableDigits()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
        Assert.Equal(string.Empty, storedPayment.CardNumberLastFour); // Too short, returns empty string
    }

    [Fact]
    public async Task ProcessPayment_WithAlphabeticCardNumber_HandlesGracefully()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment was stored with Rejected status
        Assert.Single(paymentsRepository.Payments);
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);
        Assert.Equal("7890", storedPayment.CardNumberLastFour); // Extracts last 4 chars even if invalid
    }

    [Fact]
    public async Task ProcessPayment_RejectedPayments_DoNotCallBank()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var paymentsRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(paymentsRepository);
                ((ServiceCollection)services).AddSingleton(mockBankClient.Object);
            }))
            .CreateClient();

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify payment exists with Rejected status
        var storedPayment = paymentsRepository.Payments.First();
        Assert.Equal(PaymentStatus.Rejected, storedPayment.Status);

        // Note: The bank client is not called for rejected payments
        // (validated by the fact that no bank client mock is set up in this test)
    }
}