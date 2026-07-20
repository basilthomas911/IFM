using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.OptionPricer.Queries;

/// <summary>
/// MessagePack-serializable query to check if a spread distribution job is currently in progress for a specific order and trade.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetSpreadDistributionJobInProgressQuery : IQuery<ScalarReadModel<bool>>
{
    [IgnoreMember] public const string Actor = "SpreadDistributionJobInProgressQuery";
    [IgnoreMember] public const string Verb = "GetSpreadDistributionJobInProgress";
    [IgnoreMember] public const int ErrorId = 1018;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int OrderId { get; init; }

    [Key(3)]
    public int TradeId { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetSpreadDistributionJobInProgressQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetSpreadDistributionJobInProgressQuery(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;
        EntityId = new GetSpreadDistributionJobInProgressParameter(orderId, tradeId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetSpreadDistributionJobInProgressQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId,                // Key(2)
        int tradeId)                // Key(3)
    {
        Subject = subject;
        EntityId = new GetSpreadDistributionJobInProgressParameter(orderId, tradeId);
        OrderId = orderId;
        TradeId = tradeId;
        ErrorCode = ErrorId;
    }
}
