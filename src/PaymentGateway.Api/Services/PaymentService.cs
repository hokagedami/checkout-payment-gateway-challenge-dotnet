using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Helpers;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;

namespace PaymentGateway.Api.Services;

/// <summary>
/// Service implementation for payment operations.
/// Orchestrates payment processing between the bank client and repository.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly IBankClient _bankClient;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentsRepository paymentsRepository,
        IBankClient bankClient,
        ILogger<PaymentService> logger)
    {
        _paymentsRepository = paymentsRepository;
        _bankClient = bankClient;
        _logger = logger;
    }

    public async Task<PostPaymentResponse?> ProcessPaymentAsync(
        string cardNumber,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount,
        string cvv,
        string? idempotencyKey)
    {
        // Check for existing payment with idempotency key
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingPayment = await _paymentsRepository.GetByIdempotencyKeyAsync(idempotencyKey);
            if (existingPayment != null)
            {
                _logger.LogInformation("Returning existing payment for idempotency key: {IdempotencyKey}", idempotencyKey);
                return existingPayment;
            }
        }

        // Prepare bank request
        var bankRequest = new BankPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryDate = $"{expiryMonth:D2}/{expiryYear}",
            Currency = currency,
            Amount = amount,
            Cvv = cvv
        };

        // Process payment with bank
        var bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);

        if (bankResponse == null)
        {
            _logger.LogError("Bank client returned null response for payment");
            return null;
        }

        // Create payment response
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = cardNumber.ExtractLastFourDigits(),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = currency,
            Amount = amount,
            IdempotencyKey = idempotencyKey
        };

        // Store payment
        await _paymentsRepository.AddAsync(payment);

        _logger.LogInformation("Payment processed successfully. ID: {PaymentId}, Status: {Status}",
            payment.Id, payment.Status);

        return payment;
    }

    public async Task<GetPaymentResponse?> GetPaymentByIdAsync(Guid id)
    {
        var payment = await _paymentsRepository.GetAsync(id);

        if (payment == null)
        {
            _logger.LogWarning("Payment not found. ID: {PaymentId}", id);
            return null;
        }

        var response = new GetPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        };

        return response;
    }
}
