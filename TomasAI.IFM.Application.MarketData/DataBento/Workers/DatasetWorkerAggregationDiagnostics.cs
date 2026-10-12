using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Explicit wire representation of aggregation counters and its causal in-flight record.</summary>
[MessagePackObject]
public sealed record DatasetWorkerAggregationDiagnostics(
    [property: Key(0)] long SourceQuoteRecords, [property: Key(1)] long SourceTradeRecords,
    [property: Key(2)] long EmittedQuoteBatches, [property: Key(3)] long EmittedQuoteItems,
    [property: Key(4)] long EmittedTradeEvents, [property: Key(5)] long BufferFullFlushes,
    [property: Key(6)] long PartialQuoteFlushes, [property: Key(7)] long DuplicateSourceSequences,
    [property: Key(8)] long OutOfOrderSourceSequences, [property: Key(9)] long SourceSequenceGaps,
    [property: Key(10)] long PublicationFailures, [property: Key(11)] long ProcessingFailures,
    [property: Key(12)] int ActiveTickers, [property: Key(13)] int ServiceOwnedQuoteBuffers,
    [property: Key(14)] long RecordsStarted, [property: Key(15)] long RecordsCompleted,
    [property: Key(16)] long SourceMboRecords, [property: Key(17)] long SourceStatisticsRecords,
    [property: Key(18)] long StatisticsReplayCompleteRecords, [property: Key(19)] long TradeReplayCompleteRecords,
    [property: Key(20)] long UnsupportedRecords, [property: Key(21)] long CurrentProcessingDurationTicks,
    [property: Key(22)] long TotalProcessingDurationTicks, [property: Key(23)] long MaximumProcessingDurationTicks,
    [property: Key(24)] DateTimeOffset? LastRecordStartedAtUtc, [property: Key(25)] DateTimeOffset? LastRecordCompletedAtUtc,
    [property: Key(26)] DateTimeOffset? LastRecordFailedAtUtc, [property: Key(27)] TickAggregationProcessingStage CurrentStage,
    [property: Key(28)] DatasetWorkerRecordProgress? InFlightRecord)
{
    public void Validate()
    {
        if (!Enum.IsDefined(CurrentStage) || CurrentProcessingDurationTicks < 0)
            throw new InvalidDataException("Worker aggregation diagnostic bounds are invalid.");
        InFlightRecord?.Validate();
    }
    public static DatasetWorkerAggregationDiagnostics From(TickAggregationMetricsSnapshot value) => new(
        value.SourceQuoteRecords, value.SourceTradeRecords, value.EmittedQuoteBatches, value.EmittedQuoteItems,
        value.EmittedTradeEvents, value.BufferFullFlushes, value.PartialQuoteFlushes, value.DuplicateSourceSequences,
        value.OutOfOrderSourceSequences, value.SourceSequenceGaps, value.PublicationFailures, value.ProcessingFailures,
        value.ActiveTickers, value.ServiceOwnedQuoteBuffers, value.RecordsStarted, value.RecordsCompleted,
        value.SourceMboRecords, value.SourceStatisticsRecords, value.StatisticsReplayCompleteRecords,
        value.TradeReplayCompleteRecords, value.UnsupportedRecords, value.CurrentProcessingDurationTicks,
        value.TotalProcessingDurationTicks, value.MaximumProcessingDurationTicks, value.LastRecordStartedAtUtc,
        value.LastRecordCompletedAtUtc, value.LastRecordFailedAtUtc, value.CurrentStage,
        value.InFlightRecord is { } record ? DatasetWorkerRecordProgress.From(record) : null);
    public TickAggregationMetricsSnapshot ToMetrics() => new(SourceQuoteRecords, SourceTradeRecords,
        EmittedQuoteBatches, EmittedQuoteItems, EmittedTradeEvents, BufferFullFlushes, PartialQuoteFlushes,
        DuplicateSourceSequences, OutOfOrderSourceSequences, SourceSequenceGaps, PublicationFailures,
        ProcessingFailures, ActiveTickers, ServiceOwnedQuoteBuffers)
    {
        RecordsStarted = RecordsStarted,
        RecordsCompleted = RecordsCompleted,
        SourceMboRecords = SourceMboRecords,
        SourceStatisticsRecords = SourceStatisticsRecords,
        StatisticsReplayCompleteRecords = StatisticsReplayCompleteRecords,
        TradeReplayCompleteRecords = TradeReplayCompleteRecords,
        UnsupportedRecords = UnsupportedRecords,
        CurrentProcessingDurationTicks = CurrentProcessingDurationTicks,
        TotalProcessingDurationTicks = TotalProcessingDurationTicks,
        MaximumProcessingDurationTicks = MaximumProcessingDurationTicks,
        LastRecordStartedAtUtc = LastRecordStartedAtUtc,
        LastRecordCompletedAtUtc = LastRecordCompletedAtUtc,
        LastRecordFailedAtUtc = LastRecordFailedAtUtc,
        CurrentStage = CurrentStage,
        InFlightRecord = InFlightRecord?.ToProgress()
    };
}
