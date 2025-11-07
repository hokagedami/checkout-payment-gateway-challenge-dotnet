using PaymentGateway.Api.Helpers;

namespace PaymentGateway.Api.Tests.Helpers;

[TestFixture]
public class CardNumberExtensionsTests
{
    [Test]
    public void ExtractLastFourDigits_WithValidCardNumber_ReturnsLastFourDigits()
    {
        // Arrange
        const string cardNumber = "1234567890123456";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo("3456"));
    }

    [TestCase("4532015112830366", "0366")]
    [TestCase("5425233430109903", "9903")]
    [TestCase("2222405343248877", "8877")]
    [TestCase("12345678901234", "1234")]
    [TestCase("1234567890123456789", "6789")]
    public void ExtractLastFourDigits_WithVariousValidCardNumbers_ReturnsCorrectDigits(string cardNumber, string expected)
    {
        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ExtractLastFourDigits_WithNullCardNumber_ReturnsEmptyString()
    {
        // Arrange
        string? cardNumber = null;

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractLastFourDigits_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var cardNumber = string.Empty;

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractLastFourDigits_WithWhitespace_ReturnsEmptyString()
    {
        // Arrange
        const string cardNumber = "   ";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [TestCase("1")]
    [TestCase("12")]
    [TestCase("123")]
    public void ExtractLastFourDigits_WithLessThanFourCharacters_ReturnsEmptyString(string cardNumber)
    {
        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractLastFourDigits_WithExactlyFourDigits_ReturnsThoseDigits()
    {
        // Arrange
        var cardNumber = "1234";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo("1234"));
    }

    [TestCase("123456789012abcd")]
    [TestCase("12345678901234ab")]
    [TestCase("abcd1234567890ab")]
    [TestCase("1234567890123xyz")]
    public void ExtractLastFourDigits_WithNonNumericLastFourCharacters_ReturnsEmptyString(string cardNumber)
    {
        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractLastFourDigits_WithLeadingZeros_PreservesZeros()
    {
        // Arrange
        var cardNumber = "1234567890120001";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo("0001")); // Now preserves leading zeros
    }

    [Test]
    public void ExtractLastFourDigits_WithAllZeros_ReturnsZeroString()
    {
        // Arrange
        var cardNumber = "1234567890120000";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo("0000"));
    }

    [TestCase("1234-5678-9012-3456", "3456")]
    [TestCase("1234 5678 9012 3456", "3456")]
    public void ExtractLastFourDigits_WithFormattedCardNumber_ExtractsLastFourCharacters(string cardNumber, string expected)
    {
        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ExtractLastFourDigits_WithVeryLongCardNumber_ExtractsOnlyLastFour()
    {
        // Arrange
        const string cardNumber = "12345678901234567890";

        // Act
        var result = cardNumber.ExtractLastFourDigits();

        // Assert
        Assert.That(result, Is.EqualTo("7890"));
    }
}
