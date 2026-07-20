using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve option trade details based on order and trade ids.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetOptionTradeQuery : IQuery<OptionTradeReadModel>
{
    [IgnoreMember] public const string Actor = "OptionTradeQuery";
    [IgnoreMember] public const string Verb = "GetOptionTrade";
    [IgnoreMember] public const int ErrorId = 1019;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int OrderId { get; init; }

    [Key(3)]
    public int TradeId { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetOptionTradeQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetOptionTradeQuery(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;
        EntityId = new GetOptionTradeParameter(orderId, tradeId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetOptionTradeQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId,                // Key(2)
        int tradeId)                // Key(3)
    {
        Subject = subject;
        EntityId = new GetOptionTradeParameter(orderId, tradeId);
        OrderId = orderId;
        TradeId = tradeId;
        ErrorCode = ErrorId;
    }
}

