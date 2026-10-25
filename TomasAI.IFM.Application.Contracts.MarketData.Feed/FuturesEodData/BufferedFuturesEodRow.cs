using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>One immutable observation with a sequence assigned once before retryable history writes.</summary>
public sealed class BufferedFuturesEodRow(FuturesEodDataV2ReadModel snapshot, bool appendHistory)
{
    /// <summary>Gets the admitted observation.</summary>
    public FuturesEodDataV2ReadModel Snapshot { get; } = snapshot;
    /// <summary>Gets whether this observation contributes an intraday history row.</summary>
    public bool AppendHistory { get; } = appendHistory;
    /// <summary>Gets or sets the writer-owned stable retry identity.</summary>
    public long? HistorySequenceId { get; set; }
}
