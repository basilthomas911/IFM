using Cassandra;
using Cassandra.Serialization;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>Honors driver offsets and lengths for raw trade UDTs using bounded item copies without per-field boxing.</summary>
public sealed class ScyllaRawTradeUdtSerializer : UdtSerializer
{
    static bool IsRawTrade(IColumnInfo info) => info is UdtColumnInfo udt
        && udt.Name.EndsWith(".tick_trade_item_v1", StringComparison.Ordinal);

    public override object Deserialize(ushort protocolVersion, byte[] buffer, int offset, int length, IColumnInfo typeInfo)
        => IsRawTrade(typeInfo)
            ? buffer.AsSpan(offset, length).ToArray()
            : base.Deserialize(protocolVersion, buffer, offset, length, typeInfo);
}
