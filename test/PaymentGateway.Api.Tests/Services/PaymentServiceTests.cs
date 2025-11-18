using Moq;
using NUnit.Framework;
using Microsoft.Extensions.Logging;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

/// <summary>
/// Unit tests for PaymentService.
/// Tests the business logic layer between controller and repository.
/// </summary>
[TestFixture]
public class PaymentServiceTests
{
    private Mock<IPaymentsRepository> _mockRepository = null!;
    private Mock<IBankClient> _mockBankClient = null!;
    private Mock<ILogger<PaymentService>> _mockLogger = null!;
    private PaymentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockRepository = new Mock<IPaymentsRepository>();
        _mockBankClient = new Mock<IBankClient>();
        _mockLogger = new Mock<ILogger<PaymentService>>();
        _service = new PaymentService(_mockRepository.Object, _mockBankClient.Object, _mockLogger.Object);
    }

    [Test]
    public async Task ProcessPaymentAsync_WithValidRequest_ReturnsAuthorizedPayment()
    {
        // Arrange
        var bankResponse = new BankPaymentResponse
        {
            Authorized = true,
            AuthorizationCode = "ABC123"
        };
        _mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(bankResponse);

        // Act
        var result = await _service.ProcessPaymentAsync(
            "2222405343248877",
            12,
            2026,
            "USD",
            100,
            "123",
            null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(result.CardNumberLastFour, Is.EqualTo("8877"));
        Assert.That(result.Amount, Is.EqualTo(100));
        Assert.That(result.Currency, Is.EqualTo("USD"));
        _mockRepository.Verify(x => x.AddAsync(It.IsAny<PostPaymentResponse>()), Times.Once);
    }

    [Test]
    public async Task ProcessPaymentAsync_WithDeclinedResponse_ReturnsDeclinedPayment()
    {
        // Arrange
        var bankResponse = new BankPaymentResponse
        {
            Authorized = false,
            AuthorizationCode = null
        };
        _mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(bankResponse);

        // Act
        var result = await _service.ProcessPaymentAsync(
            "2222405343248877",
            12,
            2026,
            "USD",
            100,
            "123",
            null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Declined));
        _mockRepository.Verify(x => x.AddAsync(It.IsAny<PostPaymentResponse>()), Times.Once);
    }

    [Test]
    public async Task ProcessPaymentAsync_WhenBankClientReturnsNull_ReturnsNull()
    {
        // Arrange
        _mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync((BankPaymentResponse?)null);

        // Act
        var result = await _service.ProcessPaymentAsync(
            "2222405343248877",
            12,
            2026,
            "USD",
            100,
            "123",
            null);

        // Assert
        Assert.That(result, Is.Null);
        _mockRepository.Verify(x => x.AddAsync(It.IsAny<PostPaymentResponse>()), Times.Never);
    }

    [Test]
    public async Task ProcessPaymentAsync_WithIdempotencyKey_ChecksForExistingPayment()
    {
        // Arrange
        var existingPayment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 100,
            IdempotencyKey = "test-key-123"
        };

        _mockRepository.Setup(x => x.GetByIdempotencyKeyAsync("test-key-123"))
            .ReturnsAsync(existingPayment);

        // Act
        var result = await _service.ProcessPaymentAsync(
            "2222405343248877",
            12,
            2026,
            "USD",
            100,
            "123",
            "test-key-123");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(existingPayment.Id));
        Assert.That(result.IdempotencyKey, Is.EqualTo("test-key-123"));
        _mockBankClient.Verify(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Never);
        _mockRepository.Verify(x => x.AddAsync(It.IsAny<PostPaymentResponse>()), Times.Never);
    }

    [Test]
    public async Task ProcessPaymentAsync_WithNewIdempotencyKey_ProcessesPayment()
    {
        // Arrange
        _mockRepository.Setup(x => x.GetByIdempotencyKeyAsync("new-key-123"))
            .ReturnsAsync((PostPaymentResponse?)null);

        var bankResponse = new BankPaymentResponse
        {
            Authorized = true,
            AuthorizationCode = "ABC123"
        };
        _mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(bankResponse);

        // Act
        var result = await _service.ProcessPaymentAsync(
            "2222405343248877",
            12,
            2026,
            "USD",
            100,
            "123",
            "new-key-123");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IdempotencyKey, Is.EqualTo("new-key-123"));
        _mockBankClient.Verify(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Once);
        _mockRepository.Verify(x => x.AddAsync(It.IsAny<PostPaymentResponse>()), Times.Once);
    }

    [Test]
    public async Task ProcessPaymentAsync_WithoutIdempotencyKey_ProcessesPaymentDirectly()
    {
        // Arrange
        var bankResponse = new BankPaymentResponse
        {
            Authorized = true,
            AuthorizationCode = "ABC123"
        };
        _mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(bankResponse);

        // Act
        var result = await _service.ProcessPaymentAsync(
            "2222405343248877",
            12,
            2026,
            "USD",
            100,
            "123",
            null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IdempotencyKey, Is.Null);
        _mockRepository.Verify(x => x.GetByIdempotencyKeyAsync(It.IsAny<string>()), Times.Never);
        _mockBankClient.Verify(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Once);
    }

    [Test]
    public async Task ProcessPaymentAsync_FormatsExpiryDateCorrectly()
    {
        // Arrange
        BankPaymentRequest? capturedRequest = null;
        _mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .Callback<BankPaymentRequest>(req => capturedRequest = req)
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "ABC123" });

        // Act
        await _service.ProcessPaymentAsync(
            "2222405343248877",
            3,
            2027,
            "USD",
            100,
            "123",
            null);

        // Assert
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest.ExpiryDate, Is.EqualTo("03/2027"));
    }

    [Test]
    public async Task ProcessPaymentAsync_ExtractsLastFourDigitsCorrectly()
    {
        // Arrange
        var bankResponse = new BankPaymentResponse { Authorized = true, AuthorizationCode = "ABC123" };
        _mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(bankResponse);

        // Act
        var result = await _service.ProcessPaymentAsync(
            "1234567890123456",
            12,
            2026,
            "USD",
            100,
            "123",
            null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.CardNumberLastFour, Is.EqualTo("3456"));
    }

    [Test]
    public async Task GetPaymentByIdAsync_WithExistingPayment_ReturnsPayment()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var storedPayment = new PostPaymentResponse
        {
            Id = paymentId,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "USD",
            Amount = 100
        };

        _mockRepository.Setup(x => x.GetAsync(paymentId))
            .ReturnsAsync(storedPayment);

        // Act
        var result = await _service.GetPaymentByIdAsync(paymentId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(paymentId));
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Authorized));
        Assert.That(result.CardNumberLastFour, Is.EqualTo("8877"));
        Assert.That(result.Amount, Is.EqualTo(100));
    }

    [Test]
    public async Task GetPaymentByIdAsync_WithNonExistentPayment_ReturnsNull()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _mockRepository.Setup(x => x.GetAsync(paymentId))
            .ReturnsAsync((PostPaymentResponse?)null);

        // Act
        var result = await _service.GetPaymentByIdAsync(paymentId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetPaymentByIdAsync_MapsAllFieldsCorrectly()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var storedPayment = new PostPaymentResponse
        {
            Id = paymentId,
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "1234",
            ExpiryMonth = 6,
            ExpiryYear = 2025,
            Currency = "EUR",
            Amount = 500
        };

        _mockRepository.Setup(x => x.GetAsync(paymentId))
            .ReturnsAsync(storedPayment);

        // Act
        var result = await _service.GetPaymentByIdAsync(paymentId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(paymentId));
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Declined));
        Assert.That(result.CardNumberLastFour, Is.EqualTo("1234"));
        Assert.That(result.ExpiryMonth, Is.EqualTo(6));
        Assert.That(result.ExpiryYear, Is.EqualTo(2025));
        Assert.That(result.Currency, Is.EqualTo("EUR"));
        Assert.That(result.Amount, Is.EqualTo(500));
    }
}
