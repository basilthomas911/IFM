using System.Buffers.Binary;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Dedicated response pipe avoids deadlock with control commands that drain active feeds.</summary>
public static class OptionTradeAcknowledgment
{
    public static async ValueTask WriteAsync(Stream stream, long sequence, Guid generation, bool retained, CancellationToken token)
    {
        var frame = new byte[25];
        BinaryPrimitives.WriteInt64LittleEndian(frame, sequence);
        generation.TryWriteBytes(frame.AsSpan(8, 16));
        frame[24] = retained ? (byte)1 : (byte)0;
        await stream.WriteAsync(frame, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
    public static async ValueTask ReadAsync(Stream stream, long sequence, Guid generation, CancellationToken token)
    {
        var frame = new byte[25];
        await stream.ReadExactlyAsync(frame, token).ConfigureAwait(false);
        if (BinaryPrimitives.ReadInt64LittleEndian(frame) != sequence || new Guid(frame.AsSpan(8, 16)) != generation
            || frame[24] != 1)
            throw new InvalidDataException("Option trade retention was rejected or acknowledged with the wrong identity.");
    }
}
