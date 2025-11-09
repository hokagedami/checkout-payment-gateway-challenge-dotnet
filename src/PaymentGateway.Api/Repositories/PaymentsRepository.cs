using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Repositories;

public class PaymentsRepository
{
    public readonly List<PostPaymentResponse> Payments = [];

    public void Add(PostPaymentResponse payment)
    {
        Payments.Add(payment);
    }

    public PostPaymentResponse? Get(Guid id)
    {
        return Payments.FirstOrDefault(p => p.Id == id);
    }

    public PostPaymentResponse? GetByIdempotencyKey(string idempotencyKey)
    {
        return Payments.FirstOrDefault(p => p.IdempotencyKey == idempotencyKey);
    }
}