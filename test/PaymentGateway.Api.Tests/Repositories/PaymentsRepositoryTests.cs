using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;

namespace PaymentGateway.Api.Tests.Repositories;

[TestFixture]
public class PaymentsRepositoryTests
{
    [Test]
    public void Add_StoresPaymentSuccessfully()
    {
        // Arrange
        var repository = new PaymentsRepository();
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
        repository.Add(payment);

        // Assert
        var retrieved = repository.Get(payment.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Id, Is.EqualTo(payment.Id));
        Assert.That(retrieved.Status, Is.EqualTo(payment.Status));
        Assert.That(retrieved.CardNumberLastFour, Is.EqualTo(payment.CardNumberLastFour));
    }

    [Test]
    public void Get_WithValidId_ReturnsPayment()
    {
        // Arrange
        var repository = new PaymentsRepository();
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

        repository.Add(payment);

        // Act
        var result = repository.Get(paymentId);

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
    public void Get_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var repository = new PaymentsRepository();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = repository.Get(nonExistentId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Add_MultiplePayments_AllAreStored()
    {
        // Arrange
        var repository = new PaymentsRepository();
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
        repository.Add(payment1);
        repository.Add(payment2);
        repository.Add(payment3);

        // Assert
        var retrieved1 = repository.Get(payment1.Id);
        var retrieved2 = repository.Get(payment2.Id);
        var retrieved3 = repository.Get(payment3.Id);

        Assert.That(retrieved1, Is.Not.Null);
        Assert.That(retrieved2, Is.Not.Null);
        Assert.That(retrieved3, Is.Not.Null);

        Assert.That(retrieved1.Id, Is.EqualTo(payment1.Id));
        Assert.That(retrieved2.Id, Is.EqualTo(payment2.Id));
        Assert.That(retrieved3.Id, Is.EqualTo(payment3.Id));
    }

    [Test]
    public void Get_AfterMultipleAdds_ReturnsCorrectPayment()
    {
        // Arrange
        var repository = new PaymentsRepository();
        var targetId = Guid.NewGuid();

        // Add some payments
        repository.Add(new PostPaymentResponse
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
        repository.Add(targetPayment);

        // Add more payments
        repository.Add(new PostPaymentResponse
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
        var result = repository.Get(targetId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(targetId));
        Assert.That(result.CardNumberLastFour, Is.EqualTo("9999"));
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Declined));
    }

    [Test]
    public void Payments_IsInitiallyEmpty()
    {
        // Arrange & Act
        var repository = new PaymentsRepository();

        // Assert
        Assert.That(repository.Payments, Is.Empty);
    }

    [Test]
    public void Add_IncrementsPaymentCount()
    {
        // Arrange
        var repository = new PaymentsRepository();

        // Act
        repository.Add(new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1234",
            ExpiryMonth = 12,
            ExpiryYear = 2026,
            Currency = "GBP",
            Amount = 100
        });

        repository.Add(new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "5678",
            ExpiryMonth = 6,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 200
        });

        // Assert
        Assert.That(repository.Payments.Count, Is.EqualTo(2));
    }

    [Test]
    public void GetByIdempotencyKey_WithValidKey_ReturnsPayment()
    {
        // Arrange
        var repository = new PaymentsRepository();
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

        repository.Add(payment);

        // Act
        var result = repository.GetByIdempotencyKey(idempotencyKey);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(payment.Id));
        Assert.That(result.IdempotencyKey, Is.EqualTo(idempotencyKey));
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Authorized));
    }

    [Test]
    public void GetByIdempotencyKey_WithInvalidKey_ReturnsNull()
    {
        // Arrange
        var repository = new PaymentsRepository();
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

        repository.Add(payment);

        // Act
        var result = repository.GetByIdempotencyKey("non-existent-key");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetByIdempotencyKey_WithNullIdempotencyKey_ReturnsNull()
    {
        // Arrange
        var repository = new PaymentsRepository();
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

        repository.Add(payment);

        // Act
        var result = repository.GetByIdempotencyKey("some-key");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetByIdempotencyKey_WithMultiplePayments_ReturnsCorrectOne()
    {
        // Arrange
        var repository = new PaymentsRepository();
        var targetKey = "target-key";

        repository.Add(new PostPaymentResponse
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
        repository.Add(targetPayment);

        repository.Add(new PostPaymentResponse
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
        var result = repository.GetByIdempotencyKey(targetKey);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(targetPayment.Id));
        Assert.That(result.CardNumberLastFour, Is.EqualTo("9999"));
        Assert.That(result.IdempotencyKey, Is.EqualTo(targetKey));
    }
}
