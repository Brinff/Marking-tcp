namespace Marking.Protocol;

public static class GsSymbolReplacer
{
    public static string ReplaceGsSymbols(string code)
    {
        return code.Replace("\u001D", "<GS>");
    }
}