namespace Marking.Tests.Protocol;

internal sealed class ChunkedStream(byte[] data, int maxChunk) : MemoryStream(data)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) 
        => base.ReadAsync(buffer[..Math.Min(buffer.Length, maxChunk)], cancellationToken);
}