using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>One immutable observation with a sequence assigned once before retryable history writes.</summary>
[MessagePack.MessagePackObject]
public sealed class BufferedFuturesEodRow(FuturesEodDataV2ReadModel snapshot, bool appendHistory)
{
    /// <summary>Gets the admitted observation.</summary>
    [MessagePack.Key(0)] public FuturesEodDataV2ReadModel Snapshot { get; } = snapshot;
    /// <summary>Gets whether this observation contributes an intraday history row.</summary>
    [MessagePack.Key(1)] public bool AppendHistory { get; } = appendHistory;
    /// <summary>Gets or sets the writer-owned stable retry identity.</summary>
    [MessagePack.Key(2)] public long? HistorySequenceId { get; set; }
}
