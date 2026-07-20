using System;
using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve futures trade signal identifiers for a specific value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesTradeSignalIdsQuery : IQuery<FuturesTradeSignalId[]>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesTradeSignalIds";
    [IgnoreMember] public const int ErrorId = 1009;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetFuturesTradeSignalIdsQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetFuturesTradeSignalIdsQuery(DateOnly valueDate)
    {
        ValueDate = valueDate;
        EntityId = new GetFuturesTradeSignalIdsParameter(valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesTradeSignalIdsQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        DateOnly valueDate)         // Key(2)
    {
        Subject = subject;
        EntityId = new GetFuturesTradeSignalIdsParameter(valueDate);
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}