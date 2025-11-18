using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

/// <summary>
/// Service interface for payment operations.
/// Provides business logic layer between controller and repository.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Processes a new payment request with optional idempotency support.
    /// Handles idempotency checks, bank processing, and payment storage.
    /// </summary>
    /// <param name="cardNumber">The card number for the payment</param>
    /// <param name="expiryMonth">The card expiry month</param>
    /// <param name="expiryYear">The card expiry year</param>
    /// <param name="currency">The payment currency (e.g., USD, GBP, EUR)</param>
    /// <param name="amount">The payment amount</param>
    /// <param name="cvv">The card CVV</param>
    /// <param name="idempotencyKey">Optional idempotency key to prevent duplicate payments</param>
    /// <returns>The payment response, or null if bank processing fails</returns>
    Task<PostPaymentResponse?> ProcessPaymentAsync(
        string cardNumber,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount,
        string cvv,
        string? idempotencyKey);

    /// <summary>
    /// Retrieves a payment by its unique identifier.
    /// </summary>
    /// <param name="id">The payment ID</param>
    /// <returns>The payment details, or null if not found</returns>
    Task<GetPaymentResponse?> GetPaymentByIdAsync(Guid id);
}
