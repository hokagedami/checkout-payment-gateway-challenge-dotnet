using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PaymentGateway.Api.Data;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Middleware;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace PaymentGateway.Api.Tests.Middleware;

/// <summary>
/// Tests for ModelStateValidationFilter to ensure proper validation error handling
/// </summary>
[TestFixture]
public class ModelStateValidationFilterTests
{
    private Mock<ILogger<ModelStateValidationFilter>> _mockLogger = null!;
    private ModelStateValidationFilter _filter = null!;
    private ActionExecutingContext _actionContext = null!;
    private PaymentGatewayDbContext _dbContext = null!;
    private IServiceProvider _serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<ModelStateValidationFilter>>();

        // Create in-memory database
        var options = new DbContextOptionsBuilder<PaymentGatewayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PaymentGatewayDbContext(options);

        // Setup service provider
        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        services.AddScoped<IPaymentsRepository>(sp => new PaymentsRepository(_dbContext));
        _serviceProvider = services.BuildServiceProvider();

        _filter = new ModelStateValidationFilter(_mockLogger.Object, _serviceProvider);

        // Setup action context
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = _serviceProvider;

        var actionDescriptor = new ActionDescriptor();
        var routeData = new RouteData();
        var modelState = new ModelStateDictionary();

        _actionContext = new ActionExecutingContext(
            new ActionContext(httpContext, routeData, actionDescriptor, modelState),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
        (_serviceProvider as ServiceProvider)?.Dispose();
    }

    [Test]
    public async Task OnActionExecutionAsync_WithValidModelState_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                _actionContext,
                new List<IFilterMetadata>(),
                new object()));
        }

        // Act
        await _filter.OnActionExecutionAsync(_actionContext, Next);

        // Assert
        Assert.That(nextCalled, Is.True);
        Assert.That(_actionContext.Result, Is.Null);
    }

    [Test]
    public async Task OnActionExecutionAsync_WithInvalidModelState_AndNoPostPaymentRequest_ReturnsBadRequest()
    {
        // Arrange
        _actionContext.ModelState.AddModelError("TestField", "Test error");

        Task<ActionExecutedContext> Next()
        {
            Assert.Fail("Next should not be called");
            return Task.FromResult(new ActionExecutedContext(
                _actionContext,
                new List<IFilterMetadata>(),
                new object()));
        }

        // Act
        await _filter.OnActionExecutionAsync(_actionContext, Next);

        // Assert
        Assert.That(_actionContext.Result, Is.InstanceOf<BadRequestObjectResult>());
        var result = _actionContext.Result as BadRequestObjectResult;
        Assert.That(result, Is.Not.Null);

        var apiResponse = result.Value as ApiResponse<object>;
        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.False);
        Assert.That(apiResponse.Errors, Contains.Item("Test error"));
    }

    [Test]
    public async Task OnActionExecutionAsync_WithInvalidModelState_AndPostPaymentRequest_CreatesRejectedPayment()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "123", // Invalid
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 100,
            Cvv = "123"
        };

        _actionContext.ActionArguments["request"] = request;
        _actionContext.ModelState.AddModelError("CardNumber", "Card number must be between 14-19 digits");

        Task<ActionExecutedContext> Next()
        {
            Assert.Fail("Next should not be called");
            return Task.FromResult(new ActionExecutedContext(
                _actionContext,
                new List<IFilterMetadata>(),
                new object()));
        }

        // Act
        await _filter.OnActionExecutionAsync(_actionContext, Next);

        // Assert
        Assert.That(_actionContext.Result, Is.InstanceOf<BadRequestObjectResult>());
        var result = _actionContext.Result as BadRequestObjectResult;
        Assert.That(result, Is.Not.Null);

        var apiResponse = result.Value as ApiResponse<PostPaymentResponse>;
        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.False);
        Assert.That(apiResponse.Data, Is.Not.Null);
        Assert.That(apiResponse.Data.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(apiResponse.Errors, Contains.Item("Card number must be between 14-19 digits"));

        // Verify payment was stored
        Assert.That(_dbContext.Payments.Count(), Is.EqualTo(1));
        var storedPayment = _dbContext.Payments.First();
        Assert.That(storedPayment.Status, Is.EqualTo(PaymentStatus.Rejected));
    }

    [Test]
    public async Task OnActionExecutionAsync_WithInvalidModelState_AndIdempotencyKey_ChecksForExisting()
    {
        // Arrange
        var idempotencyKey = "test-key-123";
        var existingPayment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Rejected,
            CardNumberLastFour = "",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 100,
            IdempotencyKey = idempotencyKey
        };

        // Store existing payment
        var repository = new PaymentsRepository(_dbContext);
        await repository.AddAsync(existingPayment);

        var request = new PostPaymentRequest
        {
            CardNumber = "123",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 100,
            Cvv = "123"
        };

        _actionContext.ActionArguments["request"] = request;
        _actionContext.HttpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        _actionContext.ModelState.AddModelError("CardNumber", "Card number must be between 14-19 digits");

        Task<ActionExecutedContext> Next()
        {
            Assert.Fail("Next should not be called");
            return Task.FromResult(new ActionExecutedContext(
                _actionContext,
                new List<IFilterMetadata>(),
                new object()));
        }

        // Act
        await _filter.OnActionExecutionAsync(_actionContext, Next);

        // Assert
        Assert.That(_actionContext.Result, Is.InstanceOf<OkObjectResult>());
        var result = _actionContext.Result as OkObjectResult;
        Assert.That(result, Is.Not.Null);

        var apiResponse = result.Value as ApiResponse<PostPaymentResponse>;
        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.False);
        Assert.That(apiResponse.Data, Is.Not.Null);
        Assert.That(apiResponse.Data.Id, Is.EqualTo(existingPayment.Id));
        Assert.That(apiResponse.Errors, Contains.Item("Payment was previously rejected"));

        // Verify no new payment was created
        Assert.That(_dbContext.Payments.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task OnActionExecutionAsync_WithExistingAuthorizedPayment_ReturnsSuccess()
    {
        // Arrange
        var idempotencyKey = "test-key-456";
        var existingPayment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 100,
            IdempotencyKey = idempotencyKey
        };

        // Store existing payment
        var repository = new PaymentsRepository(_dbContext);
        await repository.AddAsync(existingPayment);

        var request = new PostPaymentRequest
        {
            CardNumber = "123",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 100,
            Cvv = "123"
        };

        _actionContext.ActionArguments["request"] = request;
        _actionContext.HttpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        _actionContext.ModelState.AddModelError("CardNumber", "Card number must be between 14-19 digits");

        Task<ActionExecutedContext> Next()
        {
            Assert.Fail("Next should not be called");
            return Task.FromResult(new ActionExecutedContext(
                _actionContext,
                new List<IFilterMetadata>(),
                new object()));
        }

        // Act
        await _filter.OnActionExecutionAsync(_actionContext, Next);

        // Assert
        Assert.That(_actionContext.Result, Is.InstanceOf<OkObjectResult>());
        var result = _actionContext.Result as OkObjectResult;
        Assert.That(result, Is.Not.Null);

        var apiResponse = result.Value as ApiResponse<PostPaymentResponse>;
        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);
        Assert.That(apiResponse.Data, Is.Not.Null);
        Assert.That(apiResponse.Data.Id, Is.EqualTo(existingPayment.Id));
        Assert.That(apiResponse.Data.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(apiResponse.Errors, Is.Empty);

        // Verify no new payment was created
        Assert.That(_dbContext.Payments.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task OnActionExecutionAsync_WithMultipleValidationErrors_IncludesAllErrors()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "123",
            ExpiryMonth = 13,
            ExpiryYear = 2020,
            Currency = "INVALID",
            Amount = -1,
            Cvv = "1"
        };

        _actionContext.ActionArguments["request"] = request;
        _actionContext.ModelState.AddModelError("CardNumber", "Card number must be between 14-19 digits");
        _actionContext.ModelState.AddModelError("ExpiryMonth", "Expiry month must be between 1 and 12");
        _actionContext.ModelState.AddModelError("Currency", "Currency must be USD, GBP, or EUR");

        Task<ActionExecutedContext> Next()
        {
            Assert.Fail("Next should not be called");
            return Task.FromResult(new ActionExecutedContext(
                _actionContext,
                new List<IFilterMetadata>(),
                new object()));
        }

        // Act
        await _filter.OnActionExecutionAsync(_actionContext, Next);

        // Assert
        Assert.That(_actionContext.Result, Is.InstanceOf<BadRequestObjectResult>());
        var result = _actionContext.Result as BadRequestObjectResult;
        Assert.That(result, Is.Not.Null);

        var apiResponse = result.Value as ApiResponse<PostPaymentResponse>;
        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.False);
        Assert.That(apiResponse.Errors.Count, Is.EqualTo(3));
    }
}
