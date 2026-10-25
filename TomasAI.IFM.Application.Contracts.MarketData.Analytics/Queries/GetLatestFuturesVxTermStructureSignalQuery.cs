using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>Requests the latest projected VX term-structure signal for a stream.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetLatestFuturesVxTermStructureSignalQuery
    : IQuery<FuturesVxTermStructureSignalReadModel?>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetLatestFuturesVxTermStructureSignalQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="valueDate">The ValueDate field.</param>
    /// <param name="configurationId">The ConfigurationId field.</param>
    [SerializationConstructor]
    public GetLatestFuturesVxTermStructureSignalQuery(ActorSubject subject, IActorEntityId entityId, DateOnly valueDate, string configurationId)
    {
        Subject = subject;
        EntityId = entityId;
        ValueDate = valueDate;
        ConfigurationId = configurationId;
    }
    public const string Actor = "FuturesVxTermStructureSignalQuery";
    public const string Verb = "GetLatest";
    public const int ErrorId = 26310;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = default!;
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public string ConfigurationId { get; init; } = string.Empty;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}
