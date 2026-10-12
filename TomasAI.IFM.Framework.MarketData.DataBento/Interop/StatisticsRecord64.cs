using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815", Justification = "This sequential native ABI buffer is transported by pointer and is not a value identity.")]
[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct StatisticsRecord64
{
    public readonly MarketRecordHeader32 Header;
    public readonly long Price;
    public readonly long Quantity;
    public readonly long ReferenceTimestampNanoseconds;
    public readonly ushort StatisticType;
    public readonly ushort ChannelId;
    public readonly byte UpdateAction;
    public readonly byte StatisticFlags;
    private readonly ushort _reserved16;

    /// <summary>Initializes a new StatisticsRecord64 instance.</summary>
    /// <param name="header">The common instrument, timestamp, and sequence header.</param>
    /// <param name="price">The trade or statistics price.</param>
    /// <param name="quantity">The record quantity.</param>
    /// <param name="referenceTimestampNanoseconds">The statistics reference timestamp in nanoseconds since the Unix epoch.</param>
    /// <param name="statisticType">The type of exchange statistic represented by this record.</param>
    /// <param name="channelId">The channel id.</param>
    /// <param name="updateAction">The action identifying how to apply the statistics update.</param>
    /// <param name="statisticFlags">The provider statistics record flags.</param>
    public StatisticsRecord64(
        MarketRecordHeader32 header,
        long price,
        long quantity,
        long referenceTimestampNanoseconds,
        ushort statisticType,
        ushort channelId,
        byte updateAction,
        byte statisticFlags)
    {
        Header = header;
        Price = price;
        Quantity = quantity;
        ReferenceTimestampNanoseconds = referenceTimestampNanoseconds;
        StatisticType = statisticType;
        ChannelId = channelId;
        UpdateAction = updateAction;
        StatisticFlags = statisticFlags;
        _reserved16 = 0;
    }
}
