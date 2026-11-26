using System.Buffers;
using System.Data.Common;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Decodes an owned model without allocating a temporary bytea array per row.</summary>
public static class PooledPostgresPayload
{
    public static T? Deserialize<T>(DbDataReader row, int ordinal)
    {
        var length = checked((int)row.GetBytes(ordinal, 0, null, 0, 0));
        if (length == 0) return default;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var offset = 0;
            while (offset < length)
            {
                var read = checked((int)row.GetBytes(ordinal, offset, buffer, offset, length - offset));
                if (read == 0) throw new EndOfStreamException("PostgreSQL binary payload ended before its declared length.");
                offset += read;
            }
            return MessagePackBinarySerializer.Shared.Deserialize<T>(buffer.AsMemory(0, length));
        }
        finally { buffer.AsSpan(0, length).Clear(); ArrayPool<byte>.Shared.Return(buffer); }
    }
}
