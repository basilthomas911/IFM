using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;

/// <summary>Gets the latest successfully persisted regime-indicator snapshot for one series and timeframe.</summary>
[MessagePackObject]
public sealed record GetLatestFuturesRegimeIndicatorSnapshotQuery
    : IQuery<FuturesRegimeIndicatorSnapshot>
{
    /// <summary>Gets the query actor name.</summary>
    public const string Actor = "FuturesRegimeIndicatorQuery";
    /// <summary>Gets the query verb.</summary>
    public const string Verb = "GetLatest";
    /// <summary>Gets the stable query error code.</summary>
    public const int ErrorId = 26031;

    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; }
    /// <summary>Gets the exact market series.</summary>
    [Key(2)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }
    /// <summary>Gets the requested timeframe.</summary>
    [Key(3)] public TimeFrameType TimeFrame { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams =>
        new FuturesTradeSessionBarEntityId(MarketSeriesIdentity, TimeFrame).Format();

    /// <summary>Initializes an empty serialization instance.</summary>
    public GetLatestFuturesRegimeIndicatorSnapshotQuery() =>
        EntityId = new FuturesTradeSessionBarEntityId();

    /// <summary>Initializes an exact latest-snapshot query.</summary>
    public GetLatestFuturesRegimeIndicatorSnapshotQuery(
        MarketSeriesIdentity marketSeriesIdentity,
        TimeFrameType timeFrame)
    {
        MarketSeriesIdentity = marketSeriesIdentity;
        TimeFrame = timeFrame;
        EntityId = new FuturesTradeSessionBarEntityId(marketSeriesIdentity, timeFrame);
    }
}
