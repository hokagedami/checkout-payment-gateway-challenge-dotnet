using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PostPaymentResponse>>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var payment = await _paymentService.ProcessPaymentAsync(
            request.CardNumber,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency,
            request.Amount,
            request.Cvv,
            idempotencyKey);

        if (payment == null)
        {
            return StatusCode(503, ApiResponse<PostPaymentResponse>.ErrorResponse("Unable to process payment at this time"));
        }

        return Ok(ApiResponse<PostPaymentResponse>.SuccessResponse(payment));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<GetPaymentResponse>>> GetPaymentAsync(Guid id)
    {
        var payment = await _paymentService.GetPaymentByIdAsync(id);

        if (payment == null)
        {
            return NotFound(ApiResponse<GetPaymentResponse>.ErrorResponse("Payment not found"));
        }

        return Ok(ApiResponse<GetPaymentResponse>.SuccessResponse(payment));
    }
}