using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815", Justification = "This sequential native ABI buffer is transported by pointer and is not a value identity.")]
[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
public readonly struct MarketRecordHeader32
{
    public readonly uint InstrumentId;
    public readonly ushort PublisherId;
    public readonly MarketRecordKind RecordKind;
    public readonly byte Flags;
    public readonly long EventTimestampNanoseconds;
    public readonly long ReceiveTimestampNanoseconds;
    public readonly uint Sequence;
    public readonly ushort SourceSchema;
    public readonly ushort Reserved;

    /// <summary>Initializes a new MarketRecordHeader32 instance.</summary>
    /// <param name="instrumentId">The provider instrument identifier.</param>
    /// <param name="publisherId">The provider publisher identifier.</param>
    /// <param name="recordKind">The discriminant identifying the market record payload.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="eventTimestampNanoseconds">The source event timestamp in nanoseconds since the Unix epoch.</param>
    /// <param name="receiveTimestampNanoseconds">The receive timestamp in nanoseconds since the Unix epoch.</param>
    /// <param name="sequence">The provider record sequence number.</param>
    /// <param name="sourceSchema">The provider schema from which the record originated.</param>
    public MarketRecordHeader32(
        uint instrumentId, ushort publisherId, MarketRecordKind recordKind,
        byte flags, long eventTimestampNanoseconds, long receiveTimestampNanoseconds,
        uint sequence, ushort sourceSchema = 0)
    {
        InstrumentId = instrumentId; PublisherId = publisherId; RecordKind = recordKind;
        Flags = flags; EventTimestampNanoseconds = eventTimestampNanoseconds;
        ReceiveTimestampNanoseconds = receiveTimestampNanoseconds;
        Sequence = sequence; SourceSchema = sourceSchema; Reserved = 0;
    }
}
