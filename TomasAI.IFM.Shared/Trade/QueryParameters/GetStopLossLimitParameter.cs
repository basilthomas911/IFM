using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the stop loss limit for a specific order and trade.
/// </summary>
[MessagePackObject(false)]
public record GetStopLossLimitParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetStopLossLimitParameter() { }

    [SerializationConstructor]
    public GetStopLossLimitParameter(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;
        QueryParams = $"orderId={OrderId}&tradeId={TradeId}";
    }

    public string Format()
        => $"{OrderId}.{TradeId}";
}
