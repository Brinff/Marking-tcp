namespace Marking.Client;

public sealed class ReconnectPolicy
{
    private readonly TimeSpan _initial;
    private readonly TimeSpan _max;
    private TimeSpan _current;
    
    public ReconnectPolicy(TimeSpan initial, TimeSpan max)
    {
        if (initial <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initial), "Начальное значение должно быть больше 0");

        if (max < initial)
            throw new ArgumentOutOfRangeException(nameof(max), "Максимальная задержка должна быть равна или больше начальной");
        
        _initial = initial;
        _max = max;
        _current = initial;
    }
    
    public TimeSpan NextDelay()
    {
        var delay = _current;

        _current = TimeSpan.FromTicks(Math.Min(_max.Ticks, _current.Ticks * 2));

        return delay;
    }
    
    public void Reset()
    {
        _current = _initial;
    }
}