using Module4.Domain.Models;
using Module4.Domain.Services;

namespace Module4.Tests;

public class PassportValidatorTests
{
    [Theory]
    [InlineData("4509", "638172")]
    [InlineData("7702", "112233")]
    [InlineData("5012", "456789")]
    public void Validate_AcceptsCorrectPassports(string series, string number)
    {
        var passport = new PassportData(series, number);
        var outcome = PassportValidator.Validate(passport);

        Assert.True(outcome.IsValid);
        Assert.Equal(ValidationOutcome.ValidMessage, outcome.Message);
        Assert.Empty(outcome.Reasons);
    }

    [Theory]
    [InlineData("0691", "084DH")]     // буквы в номере
    [InlineData("ABCD", "123456")]    // буквы в серии
    [InlineData("123", "456789")]     // короткая серия
    [InlineData("12345", "456789")]   // длинная серия
    [InlineData("4509", "12345")]     // короткий номер
    [InlineData("4509", "1234567")]   // длинный номер
    [InlineData("", "456789")]        // пустая серия
    [InlineData("4509", "")]          // пустой номер
    public void Validate_RejectsBadFormat(string series, string number)
    {
        var passport = new PassportData(series, number);
        var outcome = PassportValidator.Validate(passport);

        Assert.False(outcome.IsValid);
        Assert.Equal(ValidationOutcome.InvalidMessage, outcome.Message);
        Assert.NotEmpty(outcome.Reasons);
    }

    [Fact]
    public void Validate_RejectsZeroPassport()
    {
        var passport = new PassportData("0000", "000000");
        var outcome = PassportValidator.Validate(passport);

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Reasons, r => r.Contains("нул", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TrimsWhitespace()
    {
        var passport = new PassportData(" 4509 ", " 638172 ");
        var outcome = PassportValidator.Validate(passport);

        Assert.True(outcome.IsValid);
    }
}
