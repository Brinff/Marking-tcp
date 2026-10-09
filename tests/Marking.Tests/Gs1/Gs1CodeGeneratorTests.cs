using System.Text.RegularExpressions;
using Marking.Protocol;
using Marking.Server.Gs1;

namespace Marking.Tests.Gs1;

public class Gs1CodeGeneratorTests
{
    private static readonly Regex Format = new(
        @"^01\d{14}21[A-Za-z0-9]{20}\u001D91[A-Za-z0-9]{4}\u001D92[A-Za-z0-9+/]{43}=$");

    private static Gs1CodeGenerator Create(int seed = 42) => new(new Random(seed));

    [Fact]
    public void Next_MatchesRussianGs1Format()
        => Assert.Matches(Format, Create().Next());

    [Fact]
    public void Next_GtinHasValidCheckDigit()
    {
        var code = Create().Next();
        var gtin = code.Substring(2, 14);

        Assert.Equal(Gs1CheckDigit.Calculate(gtin[..13]), gtin[13] - '0');
    }

    [Fact]
    public void Next_ContainsExactlyTwoGroupSeparators()
        => Assert.Equal(2, Create().Next().Count(c => c == '\u001D'));

    [Fact]
    public void Next_FitsProtocolLimit()
        => Assert.True(Create().Next().Length <= ProtocolConstants.MaxMessageLength);

    [Fact]
    public void Next_SameSeed_SameSequence()
    {
        var a = Create(7);
        var b = Create(7);

        for (int i = 0; i < 10; i++)
            Assert.Equal(a.Next(), b.Next());
    }

    [Fact]
    public void Next_ConsecutiveCalls_Differ()
    {
        var g = Create();

        Assert.NotEqual(g.Next(), g.Next());
    }

    [Fact]
    public void Next_ThousandCodes_AllValid()
    {
        var g = Create();

        for (int i = 0; i < 1000; i++)
        {
            var code = g.Next();
            Assert.Matches(Format, code);
            Assert.Equal(Gs1CheckDigit.Calculate(code.AsSpan(2, 13)), code[15] - '0');
        }
    }
}