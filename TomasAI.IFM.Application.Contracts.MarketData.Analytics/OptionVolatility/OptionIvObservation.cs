using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>One immutable, comparable IV observation for a configured value-date sampling slot.</summary>
[MessagePackObject]
public sealed record OptionIvObservation(
    [property: Key(0)] ushort SchemaVersion,
    [property: Key(1)] string ObservationId,
    [property: Key(2)] VolatilitySeriesIdentity Series,
    [property: Key(3)] DateOnly ExchangeValueDate,
    [property: Key(4)] string SamplingSlot,
    [property: Key(5)] decimal? ImpliedVolatility,
    [property: Key(6)] VolatilityValueUnit Unit,
    [property: Key(7)] VolatilityObservationStatus Status,
    [property: Key(8)] string StatusReason,
    [property: Key(9)] DateTimeOffset ObservedAtUtc,
    [property: Key(10)] DateTimeOffset RecordedAtUtc,
    [property: Key(11)] DateTimeOffset AvailableAtUtc,
    [property: Key(12)] int Revision,
    [property: Key(13)] string? SupersedesObservationId,
    [property: Key(14)] VolatilityObservationProvenance Provenance)
{
    public const ushort CurrentSchemaVersion = 1;
}
