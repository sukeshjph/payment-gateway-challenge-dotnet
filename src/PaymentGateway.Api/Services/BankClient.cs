using System.Net.Http.Json;
using PaymentGateway.Api.Models.BankSimulator;
using PaymentGateway.Api.Services.Interfaces;

namespace PaymentGateway.Api.Services;

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;

    public BankClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BankPaymentResponse?> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("payments", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
