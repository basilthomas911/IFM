using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the risk position type for futures based on a specific value date and trade type.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesRiskPositionTypeParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateOnly ValueDate { get; init; }
    [Key(1)] public TradeType TradeType { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesRiskPositionTypeParameter() { }

    [SerializationConstructor]
    public GetFuturesRiskPositionTypeParameter(DateOnly valueDate, TradeType tradeType)
    {
        ValueDate = valueDate;
        TradeType = tradeType;
        QueryParams = $"valueDate={ValueDate:yyyy-MM-dd}&tradeType={TradeType}";
    }

    public string Format()
        => $"{ValueDate:yyyy-MM-dd}.{TradeType}";
}
