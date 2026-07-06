namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Tests de validación de email.
/// La lógica de validación está en CompanySettingsViewModel (UI),
/// pero la testeamos aquí usando反射 o duplicando la lógica.
/// Para estos tests, usamos la misma lógica regex.
/// </summary>
public class EmailValidationTests
{
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                System.TimeSpan.FromMilliseconds(250));
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return false;
        }
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name@domain.com", true)]
    [InlineData("tech+tag@company.org", true)]
    [InlineData("a@b.co", true)]
    [InlineData("CATTECH@SERVICE.COM", true)]
    public void IsValidEmail_ValidAddresses_ReturnsTrue(string email, bool expected)
    {
        Assert.Equal(expected, IsValidEmail(email));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("noatsign", false)]
    [InlineData("missing@dot", false)]
    [InlineData("@missing-local.com", false)]
    [InlineData("missing-at-sign.com", false)]
    [InlineData("test@.com", false)]
    [InlineData("test@com.", false)]
    public void IsValidEmail_InvalidAddresses_ReturnsFalse(string email, bool expected)
    {
        Assert.Equal(expected, IsValidEmail(email));
    }

    [Fact]
    public void IsValidEmail_Null_ReturnsFalse()
    {
        Assert.False(IsValidEmail(null!));
    }
}
