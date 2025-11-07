namespace PaymentGateway.Api.Helpers;

public static class CardNumberExtensions
{
    /// <summary>
    /// Extracts the last four digits from a card number string.
    /// Returns empty string if the card number is null, empty, less than 4 characters, or the last 4 characters are non-numeric.
    /// </summary>
    /// <param name="cardNumber">The card number string</param>
    /// <returns>The last four digits as a string, or empty string if extraction fails</returns>
    public static string ExtractLastFourDigits(this string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length < 4)
        {
            return string.Empty;
        }

        var lastFour = cardNumber[^4..];
        return lastFour.All(char.IsDigit) ? lastFour : string.Empty;
    }
}
