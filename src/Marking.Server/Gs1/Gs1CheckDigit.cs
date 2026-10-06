namespace Marking.Server.Gs1;

public static class Gs1CheckDigit
{
    public static int Calculate(ReadOnlySpan<char> digits13)
    {
        if (digits13.Length != 13)
            throw new ArgumentException("digits13 length must be 13");
        
        int result = 0;
        int weight = 3;

        for (var i = digits13.Length - 1; i >= 0; --i)
        {
            if (!char.IsAsciiDigit(digits13[i]))
                throw new ArgumentException("В строке (01) содержатся не только цифры.");
            result += (digits13[i] - '0') * weight;
            weight = (weight + 2) % 4;
        }
        
        return (10 - result % 10) % 10 ;
    }
}