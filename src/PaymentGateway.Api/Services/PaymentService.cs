using PaymentGateway.Api.Common.Validation;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.BankSimulator;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services.Interfaces;

namespace PaymentGateway.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly IBankClient _bankClient;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentService(IBankClient bankClient, IPaymentsRepository paymentsRepository)
    {
        _bankClient = bankClient;
        _paymentsRepository = paymentsRepository;
    }

    public PostPaymentResponse? GetPayment(Guid id)
    {
        var payment = _paymentsRepository.Get(id);
        return payment == null ? null : ToResponse(payment);
    }

    public async Task<PostPaymentResponse> ProcessPaymentAsync(PostPaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (!PaymentRequestValidator.TryParseExpiry(request.ExpiryDate, out var expiryMonth, out var expiryYear)
            || !PaymentRequestValidator.IsValid(request, expiryMonth, expiryYear))
        {
            var rejected = CreatePayment(request, PaymentStatus.Rejected, expiryMonth, expiryYear);
            _paymentsRepository.Add(rejected);
            return ToResponse(rejected);
        }

        var bankRequest = CreateBankPaymentRequest(request);
        var bankResponse = await _bankClient.AuthorizeAsync(bankRequest, cancellationToken);
        var status = MapToPaymentStatus(bankResponse);
        var payment = CreatePayment(request, status, expiryMonth, expiryYear, bankResponse?.AuthorizationCode);
        _paymentsRepository.Add(payment);
        return ToResponse(payment);
    }

    private static PostPaymentResponse ToResponse(Payment p)
    {
        return new PostPaymentResponse
        {
            Id = p.Id,
            Status = p.Status,
            CardNumber = p.CardNumber,
            ExpiryMonth = p.ExpiryMonth,
            ExpiryYear = p.ExpiryYear,
            Currency = p.Currency,
            Amount = p.Amount,
            AuthorizationCode = p.AuthorizationCode
        };
    }

    private static BankPaymentRequest CreateBankPaymentRequest(PostPaymentRequest request)
    {
        return new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = request.ExpiryDate,
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };
    }

    private static PaymentStatus MapToPaymentStatus(BankPaymentResponse? bankResponse)
    {
        if (bankResponse == null)
            return PaymentStatus.Rejected;
        return bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined;
    }

    private static Payment CreatePayment(
        PostPaymentRequest request,
        PaymentStatus status,
        int expiryMonth,
        int expiryYear,
        string? authorizationCode = null)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            Status = status,
            CardNumber = MaskCardNumber(request.CardNumber),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            AuthorizationCode = authorizationCode
        };
    }

    private static string MaskCardNumber(string cardNumber) =>
        cardNumber.Length >= 4 ? cardNumber[^4..] : cardNumber;
}
