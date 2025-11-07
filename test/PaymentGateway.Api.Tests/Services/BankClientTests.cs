using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

[TestFixture]
public class BankClientTests
{
    private readonly Mock<ILogger<BankClient>> _mockLogger;

    public BankClientTests()
    {
        _mockLogger = new Mock<ILogger<BankClient>>();
    }

    [Test]
    public async Task ProcessPaymentAsync_WithSuccessfulResponse_ReturnsAuthorizedResponse()
    {
        // Arrange
        var expectedResponse = new BankPaymentResponse
        {
            Authorized = true,
            AuthorizationCode = "auth-123"
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var bankClient = new BankClient(httpClient, _mockLogger.Object);

        var request = new BankPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryDate = "12/2026",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var result = await bankClient.ProcessPaymentAsync(request);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Authorized, Is.True);
        Assert.That(result.AuthorizationCode, Is.EqualTo("auth-123"));
    }

    [Test]
    public async Task ProcessPaymentAsync_WithDeclinedResponse_ReturnsDeclinedResponse()
    {
        // Arrange
        var expectedResponse = new BankPaymentResponse
        {
            Authorized = false,
            AuthorizationCode = null
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var bankClient = new BankClient(httpClient, _mockLogger.Object);

        var request = new BankPaymentRequest
        {
            CardNumber = "2222405343248878",
            ExpiryDate = "12/2026",
            Currency = "USD",
            Amount = 500,
            Cvv = "456"
        };

        // Act
        var result = await bankClient.ProcessPaymentAsync(request);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Authorized, Is.False);
        Assert.That(result.AuthorizationCode, Is.Null);
    }

    [Test]
    public async Task ProcessPaymentAsync_WithBadRequestResponse_ReturnsNull()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Bad Request")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var bankClient = new BankClient(httpClient, _mockLogger.Object);

        var request = new BankPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryDate = "12/2026",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var result = await bankClient.ProcessPaymentAsync(request);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ProcessPaymentAsync_WithServiceUnavailableResponse_ReturnsNull()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("Service Unavailable")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var bankClient = new BankClient(httpClient, _mockLogger.Object);

        var request = new BankPaymentRequest
        {
            CardNumber = "2222405343248870",
            ExpiryDate = "12/2026",
            Currency = "EUR",
            Amount = 250,
            Cvv = "789"
        };

        // Act
        var result = await bankClient.ProcessPaymentAsync(request);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ProcessPaymentAsync_WithException_ReturnsNull()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var bankClient = new BankClient(httpClient, _mockLogger.Object);

        var request = new BankPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryDate = "12/2026",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var result = await bankClient.ProcessPaymentAsync(request);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ProcessPaymentAsync_SendsCorrectRequest()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var expectedResponse = new BankPaymentResponse
        {
            Authorized = true,
            AuthorizationCode = "auth-456"
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var bankClient = new BankClient(httpClient, _mockLogger.Object);

        var request = new BankPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryDate = "06/2027",
            Currency = "USD",
            Amount = 1500,
            Cvv = "999"
        };

        // Act
        await bankClient.ProcessPaymentAsync(request);

        // Assert
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(capturedRequest.RequestUri?.PathAndQuery, Is.EqualTo("/payments"));

        var requestContent = await capturedRequest.Content!.ReadAsStringAsync();
        var sentRequest = JsonSerializer.Deserialize<BankPaymentRequest>(requestContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(sentRequest, Is.Not.Null);
        Assert.That(sentRequest.CardNumber, Is.EqualTo(request.CardNumber));
        Assert.That(sentRequest.ExpiryDate, Is.EqualTo(request.ExpiryDate));
        Assert.That(sentRequest.Currency, Is.EqualTo(request.Currency));
        Assert.That(sentRequest.Amount, Is.EqualTo(request.Amount));
        Assert.That(sentRequest.Cvv, Is.EqualTo(request.Cvv));
    }
}
