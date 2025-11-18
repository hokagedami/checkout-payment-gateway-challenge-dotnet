using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PaymentGateway.Api.Middleware;

namespace PaymentGateway.Api.Tests.Middleware;

[TestFixture]
public class GlobalExceptionHandlerTests
{
    private GlobalExceptionHandler _handler;
    private Mock<ILogger<GlobalExceptionHandler>> _mockLogger;
    private DefaultHttpContext _httpContext;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        _handler = new GlobalExceptionHandler(_mockLogger.Object);
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = new MemoryStream();
    }

    [Test]
    public async Task TryHandleAsync_WithArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var result = await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_httpContext.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails.Status, Is.EqualTo(400));
        Assert.That(problemDetails.Title, Is.EqualTo("Bad Request"));
        Assert.That(problemDetails.Detail, Is.EqualTo("Invalid argument"));
    }

    [Test]
    public async Task TryHandleAsync_WithKeyNotFoundException_ReturnsNotFound()
    {
        // Arrange
        var exception = new KeyNotFoundException("Resource not found");

        // Act
        var result = await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_httpContext.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails.Status, Is.EqualTo(404));
        Assert.That(problemDetails.Title, Is.EqualTo("Not Found"));
    }

    [Test]
    public async Task TryHandleAsync_WithHttpRequestException_ReturnsBadGateway()
    {
        // Arrange
        var exception = new HttpRequestException("External service failed");

        // Act
        var result = await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_httpContext.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadGateway));

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails.Status, Is.EqualTo(502));
        Assert.That(problemDetails.Title, Is.EqualTo("External Service Error"));
        Assert.That(problemDetails.Detail, Is.EqualTo("An error occurred while communicating with an external service"));
    }

    [Test]
    public async Task TryHandleAsync_WithTimeoutException_ReturnsRequestTimeout()
    {
        // Arrange
        var exception = new TimeoutException("Request timeout");

        // Act
        var result = await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_httpContext.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.RequestTimeout));

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails.Status, Is.EqualTo(408));
        Assert.That(problemDetails.Title, Is.EqualTo("Request Timeout"));
    }

    [Test]
    public async Task TryHandleAsync_WithUnhandledException_ReturnsInternalServerError()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");

        // Act
        var result = await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_httpContext.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.InternalServerError));

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails.Status, Is.EqualTo(500));
        Assert.That(problemDetails.Title, Is.EqualTo("An error occurred while processing your request"));
        Assert.That(problemDetails.Detail, Is.EqualTo("An unexpected error occurred"));
    }

    [Test]
    public async Task TryHandleAsync_LogsException()
    {
        // Arrange
        var exception = new Exception("Test exception");

        // Act
        await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("An unhandled exception occurred")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
