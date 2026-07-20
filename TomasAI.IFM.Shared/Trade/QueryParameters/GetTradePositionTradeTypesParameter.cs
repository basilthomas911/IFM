using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve trade position trade types for a specific order, trade, value date, days to expiry and trade status.
/// </summary>
[MessagePackObject(false)]
public record GetTradePositionTradeTypesParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public int DaysToExpiry { get; init; }
    [Key(4)] public TradeStatus TradeStatus { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradePositionTradeTypesParameter() { }

    [SerializationConstructor]
    public GetTradePositionTradeTypesParameter(int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        QueryParams = $"orderId={OrderId}&tradeId={TradeId}&valueDate={ValueDate:yyyy-MM-dd}&daysToExpiry={DaysToExpiry}&tradeStatus={TradeStatus}";
    }

    public string Format()
        => $"{OrderId}.{TradeId}.{ValueDate:yyyy-MM-dd}.{DaysToExpiry}.{TradeStatus}";
}
