using TomasAI.IFM.Shared.MarketData.Diagnostics;

namespace TomasAI.IFM.Shared.MarketData.Diagnostics;

/// <summary>Initializes a new TickAggregationProcessingFailure instance.</summary>
/// <param name="Dataset">The dataset.</param>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="RecordKind">The discriminant identifying the market record payload.</param>
/// <param name="PublisherId">The provider publisher identifier.</param>
/// <param name="InstrumentId">The provider instrument identifier.</param>
/// <param name="SourceSequence">The provider sequence number for the source record.</param>
/// <param name="Stage">The stage.</param>
/// <param name="FailedAtUtc">The failed at utc.</param>
/// <param name="ProcessingDuration">The processing duration.</param>
/// <param name="ExceptionType">The exception type.</param>
/// <param name="ExceptionMessage">The exception message.</param>
public sealed record TickAggregationProcessingFailure(
    string Dataset,
    string ContractId,
    string RecordKind,
    ushort PublisherId,
    uint InstrumentId,
    uint SourceSequence,
    TickAggregationProcessingStage Stage,
    DateTimeOffset FailedAtUtc,
    TimeSpan ProcessingDuration,
    string ExceptionType,
    string ExceptionMessage);
