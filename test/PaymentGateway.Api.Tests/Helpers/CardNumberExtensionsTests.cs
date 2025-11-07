using PaymentGateway.Api.Helpers;

namespace PaymentGateway.Api.Tests.Helpers;

public class CardNumberExtensionsTests
{
    [Fact]
    public void ExtractLastFourDigits_WithValidCardNumber_ReturnsLastFourDigits()
    {
        // Arrange
        const string cardNumber = "1234567890123456";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal("3456", result);
    }

    [Theory]
    [InlineData("4532015112830366", "0366")]
    [InlineData("5425233430109903", "9903")]
    [InlineData("2222405343248877", "8877")]
    [InlineData("12345678901234", "1234")]
    [InlineData("1234567890123456789", "6789")]
    public void ExtractLastFourDigits_WithVariousValidCardNumbers_ReturnsCorrectDigits(string cardNumber, string expected)
    {
        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractLastFourDigits_WithNullCardNumber_ReturnsEmptyString()
    {
        // Arrange
        string? cardNumber = null;

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractLastFourDigits_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var cardNumber = string.Empty;

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractLastFourDigits_WithWhitespace_ReturnsEmptyString()
    {
        // Arrange
        const string cardNumber = "   ";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData("123")]
    public void ExtractLastFourDigits_WithLessThanFourCharacters_ReturnsEmptyString(string cardNumber)
    {
        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractLastFourDigits_WithExactlyFourDigits_ReturnsThoseDigits()
    {
        // Arrange
        var cardNumber = "1234";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal("1234", result);
    }

    [Theory]
    [InlineData("123456789012abcd")]
    [InlineData("12345678901234ab")]
    [InlineData("abcd1234567890ab")]
    [InlineData("1234567890123xyz")]
    public void ExtractLastFourDigits_WithNonNumericLastFourCharacters_ReturnsEmptyString(string cardNumber)
    {
        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractLastFourDigits_WithLeadingZeros_PreservesZeros()
    {
        // Arrange
        var cardNumber = "1234567890120001";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal("0001", result); // Now preserves leading zeros
    }

    [Fact]
    public void ExtractLastFourDigits_WithAllZeros_ReturnsZeroString()
    {
        // Arrange
        var cardNumber = "1234567890120000";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal("0000", result);
    }

    [Theory]
    [InlineData("1234-5678-9012-3456", "3456")]
    [InlineData("1234 5678 9012 3456", "3456")]
    public void ExtractLastFourDigits_WithFormattedCardNumber_ExtractsLastFourCharacters(string cardNumber, string expected)
    {
        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractLastFourDigits_WithVeryLongCardNumber_ExtractsOnlyLastFour()
    {
        // Arrange
        const string cardNumber = "12345678901234567890";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.Equal("7890", result);
    }
}
