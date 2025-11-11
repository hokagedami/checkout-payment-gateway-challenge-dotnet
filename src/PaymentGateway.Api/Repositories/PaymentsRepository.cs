using Microsoft.EntityFrameworkCore;
using PaymentGateway.Api.Data;
using PaymentGateway.Api.Models.Entities;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Repositories;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly PaymentGatewayDbContext _context;

    public PaymentsRepository(PaymentGatewayDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PostPaymentResponse payment)
    {
        var entity = new Payment
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount,
            IdempotencyKey = payment.IdempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<PostPaymentResponse?> GetAsync(Guid id)
    {
        var payment = await _context.Payments.FindAsync(id);

        if (payment == null)
            return null;

        return MapToResponse(payment);
    }

    public async Task<PostPaymentResponse?> GetByIdempotencyKeyAsync(string idempotencyKey)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);

        if (payment == null)
            return null;

        return MapToResponse(payment);
    }

    private static PostPaymentResponse MapToResponse(Payment payment)
    {
        return new PostPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount,
            IdempotencyKey = payment.IdempotencyKey
        };
    }
}