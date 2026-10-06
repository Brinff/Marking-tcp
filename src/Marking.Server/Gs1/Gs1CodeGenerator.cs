using System.Text;

namespace Marking.Server.Gs1;

public sealed class Gs1CodeGenerator : ICodeGenerator
{
    private readonly Random _random;
    
    private const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string Digits = "0123456789";
    
    private const char Gs = '\u001D';
    private const string GtinAi = "01";
    private const string SerialAi = "21";
    private const string KeyAi = "91";
    private const string SignatureAi = "92";
    
    public Gs1CodeGenerator(Random random)
    {
        _random = random;
    }
    
    public string Next()
    { 
        StringBuilder resultBuilder = new StringBuilder(92);
        
        BuildGtinAi(resultBuilder);
        BuildSerialAi(resultBuilder);
        BuildKeyAi(resultBuilder);
        BuildSignatureAi(resultBuilder);
        
        return resultBuilder.ToString();
    }

    private void BuildGtinAi(StringBuilder resultBuilder)
    {
        var digits = _random.GetString(Digits, 13);
        var checkDigit = Gs1CheckDigit.Calculate(digits);
        resultBuilder.Append(GtinAi)
            .Append(digits)
            .Append(checkDigit);
    }

    private void BuildSerialAi(StringBuilder resultBuilder)
    {
        resultBuilder.Append(SerialAi)
            .Append(_random.GetString(Alphanumeric, 20))
            .Append(Gs);
    }

    private void BuildKeyAi(StringBuilder resultBuilder)
    {
        resultBuilder.Append(KeyAi)
            .Append(_random.GetString(Alphanumeric, 4))
            .Append(Gs);
    }

    private void BuildSignatureAi(StringBuilder resultBuilder)
    {
        byte[] bytes =  new byte[32];
        _random.NextBytes(bytes);
        resultBuilder.Append(SignatureAi)
            .Append(Convert.ToBase64String(bytes));
    }
}