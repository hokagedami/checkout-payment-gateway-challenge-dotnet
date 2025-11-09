using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Helpers;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly IBankClient _bankClient;

    public PaymentsController(PaymentsRepository paymentsRepository, IBankClient bankClient)
    {
        _paymentsRepository = paymentsRepository;
        _bankClient = bankClient;
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingPayment = _paymentsRepository.GetByIdempotencyKey(idempotencyKey);
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

            _paymentsRepository.Add(rejectedPayment);

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

        _paymentsRepository.Add(payment);

        return Ok(payment);
    }

    [HttpGet("{id:guid}")]
    public Task<ActionResult<GetPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        if (payment == null)
        {
            return Task.FromResult<ActionResult<GetPaymentResponse?>>(NotFound());
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

        return Task.FromResult<ActionResult<GetPaymentResponse?>>(Ok(response));
    }
}