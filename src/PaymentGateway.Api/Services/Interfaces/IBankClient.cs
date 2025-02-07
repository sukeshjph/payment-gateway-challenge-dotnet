using PaymentGateway.Api.Models.BankSimulator;

namespace PaymentGateway.Api.Services.Interfaces;

public interface IBankClient
{
    Task<BankPaymentResponse?> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken = default);
}
