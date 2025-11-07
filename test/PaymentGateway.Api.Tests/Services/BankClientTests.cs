using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

public class BankClientTests
{
    private readonly Mock<ILogger<BankClient>> _mockLogger;

    public BankClientTests()
    {
        _mockLogger = new Mock<ILogger<BankClient>>();
    }

    [Fact]
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
        Assert.NotNull(result);
        Assert.True(result.Authorized);
        Assert.Equal("auth-123", result.AuthorizationCode);
    }

    [Fact]
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
        Assert.NotNull(result);
        Assert.False(result.Authorized);
        Assert.Null(result.AuthorizationCode);
    }

    [Fact]
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
        Assert.Null(result);
    }

    [Fact]
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
        Assert.Null(result);
    }

    [Fact]
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
        Assert.Null(result);
    }

    [Fact]
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
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("/payments", capturedRequest.RequestUri?.PathAndQuery);

        var requestContent = await capturedRequest.Content!.ReadAsStringAsync();
        var sentRequest = JsonSerializer.Deserialize<BankPaymentRequest>(requestContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(sentRequest);
        Assert.Equal(request.CardNumber, sentRequest.CardNumber);
        Assert.Equal(request.ExpiryDate, sentRequest.ExpiryDate);
        Assert.Equal(request.Currency, sentRequest.Currency);
        Assert.Equal(request.Amount, sentRequest.Amount);
        Assert.Equal(request.Cvv, sentRequest.Cvv);
    }
}
