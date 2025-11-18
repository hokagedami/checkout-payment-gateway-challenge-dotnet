using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PaymentGateway.Api.Authentication;

namespace PaymentGateway.Api.Tests.Authentication;

/// <summary>
/// Tests for API Key authentication handler
/// </summary>
[TestFixture]
public class ApiKeyAuthenticationHandlerTests
{
    private Mock<IOptionsMonitor<AuthenticationSchemeOptions>> _mockOptions = null!;
    private Mock<ILoggerFactory> _mockLoggerFactory = null!;
    private Mock<UrlEncoder> _mockUrlEncoder = null!;
    private IConfiguration _mockConfiguration = null!;
    private ApiKeyAuthenticationHandler _handler = null!;
    private DefaultHttpContext _httpContext = null!;
    private AuthenticationScheme _scheme = null!;

    [SetUp]
    public void SetUp()
    {
        _mockOptions = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        _mockOptions.Setup(x => x.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());

        _mockUrlEncoder = new Mock<UrlEncoder>();

        // Setup configuration with API keys using ConfigurationBuilder
        var configData = new Dictionary<string, string?>
        {
            { "Authentication:ApiKeys:0", "test-key-1" },
            { "Authentication:ApiKeys:1", "test-key-2" },
            { "Authentication:ApiKeys:2", "valid-key" }
        };
        _mockConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _httpContext = new DefaultHttpContext();
        _scheme = new AuthenticationScheme(
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(ApiKeyAuthenticationHandler));

        _handler = new ApiKeyAuthenticationHandler(
            _mockOptions.Object,
            _mockLoggerFactory.Object,
            _mockUrlEncoder.Object,
            _mockConfiguration);
    }

    [Test]
    public async Task HandleAuthenticateAsync_WithValidApiKey_ReturnsSuccess()
    {
        // Arrange
        _httpContext.Request.Headers["X-API-Key"] = "test-key-1";
        await _handler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Ticket, Is.Not.Null);
        Assert.That(result.Ticket.Principal.Identity?.IsAuthenticated, Is.True);
        Assert.That(result.Ticket.Principal.Identity?.AuthenticationType, Is.EqualTo(ApiKeyAuthenticationDefaults.AuthenticationScheme));
    }

    [Test]
    public async Task HandleAuthenticateAsync_WithDifferentValidApiKey_ReturnsSuccess()
    {
        // Arrange
        _httpContext.Request.Headers["X-API-Key"] = "valid-key";
        await _handler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Ticket, Is.Not.Null);
    }

    [Test]
    public async Task HandleAuthenticateAsync_WithInvalidApiKey_ReturnsFail()
    {
        // Arrange
        _httpContext.Request.Headers["X-API-Key"] = "invalid-key";
        await _handler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
        Assert.That(result.Failure?.Message, Does.Contain("Invalid API Key"));
    }

    [Test]
    public async Task HandleAuthenticateAsync_WithMissingApiKey_ReturnsFail()
    {
        // Arrange - no API key header set
        await _handler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
        Assert.That(result.Failure?.Message, Does.Contain("Missing API Key"));
    }

    [Test]
    public async Task HandleAuthenticateAsync_WithEmptyApiKey_ReturnsFail()
    {
        // Arrange
        _httpContext.Request.Headers["X-API-Key"] = "";
        await _handler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
        Assert.That(result.Failure?.Message, Does.Contain("Invalid API Key"));
    }

    [Test]
    public async Task HandleAuthenticateAsync_WithWhitespaceApiKey_ReturnsFail()
    {
        // Arrange
        _httpContext.Request.Headers["X-API-Key"] = "   ";
        await _handler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
    }

    [Test]
    public async Task HandleAuthenticateAsync_WithCaseSensitiveApiKey_FailsIfCaseMismatch()
    {
        // Arrange
        _httpContext.Request.Headers["X-API-Key"] = "TEST-KEY-1"; // Different case
        await _handler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task HandleAuthenticateAsync_ValidKey_CreatesPrincipalWithClaims()
    {
        // Arrange
        _httpContext.Request.Headers["X-API-Key"] = "test-key-1";
        await _handler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.True);
        var principal = result.Ticket?.Principal;
        Assert.That(principal, Is.Not.Null);
        Assert.That(principal.Identity, Is.Not.Null);
        Assert.That(principal.Identity.AuthenticationType, Is.EqualTo(ApiKeyAuthenticationDefaults.AuthenticationScheme));
    }

    [Test]
    public async Task HandleAuthenticateAsync_WithNoConfiguredKeys_AlwaysFails()
    {
        // Arrange
        var emptyConfigData = new Dictionary<string, string?>();
        var emptyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(emptyConfigData)
            .Build();

        var emptyHandler = new ApiKeyAuthenticationHandler(
            _mockOptions.Object,
            _mockLoggerFactory.Object,
            _mockUrlEncoder.Object,
            emptyConfiguration);

        _httpContext.Request.Headers["X-API-Key"] = "any-key";
        await emptyHandler.InitializeAsync(_scheme, _httpContext);

        // Act
        var result = await emptyHandler.AuthenticateAsync();

        // Assert
        Assert.That(result.Succeeded, Is.False);
    }
}
