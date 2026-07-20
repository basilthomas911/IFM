using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve option trade spread data.
/// </summary>
[MessagePackObject(false)]
public record GetOptionTradeSpreadDataParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }
    [Key(2)] public TradeType TradeType { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetOptionTradeSpreadDataParameter() { }

    [SerializationConstructor]
    public GetOptionTradeSpreadDataParameter(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        QueryParams = $"orderId={OrderId}&tradeId={TradeId}&tradeType={TradeType}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{OrderId}.{TradeId}.{TradeType}.{ValueDate:yyyy-MM-dd}";
}
