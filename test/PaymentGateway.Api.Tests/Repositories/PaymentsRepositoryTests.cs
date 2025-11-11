using Microsoft.EntityFrameworkCore;
using PaymentGateway.Api.Data;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;

namespace PaymentGateway.Api.Tests.Repositories;

[TestFixture]
public class PaymentsRepositoryTests
{
    private PaymentGatewayDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PaymentGatewayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PaymentGatewayDbContext(options);
    }

    [Test]
    public async Task Add_StoresPaymentSuccessfully()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1234",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000
        };

        // Act
        await repository.AddAsync(payment);

        // Assert
        var retrieved = await repository.GetAsync(payment.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Id, Is.EqualTo(payment.Id));
        Assert.That(retrieved.Status, Is.EqualTo(payment.Status));
        Assert.That(retrieved.CardNumberLastFour, Is.EqualTo(payment.CardNumberLastFour));
    }

    [Test]
    public async Task Get_WithValidId_ReturnsPayment()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var paymentId = Guid.NewGuid();
        var payment = new PostPaymentResponse
        {
            Id = paymentId,
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "5678",
            ExpiryMonth = 6,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 2500
        };

        await repository.AddAsync(payment);

        // Act
        var result = await repository.GetAsync(paymentId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(paymentId));
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Declined));
        Assert.That(result.CardNumberLastFour, Is.EqualTo("5678"));
        Assert.That(result.ExpiryMonth, Is.EqualTo(6));
        Assert.That(result.ExpiryYear, Is.EqualTo(2027));
        Assert.That(result.Currency, Is.EqualTo("USD"));
        Assert.That(result.Amount, Is.EqualTo(2500));
    }

    [Test]
    public async Task Get_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.GetAsync(nonExistentId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Add_MultiplePayments_AllAreStored()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var payment1 = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1111",
            ExpiryMonth = 1,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100
        };

        var payment2 = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "2222",
            ExpiryMonth = 2,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 200
        };

        var payment3 = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "3333",
            ExpiryMonth = 3,
            ExpiryYear = 2028,
            Currency = "EUR",
            Amount = 300
        };

        // Act
        await repository.AddAsync(payment1);
        await repository.AddAsync(payment2);
        await repository.AddAsync(payment3);

        // Assert
        var retrieved1 = await repository.GetAsync(payment1.Id);
        var retrieved2 = await repository.GetAsync(payment2.Id);
        var retrieved3 = await repository.GetAsync(payment3.Id);

        Assert.That(retrieved1, Is.Not.Null);
        Assert.That(retrieved2, Is.Not.Null);
        Assert.That(retrieved3, Is.Not.Null);

        Assert.That(retrieved1.Id, Is.EqualTo(payment1.Id));
        Assert.That(retrieved2.Id, Is.EqualTo(payment2.Id));
        Assert.That(retrieved3.Id, Is.EqualTo(payment3.Id));
    }

    [Test]
    public async Task Get_AfterMultipleAdds_ReturnsCorrectPayment()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var targetId = Guid.NewGuid();

        // Add some payments
        await repository.AddAsync(new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1111",
            ExpiryMonth = 1,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100
        });

        // Add target payment
        var targetPayment = new PostPaymentResponse
        {
            Id = targetId,
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "9999",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "EUR",
            Amount = 5000
        };
        await repository.AddAsync(targetPayment);

        // Add more payments
        await repository.AddAsync(new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "2222",
            ExpiryMonth = 6,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 300
        });

        // Act
        var result = await repository.GetAsync(targetId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(targetId));
        Assert.That(result.CardNumberLastFour, Is.EqualTo("9999"));
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Declined));
    }

    [Test]
    public async Task GetByIdempotencyKey_WithValidKey_ReturnsPayment()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var idempotencyKey = "test-key-123";
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1234",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            IdempotencyKey = idempotencyKey
        };

        await repository.AddAsync(payment);

        // Act
        var result = await repository.GetByIdempotencyKeyAsync(idempotencyKey);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(payment.Id));
        Assert.That(result.IdempotencyKey, Is.EqualTo(idempotencyKey));
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Authorized));
    }

    [Test]
    public async Task GetByIdempotencyKey_WithInvalidKey_ReturnsNull()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1234",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            IdempotencyKey = "key-1"
        };

        await repository.AddAsync(payment);

        // Act
        var result = await repository.GetByIdempotencyKeyAsync("non-existent-key");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByIdempotencyKey_WithNullIdempotencyKey_ReturnsNull()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1234",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 1000,
            IdempotencyKey = null
        };

        await repository.AddAsync(payment);

        // Act
        var result = await repository.GetByIdempotencyKeyAsync("some-key");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByIdempotencyKey_WithMultiplePayments_ReturnsCorrectOne()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new PaymentsRepository(context);
        var targetKey = "target-key";

        await repository.AddAsync(new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1111",
            ExpiryMonth = 1,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100,
            IdempotencyKey = "key-1"
        });

        var targetPayment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "9999",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "EUR",
            Amount = 5000,
            IdempotencyKey = targetKey
        };
        await repository.AddAsync(targetPayment);

        await repository.AddAsync(new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "2222",
            ExpiryMonth = 6,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 300,
            IdempotencyKey = "key-2"
        });

        // Act
        var result = await repository.GetByIdempotencyKeyAsync(targetKey);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(targetPayment.Id));
        Assert.That(result.CardNumberLastFour, Is.EqualTo("9999"));
        Assert.That(result.IdempotencyKey, Is.EqualTo(targetKey));
    }
}
