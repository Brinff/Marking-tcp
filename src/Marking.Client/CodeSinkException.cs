namespace Marking.Client;

public sealed class CodeSinkException : Exception
{
    public CodeSinkException(string message) : base(message) { }
    public CodeSinkException(string message, Exception inner) : base(message, inner) { }
}