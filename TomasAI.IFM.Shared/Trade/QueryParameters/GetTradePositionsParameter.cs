using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve trade positions for a specific order and trade.
/// </summary>
[MessagePackObject(false)]
public record GetTradePositionsParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradePositionsParameter() { }

    [SerializationConstructor]
    public GetTradePositionsParameter(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;
        QueryParams = $"orderId={OrderId}&tradeId={TradeId}";
    }

    public string Format()
        => $"{OrderId}.{TradeId}";
}
