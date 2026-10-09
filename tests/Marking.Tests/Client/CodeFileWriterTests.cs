using Marking.Client;

namespace Marking.Tests.Client;

public sealed class CodeFileWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "marking-tests-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "sub", "codes.txt");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
    
    private static string[] ReadLinesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public async Task Write_CreatesMissingDirectoryAndAppendsLinePerCode()
    {
        await using (var writer = new CodeFileWriter(FilePath))
        {
            await writer.WriteAsync("one", CancellationToken.None);
            await writer.WriteAsync("two", CancellationToken.None);
        }

        Assert.Equal(new[] { "one", "two" }, File.ReadAllLines(FilePath));
    }

    [Fact]
    public async Task Write_ReplacesGroupSeparatorWithMarker()
    {
        await using (var writer = new CodeFileWriter(FilePath))
            await writer.WriteAsync("01AAA\u001D91BBB", CancellationToken.None);

        Assert.Equal(new[] { "01AAA<GS>91BBB" }, File.ReadAllLines(FilePath));
    }

    [Fact]
    public async Task Write_DataIsOnDiskBeforeDispose()
    {
        await using var writer = new CodeFileWriter(FilePath);

        await writer.WriteAsync("flushed", CancellationToken.None);

        Assert.Equal(new[] { "flushed" }, ReadLinesShared(FilePath));
    }

    [Fact]
    public async Task NewInstance_AppendsToExistingFile()
    {
        await using (var first = new CodeFileWriter(FilePath))
            await first.WriteAsync("first", CancellationToken.None);
        await using (var second = new CodeFileWriter(FilePath))
            await second.WriteAsync("second", CancellationToken.None);

        Assert.Equal(new[] { "first", "second" }, File.ReadAllLines(FilePath));
    }

    [Fact]
    public async Task File_HasNoByteOrderMark()
    {
        await using (var writer = new CodeFileWriter(FilePath))
            await writer.WriteAsync("x", CancellationToken.None);

        var bytes = File.ReadAllBytes(FilePath);
        Assert.Equal((byte)'x', bytes[0]);
    }
}