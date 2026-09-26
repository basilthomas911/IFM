using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Gets diagnostics for one durable historical data-load attempt.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFuturesAnalyticsHistoricalDataLoaderQuery
    : IQuery<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel>
{

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="dataLoadAttemptId">The DataLoadAttemptId field.</param>
    [SerializationConstructor]
    public GetFuturesAnalyticsHistoricalDataLoaderQuery(ActorSubject subject, IActorEntityId entityId, Guid dataLoadAttemptId)
    {
        Subject = subject;
        EntityId = entityId;
        DataLoadAttemptId = dataLoadAttemptId;
    }
    /// <summary>Gets the Query actor name.</summary>
    public const string Actor = "FuturesAnalyticsHistoricalDataLoaderQuery";
    /// <summary>Gets the query verb.</summary>
    public const string Verb = "GetDataLoad";
    /// <summary>Gets the stable query error code.</summary>
    public const int ErrorId = 26021;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; }
    /// <summary>Gets the attempt identity.</summary>
    [Key(2)] public Guid DataLoadAttemptId { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams => DataLoadAttemptId.ToString("D");

    /// <summary>Initializes an empty serialization instance.</summary>
    public GetFuturesAnalyticsHistoricalDataLoaderQuery() => EntityId = new FuturesAnalyticsHistoricalDataLoaderEntityId();

    /// <summary>Initializes a query for one attempt.</summary>
    public GetFuturesAnalyticsHistoricalDataLoaderQuery(Guid dataLoadAttemptId)
    {
        DataLoadAttemptId = dataLoadAttemptId;
        EntityId = new FuturesAnalyticsHistoricalDataLoaderEntityId(dataLoadAttemptId);
    }
}
