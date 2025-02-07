using PaymentGateway.Api.Models.BankSimulator;
using PaymentGateway.Api.Services.Interfaces;

namespace PaymentGateway.Api.Tests;

public class FakeBankClient : IBankClient
{
    public BankPaymentResponse? Response { get; set; }

    public Task<BankPaymentResponse?> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Response);
}
