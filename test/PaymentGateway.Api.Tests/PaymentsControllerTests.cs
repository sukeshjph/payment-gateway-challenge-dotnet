using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.BankSimulator;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services.Interfaces;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private static PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryDate = "04/2030",
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private static WebApplicationFactory<Program> CreateFactory(
        BankPaymentResponse? bankResponse = null,
        IPaymentsRepository? repository = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IBankClient>(new FakeBankClient { Response = bankResponse });
                if (repository != null)
                    services.AddSingleton<IPaymentsRepository>(repository);
            }));
    }

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumber = "8877",
            ExpiryYear = 2030,
            ExpiryMonth = 4,
            Amount = 1000,
            Currency = "GBP"
        };

        var repository = new PaymentsRepository();
        repository.Add(payment);

        var client = CreateFactory(repository: repository).CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse!.Id);
        Assert.Equal(payment.Status, paymentResponse.Status);
        Assert.Equal(payment.CardNumber, paymentResponse.CardNumber);
        Assert.Equal(payment.Amount, paymentResponse.Amount);
        Assert.Equal(payment.Currency, paymentResponse.Currency);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        var client = CreateFactory().CreateClient();

        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_Authorized_Returns201WithAuthorizedStatusAndLocationHeader()
    {
        // Arrange
        var client = CreateFactory(new BankPaymentResponse
        {
            Authorized = true,
            AuthorizationCode = "auth-code-123"
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(paymentResponse!.Id.ToString(), response.Headers.Location!.ToString());
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal("8877", paymentResponse.CardNumber); // last 4 digits only
        Assert.Equal("auth-code-123", paymentResponse.AuthorizationCode);
        Assert.Equal(100, paymentResponse.Amount);
        Assert.Equal("GBP", paymentResponse.Currency);
    }

    [Fact]
    public async Task PostPayment_Declined_Returns201WithDeclinedStatus()
    {
        // Arrange
        var client = CreateFactory(new BankPaymentResponse
        {
            Authorized = false,
            AuthorizationCode = ""
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse!.Status);
        Assert.Equal("8877", paymentResponse.CardNumber);
    }

    [Fact]
    public async Task PostPayment_InvalidCardNumber_Returns400WithRejectedStatus()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.CardNumber = "1234"; // too short, fails validation

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Rejected, paymentResponse!.Status);
    }

    [Fact]
    public async Task PostPayment_ExpiredCard_Returns400WithRejectedStatus()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.ExpiryDate = "01/2020";

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Rejected, paymentResponse!.Status);
    }

    [Fact]
    public async Task PostPayment_InvalidCvv_Returns400WithRejectedStatus()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.Cvv = "12"; // too short

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Rejected, paymentResponse!.Status);
    }

    [Fact]
    public async Task PostPayment_BankUnavailable_Returns400WithRejectedStatus()
    {
        // Arrange — null simulates bank 503 / network failure
        var client = CreateFactory(bankResponse: null).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Rejected, paymentResponse!.Status);
    }

    [Fact]
    public async Task PostPayment_StoresPaymentAndCanBeRetrievedByGet()
    {
        // Arrange
        var client = CreateFactory(new BankPaymentResponse { Authorized = true, AuthorizationCode = "code" }).CreateClient();

        // Act — POST then GET
        var postResponse = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var posted = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.NotNull(posted);

        var getResponse = await client.GetAsync($"/api/Payments/{posted!.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrieved);
        Assert.Equal(posted.Id, retrieved!.Id);
        Assert.Equal(posted.Status, retrieved.Status);
        Assert.Equal(posted.CardNumber, retrieved.CardNumber);
    }
}
