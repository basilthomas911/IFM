using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Generated structured diagnostics for tick processing and transient publication failures.</summary>
internal static partial class TickAggregationLog
{
    [LoggerMessage(201, LogLevel.Error, "Tick aggregation failed processing dataset {Dataset}, contract {ContractId}, record {RecordKind}, publisher {PublisherId}, instrument {InstrumentId}, sequence {SourceSequence}, stage {ProcessingStage}, elapsed {ElapsedMilliseconds} ms. Records started {RecordsStarted}, completed {RecordsCompleted}, failed {ProcessingFailures}; quotes {SourceQuoteRecords}, trades {SourceTradeRecords}, MBO {SourceMboRecords}, statistics {SourceStatisticsRecords}; publication failures {PublicationFailures}.")]
    internal static partial void ProcessingFailed(ILogger logger, Exception exception, string dataset, string contractId, string recordKind, ushort publisherId, uint instrumentId, uint sourceSequence, TickAggregationProcessingStage processingStage, double elapsedMilliseconds, long recordsStarted, long recordsCompleted, long processingFailures, long sourceQuoteRecords, long sourceTradeRecords, long sourceMboRecords, long sourceStatisticsRecords, long publicationFailures);

    [LoggerMessage(202, LogLevel.Warning, "Tick aggregation handled a non-durable publication failure for {PublicationType}; dataset {Dataset}, contract {ContractId}, record {RecordKind}, publisher {PublisherId}, instrument {InstrumentId}, sequence {SourceSequence}, stage {ProcessingStage}. The hot cache remains current and record processing will continue. Publication failures {PublicationFailures}.")]
    internal static partial void PublicationFailed(ILogger logger, Exception exception, string publicationType, string dataset, string contractId, string recordKind, ushort publisherId, uint instrumentId, uint sourceSequence, TickAggregationProcessingStage processingStage, long publicationFailures);
}
