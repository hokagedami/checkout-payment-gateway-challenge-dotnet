using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Helpers;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;

namespace PaymentGateway.Api.Middleware;

/// <summary>
/// Action filter that intercepts invalid model state and creates rejected payments
/// </summary>
public class ModelStateValidationFilter : IAsyncActionFilter
{
    private readonly ILogger<ModelStateValidationFilter> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ModelStateValidationFilter(
        ILogger<ModelStateValidationFilter> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            // Only handle PostPaymentRequest validation errors
            var postPaymentRequest = context.ActionArguments.Values
                .OfType<PostPaymentRequest>()
                .FirstOrDefault();

            if (postPaymentRequest != null)
            {
                _logger.LogWarning("Payment request validation failed. Creating rejected payment.");

                // Get idempotency key from headers
                string? idempotencyKey = null;
                if (context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var headerValue))
                {
                    idempotencyKey = headerValue.ToString();
                }

                // Check for existing payment with same idempotency key
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();

                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var existingPayment = await repository.GetByIdempotencyKeyAsync(idempotencyKey);
                    if (existingPayment != null)
                    {
                        // Return existing payment (even if it's rejected) with success=false for rejected
                        var response = existingPayment.Status == PaymentStatus.Rejected
                            ? ApiResponse<PostPaymentResponse>.ErrorResponse(
                                ["Payment was previously rejected"],
                                existingPayment)
                            : ApiResponse<PostPaymentResponse>.SuccessResponse(existingPayment);

                        context.Result = new OkObjectResult(response);
                        return;
                    }
                }

                // Create rejected payment
                var rejectedPayment = new PostPaymentResponse
                {
                    Id = Guid.NewGuid(),
                    Status = PaymentStatus.Rejected,
                    CardNumberLastFour = postPaymentRequest.CardNumber?.ExtractLastFourDigits() ?? string.Empty,
                    ExpiryMonth = postPaymentRequest.ExpiryMonth,
                    ExpiryYear = postPaymentRequest.ExpiryYear,
                    Currency = postPaymentRequest.Currency,
                    Amount = postPaymentRequest.Amount,
                    IdempotencyKey = idempotencyKey
                };

                // Store rejected payment
                await repository.AddAsync(rejectedPayment);

                // Extract error messages
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                    .ToList();

                // Return BadRequest with unified response format
                context.Result = new BadRequestObjectResult(
                    ApiResponse<PostPaymentResponse>.ErrorResponse(errors, rejectedPayment));

                return;
            }

            // For other validation errors (non-payment requests), return standard validation error
            var standardErrors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();

            context.Result = new BadRequestObjectResult(
                ApiResponse<object>.ErrorResponse(standardErrors));
            return;
        }

        // Continue to action if model is valid
        await next();
    }
}
