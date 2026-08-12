using System.Buffers.Binary;
using System.Text;

namespace QuickConvert.Core.Messaging;

public static class NativeMessageProtocol
{
    private const int MaximumMessageBytes = 1024 * 1024;

    public static async Task<string?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[sizeof(int)];
        var firstRead = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
        if (firstRead == 0)
            return null;
        await ReadRemainderAsync(stream, header, firstRead, cancellationToken).ConfigureAwait(false);

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 0 or > MaximumMessageBytes)
            throw new InvalidDataException("Nieprawidłowy rozmiar wiadomości.");

        var payload = new byte[length];
        await ReadRemainderAsync(stream, payload, 0, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(payload);
    }

    public static async Task WriteAsync(Stream stream, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);
        var payload = Encoding.UTF8.GetBytes(message);
        if (payload.Length > MaximumMessageBytes)
            throw new InvalidDataException("Wiadomość jest zbyt duża.");

        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadRemainderAsync(
        Stream stream,
        byte[] buffer,
        int offset,
        CancellationToken cancellationToken)
    {
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Niekompletna wiadomość.");
            offset += read;
        }
    }
}
