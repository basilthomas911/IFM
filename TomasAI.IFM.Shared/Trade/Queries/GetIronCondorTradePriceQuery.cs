using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the trade price for an Iron Condor trade based on the specified trade ID and value
/// date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetIronCondorTradePriceQuery : IQuery<TradePriceReadModel>
{
    [IgnoreMember] public const string Actor = "IronCondorTradePriceQuery";
    [IgnoreMember] public const string Verb = "GetIronCondorTradePrice";
    [IgnoreMember] public const int ErrorId = 1044;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int TradeId { get; init; }

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetIronCondorTradePriceQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetIronCondorTradePriceQuery(int tradeId, DateOnly valueDate)
    {
        TradeId = tradeId;
        ValueDate = valueDate;
        EntityId = new GetIronCondorTradePriceParameter(tradeId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetIronCondorTradePriceQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int tradeId,                // Key(2)
        DateOnly valueDate)         // Key(3)
    {
        Subject = subject;
        EntityId = new GetIronCondorTradePriceParameter(tradeId, valueDate);
        TradeId = tradeId;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
