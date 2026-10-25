using TomasAI.IFM.Shared.MarketData.Diagnostics;

namespace TomasAI.IFM.Shared.MarketData.Diagnostics;

/// <summary>Initializes a new TickAggregationRecordProgress instance.</summary>
/// <param name="Dataset">The dataset.</param>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="RecordKind">The discriminant identifying the market record payload.</param>
/// <param name="PublisherId">The provider publisher identifier.</param>
/// <param name="InstrumentId">The provider instrument identifier.</param>
/// <param name="SourceSequence">The provider sequence number for the source record.</param>
/// <param name="StartedAtUtc">The started at utc.</param>
public sealed record TickAggregationRecordProgress(
    string Dataset,
    string ContractId,
    string RecordKind,
    ushort PublisherId,
    uint InstrumentId,
    uint SourceSequence,
    DateTimeOffset StartedAtUtc);
