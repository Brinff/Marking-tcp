using Marking.Server.Gs1;

namespace Marking.Tests.Gs1;

public class Gs1CheckDigitTests
{
    [Theory]
    [InlineData("0400638133393", 1)]
    [InlineData("0000000000000", 0)]
    public void Calculate_KnownValues_ReturnsExpectedDigit(string digits13, int expected)
    {
        Assert.Equal(expected, Gs1CheckDigit.Calculate(digits13));
    }

    [Theory]
    [InlineData("")]
    [InlineData("040063813339")]
    [InlineData("04006381333931")]
    public void Calculate_WrongLength_Throws(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => Gs1CheckDigit.Calculate(input));
    }

    [Theory]
    [InlineData("04006381333a3")]
    [InlineData("0400638133 93")]
    [InlineData("٠٤٠٠٦٣٨١٣٣٣٩٣")]
    public void Calculate_NonAsciiDigits_Throws(string input)
    {
        Assert.ThrowsAny<Exception>(() => Gs1CheckDigit.Calculate(input));
    }
}