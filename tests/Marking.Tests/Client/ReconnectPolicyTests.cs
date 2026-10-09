using Marking.Client;

namespace Marking.Tests.Client;

public class ReconnectPolicyTests
{
    private static ReconnectPolicy Create()
        => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

    [Fact]
    public void NextDelay_DoublesUntilMax()
    {
        var policy = Create();

        var delays = Enumerable.Range(0, 8).Select(_ => policy.NextDelay().TotalSeconds).ToArray();

        Assert.Equal(new double[] { 1, 2, 4, 8, 16, 30, 30, 30 }, delays);
    }

    [Fact]
    public void Reset_StartsOver()
    {
        var policy = Create();
        policy.NextDelay();
        policy.NextDelay();
        policy.NextDelay();

        policy.Reset();

        Assert.Equal(TimeSpan.FromSeconds(1), policy.NextDelay());
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(-1, 30)]
    [InlineData(5, 1)]
    public void Constructor_InvalidArguments_Throws(int initialSeconds, int maxSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReconnectPolicy(TimeSpan.FromSeconds(initialSeconds), TimeSpan.FromSeconds(maxSeconds)));
    }
}