using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Common.Validation;

internal static class PaymentRequestValidator
{
    private static readonly HashSet<string> AllowedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP", "USD", "EUR", "JPY", "CHF", "CAD", "AUD", "INR"
    };

    public static bool TryParseExpiry(string expiryDate, out int month, out int year)
    {
        month = 0;
        year = 0;
        if (string.IsNullOrWhiteSpace(expiryDate))
            return false;
        var parts = expiryDate.Trim().Split('/');
        if (parts.Length != 2)
            return false;
        if (!int.TryParse(parts[0].Trim(), out month) || !int.TryParse(parts[1].Trim(), out year))
            return false;
        if (year < 100)
            year += 2000;
        return true;
    }

    public static bool IsValid(PostPaymentRequest request, int expiryMonth, int expiryYear)
    {
        return HasValidCardNumber(request.CardNumber)
            && IsExpiryValid(expiryMonth, expiryYear)
            && IsCurrencyValid(request.Currency)
            && IsAmountValid(request.Amount)
            && IsCvvValid(request.Cvv);
    }

    public static bool HasValidCardNumber(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 14 || cardNumber.Length > 19 || !cardNumber.All(char.IsDigit))
            return false;
        return PassesLuhnCheck(cardNumber);
    }

    public static bool IsExpiryValid(int expiryMonth, int expiryYear)
    {
        if (expiryMonth < 1 || expiryMonth > 12)
            return false;
        var now = DateTime.UtcNow;
        return expiryYear > now.Year || (expiryYear == now.Year && expiryMonth >= now.Month);
    }

    public static bool IsCurrencyValid(string currency)
    {
        return !string.IsNullOrEmpty(currency)
            && currency.Length == 3
            && AllowedCurrencies.Contains(currency);
    }

    public static bool IsAmountValid(int amount)
    {
        return amount > 0;
    }

    public static bool IsCvvValid(string cvv)
    {
        return !string.IsNullOrEmpty(cvv)
            && cvv.Length >= 3
            && cvv.Length <= 4
            && cvv.All(char.IsDigit);
    }

    private static bool PassesLuhnCheck(string cardNumber)
    {
        var sum = 0;
        var alt = false;
        for (var i = cardNumber.Length - 1; i >= 0; i--)
        {
            var d = cardNumber[i] - '0';
            if (alt)
            {
                d *= 2;
                if (d > 9)
                    d -= 9;
            }
            sum += d;
            alt = !alt;
        }
        return sum % 10 == 0;
    }
}
