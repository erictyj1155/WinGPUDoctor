using System.Buffers.Binary;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Protocol;

internal static class ProtocolFraming
{
    internal static byte[] Encode(ProtocolFrame frame)
    {
        var payload = ProtocolCodec.Encode(frame);
        var buffer = new byte[sizeof(uint) + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)payload.Length);
        payload.CopyTo(buffer, sizeof(uint));
        return buffer;
    }

    internal static uint ReadLength(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length != sizeof(uint))
            throw new ProtocolValidationException(ReasonCode.InvalidValue, "Frame prefix is not four bytes.");
        return BinaryPrimitives.ReadUInt32LittleEndian(prefix);
    }

    internal static async ValueTask WriteAsync(Stream stream, ProtocolFrame frame, CancellationToken cancellationToken)
    {
        var bytes = Encode(frame);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<ProtocolFrame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var prefix = new byte[sizeof(uint)];
        var read = await ReadExactlyAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        if (read == 0) throw new EndOfStreamException("Worker pipe ended before a frame was received.");
        var length = ReadLength(prefix);
        if (length > ProtocolConstants.MaxDataFrameBytes)
            throw new ProtocolValidationException(ReasonCode.ResourceLimit, "Frame exceeds the protocol limit.");
        var payload = new byte[length];
        if (await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false) != payload.Length)
            throw new ProtocolValidationException(ReasonCode.InvalidValue, "Frame ended before its declared payload length.");
        return ProtocolCodec.Decode(payload);
    }

    private static async ValueTask<int> ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0) return total;
            total += read;
        }
        return total;
    }
}
