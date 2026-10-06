using System.Buffers.Binary;
using System.Text;

namespace Marking.Protocol;

public static class MessageFramer
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static async Task WriteMessageAsync(Stream stream, string message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);
        
        int payloadLength = StrictUtf8.GetByteCount(message);
        if (payloadLength < 1 || payloadLength > ProtocolConstants.MaxMessageLength)
            throw new ArgumentOutOfRangeException(nameof(message), payloadLength, $"Длина сообщения должна быть от 1 до {ProtocolConstants.MaxMessageLength} байт, получено {payloadLength}.");

        var frame = new byte[ProtocolConstants.HeaderSize + payloadLength];
        BinaryPrimitives.WriteInt32BigEndian(frame, payloadLength);
        StrictUtf8.GetBytes(message, frame.AsSpan(ProtocolConstants.HeaderSize));
        
        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
    }

    public static async Task<string?> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[ProtocolConstants.HeaderSize];
        if (!await TryReadExactAsync(stream, header, ct).ConfigureAwait(false))
            return null;
        
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < 1 || length > ProtocolConstants.MaxMessageLength)
            throw new ProtocolException($"Недопустимый размер сообщения, он должен быть от 1 до {ProtocolConstants.MaxMessageLength}");

        var payload = new byte[length];
        if (!await TryReadExactAsync(stream, payload, ct).ConfigureAwait(false))
            throw new ProtocolException("Соединение закрыто после заголовка, payload не получен");

        try
        {
            return StrictUtf8.GetString(payload);
        }
        catch(DecoderFallbackException ex)
        {
            throw new ProtocolException("Payload не является корректным UTF-8", ex);
        }
    }

    private static async ValueTask<bool> TryReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        int total = 0;

        while (total < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer[total..], ct).ConfigureAwait(false);

            if (n == 0)
            {
                if (total == 0) return false;
                throw new ProtocolException($"Соединение закрыто посреди сообщения, получено {total} из {buffer.Length} байт");
            }
            
            total += n;
        }
        
        return true;
    }
}