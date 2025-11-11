using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Helpers;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Authorize]
[EnableRateLimiting("fixed")]
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
    [EnableRateLimiting("payments")]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingPayment = await _paymentsRepository.GetByIdempotencyKeyAsync(idempotencyKey);
            if (existingPayment != null)
            {
                return Ok(existingPayment);
            }
        }

        if (!ModelState.IsValid)
        {
            var rejectedPayment = new PostPaymentResponse
            {
                Id = Guid.NewGuid(),
                Status = PaymentStatus.Rejected,
                CardNumberLastFour = request.CardNumber.ExtractLastFourDigits(),
                ExpiryMonth = request.ExpiryMonth,
                ExpiryYear = request.ExpiryYear,
                Currency = request.Currency,
                Amount = request.Amount,
                IdempotencyKey = idempotencyKey
            };

            await _paymentsRepository.AddAsync(rejectedPayment);

            return BadRequest(new
            {
                payment = rejectedPayment,
                errors = ModelState
            });
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
            return StatusCode(503, new { error = "Unable to process payment at this time" });
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

        return Ok(payment);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        var payment = await _paymentsRepository.GetAsync(id);

        if (payment == null)
        {
            return NotFound();
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

        return Ok(response);
    }
}