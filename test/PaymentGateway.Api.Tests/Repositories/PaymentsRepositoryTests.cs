using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;

namespace PaymentGateway.Api.Tests.Repositories;

public class PaymentsRepositoryTests
{
    [Fact]
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
        Assert.NotNull(retrieved);
        Assert.Equal(payment.Id, retrieved.Id);
        Assert.Equal(payment.Status, retrieved.Status);
        Assert.Equal(payment.CardNumberLastFour, retrieved.CardNumberLastFour);
    }

    [Fact]
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
        Assert.NotNull(result);
        Assert.Equal(paymentId, result.Id);
        Assert.Equal(PaymentStatus.Declined, result.Status);
        Assert.Equal("5678", result.CardNumberLastFour);
        Assert.Equal(6, result.ExpiryMonth);
        Assert.Equal(2027, result.ExpiryYear);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(2500, result.Amount);
    }

    [Fact]
    public void Get_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var repository = new PaymentsRepository();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = repository.Get(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
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

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.NotNull(retrieved3);

        Assert.Equal(payment1.Id, retrieved1.Id);
        Assert.Equal(payment2.Id, retrieved2.Id);
        Assert.Equal(payment3.Id, retrieved3.Id);
    }

    [Fact]
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
        Assert.NotNull(result);
        Assert.Equal(targetId, result.Id);
        Assert.Equal("9999", result.CardNumberLastFour);
        Assert.Equal(PaymentStatus.Declined, result.Status);
    }

    [Fact]
    public void Payments_IsInitiallyEmpty()
    {
        // Arrange & Act
        var repository = new PaymentsRepository();

        // Assert
        Assert.Empty(repository.Payments);
    }

    [Fact]
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
        Assert.Equal(2, repository.Payments.Count);
    }
}
