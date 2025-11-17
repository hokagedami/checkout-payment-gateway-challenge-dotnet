using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Helpers;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly IBankClient _bankClient;

    public PaymentsController(IPaymentsRepository paymentsRepository, IBankClient bankClient)
    {
        _paymentsRepository = paymentsRepository;
        _bankClient = bankClient;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PostPaymentResponse>>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        // Check for existing payment with same idempotency key
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingPayment = await _paymentsRepository.GetByIdempotencyKeyAsync(idempotencyKey);
            if (existingPayment != null)
            {
                return Ok(ApiResponse<PostPaymentResponse>.SuccessResponse(existingPayment));
            }
        }

        var bankRequest = new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        var bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);

        if (bankResponse == null)
        {
            return StatusCode(503, ApiResponse<PostPaymentResponse>.ErrorResponse("Unable to process payment at this time"));
        }

        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber.ExtractLastFourDigits(),
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            IdempotencyKey = idempotencyKey
        };

        await _paymentsRepository.AddAsync(payment);

        return Ok(ApiResponse<PostPaymentResponse>.SuccessResponse(payment));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<GetPaymentResponse>>> GetPaymentAsync(Guid id)
    {
        var payment = await _paymentsRepository.GetAsync(id);

        if (payment == null)
        {
            return NotFound(ApiResponse<GetPaymentResponse>.ErrorResponse("Payment not found"));
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

        return Ok(ApiResponse<GetPaymentResponse>.SuccessResponse(response));
    }
}