using System.Text;
using Marking.Protocol;

namespace Marking.Client;

public interface ICodeSink
{
    Task WriteAsync(string code, CancellationToken ct);
}

public sealed class CodeFileWriter : ICodeSink, IAsyncDisposable
{
    private readonly FileStream _file;
    private readonly StreamWriter _writer;
    
    public CodeFileWriter(string path)
    {
        path = Path.GetFullPath(path);
        
        string? directoryName = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directoryName)) Directory.CreateDirectory(directoryName);

        _file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 4096,
            useAsync: true);
        
        _writer = new StreamWriter(_file, new UTF8Encoding(false));
        _writer.AutoFlush = true;
    }

    public async Task WriteAsync(string code, CancellationToken ct)
    {
        string replacedCode = GsSymbolReplacer.ReplaceGsSymbols(code);
        await _writer.WriteLineAsync(replacedCode.AsMemory(), ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
    }
}