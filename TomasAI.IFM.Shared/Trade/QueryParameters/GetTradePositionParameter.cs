using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a trade position by specific criteria.
/// </summary>
[MessagePackObject(false)]
public record GetTradePositionParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }
    [Key(2)] public TradeType TradeType { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public int DaysToExpiry { get; init; }
    [Key(5)] public TradeStatus TradeStatus { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradePositionParameter() { }

    [SerializationConstructor]
    public GetTradePositionParameter(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        QueryParams = $"orderId={OrderId}&tradeId={TradeId}&tradeType={TradeType}&valueDate={ValueDate:yyyy-MM-dd}&daysToExpiry={DaysToExpiry}&tradeStatus={TradeStatus}";
    }

    public string Format()
        => $"{OrderId}.{TradeId}.{TradeType}.{ValueDate:yyyy-MM-dd}.{DaysToExpiry}.{TradeStatus}";
}
