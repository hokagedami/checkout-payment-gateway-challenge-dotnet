using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Repositories;

public interface IPaymentsRepository
{
    Task AddAsync(PostPaymentResponse payment);
    Task<PostPaymentResponse?> GetAsync(Guid id);
    Task<PostPaymentResponse?> GetByIdempotencyKeyAsync(string idempotencyKey);
}
