using PlikShare.Users.Entities;

namespace PlikShare.Tests;

public class EmailAnonymizationTests
{
    [Fact]
    public void Anonymize_ShouldCorrectlyAnonymizeEmail()
    {
        // Arrange
        var email = new Email("test@example.com");
        var expected = "t**t@e*****e.com";

        // Act
        var result = email.Anonymize();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Anonymize_ShouldHandleEmailsWithDifferentLengths()
    {
        // Arrange
        var emailShort = new Email("a@b.cd");
        var emailLong = new Email("longemailaddress@verylongdomainname.com");
        var expectedShort = "a@b.cd";
        var expectedLong = "long********ress@v****************e.com";

        // Act
        var resultShort = emailShort.Anonymize();
        var resultLong = emailLong.Anonymize();

        // Assert
        Assert.Equal(expectedShort, resultShort);
        Assert.Equal(expectedLong, resultLong);
    }

    [Fact]
    public void Anonymize_ShouldRetainCaseSensitivity()
    {
        // Arrange
        var email = new Email("Test@Example.com");
        var expected = "t**t@e*****e.com"; // Assuming the input is converted to lower case in the constructor

        // Act
        var result = email.Anonymize();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Anonymize_ShouldThrowExceptionForInvalidEmail()
    {
        // Arrange
        var email = new Email("invalid");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => email.Anonymize());
    }

    [Fact]
    public void Anonymize_ShouldHandleEncodedAtSymbol()
    {
        // Arrange
        var email = new Email("test%40example.com");
        var expected = "t**t@e*****e.com";

        // Act
        var result = email.Anonymize();

        // Assert
        Assert.Equal(expected, result);
    }
}