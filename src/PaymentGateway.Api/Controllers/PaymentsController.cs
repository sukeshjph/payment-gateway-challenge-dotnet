using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services.Interfaces;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing payment request for amount {Amount} {Currency}", request.Amount, request.Currency);

        var result = await _paymentService.ProcessPaymentAsync(request, cancellationToken);

        if (result.Status == PaymentStatus.Rejected)
        {
            _logger.LogWarning("Payment rejected for amount {Amount} {Currency}", request.Amount, request.Currency);
            return BadRequest(result);
        }

        _logger.LogInformation("Payment {Status} with id {PaymentId}", result.Status, result.Id);
        return CreatedAtAction(
            nameof(GetPayment),
            new { id = result.Id },
            result);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<PostPaymentResponse?> GetPayment(Guid id)
    {
        var payment = _paymentService.GetPayment(id);
        if (payment == null)
        {
            _logger.LogInformation("Payment {PaymentId} not found", id);
            return NotFound();
        }

        return Ok(payment);
    }
}
