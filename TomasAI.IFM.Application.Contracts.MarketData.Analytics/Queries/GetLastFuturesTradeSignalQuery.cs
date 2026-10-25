using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the most recent futures trade signal.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetLastFuturesTradeSignalQuery : IQuery<FuturesTradeSignalV2ReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalQuery";
    [IgnoreMember] public const string Verb = "GetLastFuturesTradeSignal";
    [IgnoreMember] public const int ErrorId = 1009;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetLastFuturesTradeSignalQuery()
    {
        EntityId = new GetLastFuturesTradeSignalParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetLastFuturesTradeSignalQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId)    // Key(1)
    {
        Subject = subject;
        EntityId = new GetLastFuturesTradeSignalParameter();
        ErrorCode = ErrorId;
    }
}
