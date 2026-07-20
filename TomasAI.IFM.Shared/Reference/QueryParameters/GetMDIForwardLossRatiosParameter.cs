using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve MDI forward loss ratios for a specific trend direction and trade type.
/// </summary>
/// <remarks>Use this type to specify the trend direction and trade type when requesting MDI forward loss ratios.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetMDIForwardLossRatiosParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public IntrinsicTimeTrendType TrendDirection { get; init; }
    [Key(1)] public TradeType TradeType { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetMDIForwardLossRatiosParameter() { }

    [SerializationConstructor]
    public GetMDIForwardLossRatiosParameter(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
    {
        TrendDirection = trendDirection;
        TradeType = tradeType;
        QueryParams = $"trendDirection={TrendDirection}&tradeType={TradeType}";
    }

    public string Format()
        => $"{TrendDirection}.{TradeType}";
}
