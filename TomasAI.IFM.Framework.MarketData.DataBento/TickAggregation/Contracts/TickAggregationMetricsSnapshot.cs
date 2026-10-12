using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

/// <summary>Initializes a new TickAggregationMetricsSnapshot instance.</summary>
/// <param name="SourceQuoteRecords">The source quote records.</param>
/// <param name="SourceTradeRecords">The source trade records.</param>
/// <param name="EmittedQuoteBatches">The emitted quote batches.</param>
/// <param name="EmittedQuoteItems">The emitted quote items.</param>
/// <param name="EmittedTradeEvents">The emitted trade events.</param>
/// <param name="BufferFullFlushes">The buffer full flushes.</param>
/// <param name="PartialQuoteFlushes">The partial quote flushes.</param>
/// <param name="DuplicateSourceSequences">The duplicate source sequences.</param>
/// <param name="OutOfOrderSourceSequences">The out of order source sequences.</param>
/// <param name="SourceSequenceGaps">The source sequence gaps.</param>
/// <param name="PublicationFailures">The publication failures.</param>
/// <param name="ProcessingFailures">The processing failures.</param>
/// <param name="ActiveTickers">The active tickers.</param>
/// <param name="ServiceOwnedQuoteBuffers">The service owned quote buffers.</param>
public readonly record struct TickAggregationMetricsSnapshot(
    long SourceQuoteRecords,
    long SourceTradeRecords,
    long EmittedQuoteBatches,
    long EmittedQuoteItems,
    long EmittedTradeEvents,
    long BufferFullFlushes,
    long PartialQuoteFlushes,
    long DuplicateSourceSequences,
    long OutOfOrderSourceSequences,
    long SourceSequenceGaps,
    long PublicationFailures,
    long ProcessingFailures,
    int ActiveTickers,
    int ServiceOwnedQuoteBuffers)
{
    public long RecordsStarted { get; init; }
    public long RecordsCompleted { get; init; }
    public long SourceMboRecords { get; init; }
    public long SourceStatisticsRecords { get; init; }
    public long StatisticsReplayCompleteRecords { get; init; }
    public long TradeReplayCompleteRecords { get; init; }
    public long UnsupportedRecords { get; init; }
    public long CurrentProcessingDurationTicks { get; init; }
    public long TotalProcessingDurationTicks { get; init; }
    public long MaximumProcessingDurationTicks { get; init; }
    public DateTimeOffset? LastRecordStartedAtUtc { get; init; }
    public DateTimeOffset? LastRecordCompletedAtUtc { get; init; }
    public DateTimeOffset? LastRecordFailedAtUtc { get; init; }
    public TickAggregationProcessingStage CurrentStage { get; init; }
    public TickAggregationRecordProgress? InFlightRecord { get; init; }
    public TickAggregationProcessingFailure? LastFailure { get; init; }
}
