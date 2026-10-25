using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>Requests projected updates for one futures-contract session.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFuturesVwapSignalHistoryQuery : IQuery<FuturesVwapSignalReadModel[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetFuturesVwapSignalHistoryQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="contractId">The ContractId field.</param>
    /// <param name="valueDate">The ValueDate field.</param>
    /// <param name="configurationId">The ConfigurationId field.</param>
    [SerializationConstructor]
    public GetFuturesVwapSignalHistoryQuery(ActorSubject subject, IActorEntityId entityId, string contractId, DateOnly valueDate, string configurationId)
    {
        Subject = subject;
        EntityId = entityId;
        ContractId = contractId;
        ValueDate = valueDate;
        ConfigurationId = configurationId;
    }
    public const string Verb = "GetHistory";
    public const int ErrorId = 26421;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = default!;
    [Key(2)] public string ContractId { get; init; } = string.Empty;
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public string ConfigurationId { get; init; } = string.Empty;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}
